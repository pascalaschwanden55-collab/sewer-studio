using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Orchestrates the Multi-Model pipeline per frame:
/// YOLO (pre-screening) -> DINO (detection) -> SAM (segmentation) -> Quantification -> Qwen VSA-Code.
/// Output is convertible to the existing <see cref="EnhancedFrameAnalysis"/> / <see cref="RawVideoDetection"/>.
/// </summary>
public sealed partial class MultiModelAnalysisService
{
    private readonly IVisionPipelineClient _client;
    private readonly PipelineConfig _config;
    private readonly EnhancedVisionAnalysisService? _qwenVision;
    private readonly ILogger _logger;
    private readonly IPipelineTraceWriter _pipelineTraceWriter;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly VideoProbeService _videoProbe;
    // Checkpoint-Journal (Resume): null = ohne Journal (Tests/aeltere Aufrufer).
    private readonly IAnalysisCheckpointJournal? _checkpointJournal;
    // Ausgelagerte Modellschritte (AP05b); ohne eigenen Laufzustand, daher je Dienst einmal.
    private readonly MultiModelClsVorfilter _clsVorfilter;
    private readonly MultiModelYoloSchritt _yoloSchritt;
    private readonly MultiModelDinoSchritt _dinoSchritt;
    private readonly MultiModelSamSchritt _samSchritt;

    /// <summary>
    /// Klassifikator als fuehrende Code-Quelle (Paket 2): ResolveFromClassifier +
    /// Temporal-Voting setzen den VSA-Code, Qwen liefert nur noch OSD/Beschreibung
    /// und fuellt unsichere Faelle. Default AUS, bis der End-to-End-Eval gruen ist
    /// (Env: SEWERSTUDIO_CLASSIFIER_DECISION=1).
    /// </summary>
    public bool ClassifierDecisionEnabled { get; set; }

    /// <summary>
    /// Fix #1: Wenn DINO keine Box liefert, aber der Klassifikator einen Grundgeruest-Code
    /// (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, wird ein box-loser Befund erzeugt,
    /// statt den Frame still zu verwerfen. Default AN, reversibel ueber Env.
    /// </summary>
    public bool ClassifierOnlyStructuralEnabled { get; set; }

    /// <summary>Mindestkonfidenz fuer den box-losen Grundgeruest-Befund (Fix #1).</summary>
    public double ClassifierOnlyMinConfidence { get; set; } = 0.60;

    // Erwartete Eigengewichte fuer die COCO-Fallback-Warnung. Liefert der Sidecar
    // einen anderen Modellnamen (z.B. yolo11m.pt), wird einmal pro Lauf gewarnt.
    private readonly string _expectedYoloModel;

    // Gecachter minimaler Confidence-Schwellenwert (einmal berechnet statt pro Frame)
    private readonly double _minClassConfidence;

    // Test-Seams: null = Produktiv-Verhalten (VideoFrameStream / GetVideoDurationAsync)
    private readonly Func<string, string, double, double, CancellationToken, IAsyncEnumerable<FrameData>>? _frameSource;
    private readonly Func<string, CancellationToken, Task<double>>? _durationProbe;
    private readonly bool _frameSourceOverridden;

    public MultiModelAnalysisService(
        IVisionPipelineClient client,
        PipelineConfig config,
        string ffmpegPath = "ffmpeg",
        EnhancedVisionAnalysisService? qwenVision = null,
        ILogger? logger = null,
        Func<string, string, double, double, CancellationToken, IAsyncEnumerable<FrameData>>? frameSource = null,
        Func<string, CancellationToken, Task<double>>? durationProbe = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null, IProcessOutputReader? processOutputs = null,
        IAnalysisCheckpointJournal? checkpointJournal = null, ISidecarRestartService? sidecarRestart = null)
        : this(
            PipelineTraceWriter.Current,
            client,
            config,
            ffmpegPath,
            qwenVision,
            logger,
            frameSource,
            durationProbe,
            pipelineEnvironmentOptions, processOutputs, checkpointJournal, sidecarRestart)
    {
    }

    public MultiModelAnalysisService(
        IPipelineTraceWriter pipelineTraceWriter,
        IVisionPipelineClient client,
        PipelineConfig config,
        string ffmpegPath = "ffmpeg",
        EnhancedVisionAnalysisService? qwenVision = null,
        ILogger? logger = null,
        Func<string, string, double, double, CancellationToken, IAsyncEnumerable<FrameData>>? frameSource = null,
        Func<string, CancellationToken, Task<double>>? durationProbe = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null, IProcessOutputReader? processOutputs = null,
        IAnalysisCheckpointJournal? checkpointJournal = null, ISidecarRestartService? sidecarRestart = null)
    {
        _pipelineTraceWriter = pipelineTraceWriter ?? throw new ArgumentNullException(nameof(pipelineTraceWriter));
        var options = pipelineEnvironmentOptions ?? PipelineEnvironmentOptions.Current;
        _client = client;
        _config = config;
        _qwenVision = qwenVision;
        _logger = logger ?? NullLogger.Instance;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = DeriveFfprobePath(ffmpegPath);
        _videoProbe = new VideoProbeService(ffprobePath: _ffprobePath, ffmpegPath: _ffmpegPath, processOutputs: processOutputs);
        _minClassConfidence = config.YoloClassConfidence.Count > 0
            ? config.YoloClassConfidence.Values.Min()
            : config.YoloConfidence;
        _frameSource = frameSource;
        _durationProbe = durationProbe;
        _frameSourceOverridden = frameSource is not null;
        ClassifierDecisionEnabled = options.ClassifierDecisionEnabled();
        ClassifierOnlyStructuralEnabled = options.ClassifierOnlyStructuralEnabled();
        _expectedYoloModel = options.ExpectedYoloModel();
        _checkpointJournal = checkpointJournal;
        _sidecarRestart = sidecarRestart;
        _clsVorfilter = new MultiModelClsVorfilter(client, _logger);
        _yoloSchritt = new MultiModelYoloSchritt(client, config, _minClassConfidence, _expectedYoloModel, _logger);
        _dinoSchritt = new MultiModelDinoSchritt(client, config, _logger);
        _samSchritt = new MultiModelSamSchritt(client, _logger);
    }
    public static (string MeterSource, bool IsMeterEstimated) GetDedupMeterMetadata(bool qwenMeterAccepted)
        => qwenMeterAccepted ? ("QwenOsd", false) : ("LinearEstimate", true);

    /// <summary>
    /// Run the full multi-model pipeline on a video file.
    /// Returns the same <see cref="VideoAnalysisResult"/> as the Ollama-only path.
    /// </summary>
    public async Task<VideoAnalysisResult> AnalyzeAsync(
        string videoPath,
        IProgress<VideoAnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        videoPath = NormalizePath(videoPath);
        // File-Existenz-Check nur ohne frameSource-Override (Tests nutzen Dummy-Pfad).
        if (!_frameSourceOverridden && !File.Exists(videoPath))
            return VideoAnalysisResult.Failed($"Video nicht gefunden: {videoPath}");

        progress?.Report(new VideoAnalysisProgress(0, 0, "Multi-Model: Videodauer wird ermittelt..."));

        var durationFunc = _durationProbe ?? GetVideoDurationAsync;
        var duration = await durationFunc(videoPath, ct).ConfigureAwait(false);
        if (duration <= 0)
            return VideoAnalysisResult.Failed("Videodauer konnte nicht ermittelt werden.");

        var totalFrames = (int)Math.Ceiling(duration / FrameStepSeconds);
        // Laufzustand: eine Instanz je Lauf, nichts davon ueberlebt das Ergebnis.
        // Haltungs-DN vor globaler Vorgabe; unbekannt liefert keine erfundenen Masse.
        var run = new MultiModelLaufZustand(videoPath, duration, totalFrames,
            PipelinePipeDiameterPolicy.Resolve(_config) ?? 0,
            new TemporalFindingDeduplicator(new TemporalDedupOptions
            {
                DedupWindowFrames = DedupWindowFrames,
                NormalizeFallbackLabels = true,
                // Klassifikator-Regime: Ganzbild-Code darf nicht ueber Masken-Uhrlagen
                // aufsplitten (Pilot 2026-06-10: 12x BDD statt 1 Befund)
                ClockInKey = !ClassifierDecisionEnabled,
                NormalizeOutputClock = false,
                MinStretchLengthMeters = 1.0,
                MeterMergeGapMaxMeters = 1.0
            }),
            progress);
        if (_qwenVision is not null)
            run.Qwen = new MultiModelQwenSchritt(_qwenVision, run.QwenOutage, _logger);

        progress?.Report(new VideoAnalysisProgress(0, totalFrames,
            $"Multi-Model Pipeline: {totalFrames} Frames, "
            + (run.PipeDiameterMm > 0 ? $"DN{run.PipeDiameterMm}" : "DN unbekannt")));

        // Stufen-Trace pro Lauf (reine Sichtbarkeit, aendert kein Verhalten).
        run.RunId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N")[..6];
        _logger.LogInformation("Multi-Model Pipeline runId={RunId}, Stufen-Trace: {TracePath}",
            run.RunId, PipelineTraceWriteGuard.ResolvePath(_pipelineTraceWriter, run.RunId));

        // Qualifikations-Check einmalig zu Beginn. Ein ausdruecklich unqualifizierter
        // Detektor wird nicht mehr als Filter oder Beweis verwendet. DINO/SAM laufen
        // fuer jeden verwertbaren Frame weiter; der ganze Lauf bleibt review-pflichtig.
        var detectorQualification = await ReadDetectorQualificationAsync(ct).ConfigureAwait(false);
        run.EffectiveDetectorQualified = detectorQualification?.Qualified;
        run.DetectorQualified = run.EffectiveDetectorQualified == true;
        run.DetectorQualificationReason = run.DetectorQualified
            ? null
            : detectorQualification is null
                ? "Qualifikationsstatus fehlt oder konnte nicht gelesen werden"
                : string.IsNullOrWhiteSpace(detectorQualification.Reason)
                    ? "Detektor wurde nicht freigegeben"
                    : detectorQualification.Reason;
        if (!run.DetectorQualified)
        {
            _logger.LogWarning(
                "Multi-Model Pipeline runId={RunId}: aktiver Detektor NICHT qualifiziert ({Reason}) — Ergebnis nicht qualitaetsgesichert.",
                run.RunId,
                run.DetectorQualificationReason);
            progress?.Report(new VideoAnalysisProgress(
                0,
                totalFrames,
                "WARNUNG: YOLO nicht freigegeben – DINO/SAM laufen ohne YOLO-Filter; manuelle Prüfung erforderlich."));
        }

        await RestoreCheckpointAsync(run, ct).ConfigureAwait(false);

        // frameSource-Seam: im Test injizierbar; sonst echter VideoFrameStream.
        var frames = _frameSource is not null
            ? _frameSource(_ffmpegPath, videoPath, FrameStepSeconds, duration, ct)
            : DefaultFrameSource(_ffmpegPath, videoPath, FrameStepSeconds, duration, run.Completeness, ct);

        await foreach (var frame in frames.ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var frameSw = Stopwatch.StartNew();
            run.FrameIndex++;
            if (run.FrameIndex <= run.ResumedFrames) continue;   // Resume: journalierte Frames dekodieren, NICHT erneut inferieren (v1)

            var trace = new PipelineFrameTrace
            {
                RunId = run.RunId,
                TimestampUtc = DateTimeOffset.UtcNow,
                FrameIndex = run.FrameIndex,
                TimeSec = frame.TimestampSeconds,
            };
            var ergebnis = await ProcessFrameAsync(run, frame, trace, frameSw, ct).ConfigureAwait(false);
            if (!await BookFrameResultAsync(run, trace, frame.TimestampSeconds, ergebnis, ct).ConfigureAwait(false))
                break;   // Sidecar-Ausfall: Lauf degraded beenden
        }

        run.Detections.AddRange(run.Deduplicator.Flush());
        run.Completeness.FinishExtraction(run.FrameIndex, totalFrames, run.SidecarOutage);
        if (run.Completeness.CanCompleteJournal(run.SidecarOutage, run.OutageGuard.ErrorSkipCount) && _checkpointJournal is not null)
            await _checkpointJournal.CompleteAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Multi-Model Pipeline complete: {Detections} detections, {Skipped}/{Total} frames skipped, {Duration:F1}s video",
            run.Detections.Count, run.SkippedFrames, run.FrameIndex, duration);

        var summary = run.Telemetry.GetSummary();
        _logger.LogInformation(
            "Telemetry: Wall={WallMs}ms, Extraction Mean={ExtMean:F0}ms P95={ExtP95:F0}ms, YOLO Mean={YoloMean:F0}ms P95={YoloP95:F0}ms, DINO Mean={DinoMean:F0}ms, SAM Mean={SamMean:F0}ms, Qwen Mean={QwenMean:F0}ms",
            summary.WallClockMs, summary.Extraction.MeanMs, summary.Extraction.P95Ms,
            summary.Yolo.MeanMs, summary.Yolo.P95Ms, summary.Dino.MeanMs,
            summary.Sam.MeanMs, summary.Qwen.MeanMs);
        await PipelineTraceWriteGuard
            .WriteSummaryAsync(_pipelineTraceWriter, run.RunId, summary)
            .ConfigureAwait(false);

        var result = BuildResult(run, summary);
        ReportCompletion(progress, totalFrames, run.SkippedFrames, result);
        return result;
    }

    /// <summary>
    /// Bucht den Ausgang eines Bildes: Trace, Zusammenfuehrung (Dedup-Update oder Altern),
    /// Checkpoint und Fehlerzaehlung, je Ausgang in genau der Reihenfolge, die vor der Umordnung
    /// (AP05) inline stand und die der Referenzschnappschuss festhaelt.
    /// false = Lauf wegen Sidecar-Ausfalls abbrechen.
    /// </summary>
    private async Task<bool> BookFrameResultAsync(
        MultiModelLaufZustand run, PipelineFrameTrace trace, double t, MultiModelBildErgebnis ergebnis, CancellationToken ct)
    {
        switch (ergebnis.Ausgang)
        {
            case MultiModelBildAusgang.Uebersprungen:
                await WriteTraceAsync(trace).ConfigureAwait(false);
                run.Detections.AddRange(run.Deduplicator.AdvanceAll());
                await AppendAdvanceCheckpointAsync(run, t, ergebnis.Meter, ct).ConfigureAwait(false);
                return true;

            case MultiModelBildAusgang.OhneBoxUebersprungen:
                // Belegte Reihenfolge dieses Zweigs: Checkpoint vor Trace.
                run.Detections.AddRange(run.Deduplicator.AdvanceAll());
                await AppendAdvanceCheckpointAsync(run, t, ergebnis.Meter, ct).ConfigureAwait(false);
                await WriteTraceAsync(trace).ConfigureAwait(false);
                return true;

            case MultiModelBildAusgang.Grundgeruestbefund:
                run.Detections.AddRange(run.Deduplicator.Update(ergebnis.Befunde, ergebnis.Meter, ergebnis.Evidence,
                    meterSource: ergebnis.MeterSource, isMeterEstimated: ergebnis.IsMeterEstimated));
                trace.ActiveCount = run.Deduplicator.ActiveCount;
                trace.DetectionsTotal = run.Detections.Count;
                await AppendCheckpointAsync(new(CheckpointFrameKind.Update, run.FrameIndex, t, ergebnis.Meter,
                    ergebnis.MeterSource, ergebnis.IsMeterEstimated, ergebnis.Evidence, ergebnis.Befunde), ct).ConfigureAwait(false);
                await WriteTraceAsync(trace).ConfigureAwait(false);
                return true;

            case MultiModelBildAusgang.Befunde:
                // Technischer SAM-Teilverlust oder Qwen-Fehler: genau ein Eintrag in die Skip-Quote.
                if (ergebnis.ErneutNoetig)
                    run.OutageGuard.RegisterFailureSkip();
                run.Detections.AddRange(run.Deduplicator.Update(ergebnis.Befunde, ergebnis.Meter, ergebnis.Evidence,
                    meterSource: ergebnis.MeterSource, isMeterEstimated: ergebnis.IsMeterEstimated));
                trace.ActiveCount = run.Deduplicator.ActiveCount;
                trace.DetectionsTotal = run.Detections.Count;
                await WriteTraceAsync(trace).ConfigureAwait(false);
                await AppendCheckpointAsync(new(ergebnis.ErneutNoetig ? CheckpointFrameKind.RetryRequired : CheckpointFrameKind.Update,
                    run.FrameIndex, t, ergebnis.Meter, ergebnis.MeterSource, ergebnis.IsMeterEstimated, ergebnis.Evidence,
                    ergebnis.Befunde), ct).ConfigureAwait(false);
                run.Progress?.Report(new VideoAnalysisProgress(
                    run.FrameIndex, run.TotalFrames,
                    $"Frame {run.FrameIndex}/{run.TotalFrames} @ {ergebnis.Meter:0.0}m – {ergebnis.Befunde.Count} Befunde (Multi-Model)",
                    FramePreviewPng: ergebnis.FrameBytes,
                    LiveFindings: ergebnis.LiveFindings()));
                return true;

            default:
                // Erneut noetig: Transportfehler zaehlen in die Ausfallserie (Neustart/Abbruch moeglich).
                if (ergebnis.Fehlerart == MultiModelFehlerart.Transport)
                    return !await RecordGeneralModelErrorAsync(run, trace, ergebnis.FehlerCode!, t, ergebnis.Meter, ct)
                        .ConfigureAwait(false);
                // VRAM-Mangel und Modellfehler: nur Skip-Quote, nie Ausfallserie oder Neustart.
                await RecordRetryRequiredFrameAsync(run, trace, t, ergebnis.Meter, ct).ConfigureAwait(false);
                run.OutageGuard.RegisterFailureSkip();
                if (ergebnis.Fehlerart == MultiModelFehlerart.Kapazitaet)
                    run.VramInsufficientMessage ??= ergebnis.KapazitaetMeldung;
                return true;
        }
    }

    /// <summary>
    /// Ein Bild = ein Schritt: cls-Vorfilter, YOLO, DINO, SAM, Quantifizierung, Klassifikator-
    /// Entscheidung und Qwen. Die Modellschritte liegen in eigenen Klassen (AP05b); hier bleiben
    /// ihre Reihenfolge, die Klassifikator-Entscheidung und der Beleg des Bildes.
    /// Liefert nur den Ausgang des Bildes; Trace-Schreiben, Dedup,
    /// Checkpoint und Fehlerzaehlung bucht <see cref="BookFrameResultAsync"/>. Ein Nutzerabbruch
    /// wird als Ausnahme weitergereicht und zaehlt nie als Fehler.
    /// </summary>
    private async Task<MultiModelBildErgebnis> ProcessFrameAsync(
        MultiModelLaufZustand run, FrameData frame, PipelineFrameTrace trace, Stopwatch frameSw, CancellationToken ct)
    {
        var t = frame.TimestampSeconds;

        // Extraction timing is effectively 0 for streaming (already read)
        var extractionMs = frameSw.ElapsedMilliseconds;
        var frameBytes = frame.PngBytes;

        if (frameBytes is null or { Length: 0 })
        {
            run.Telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "empty_frame";
            trace.DropReason = "empty_frame";
            return MultiModelBildErgebnis.Uebersprungen(run.LastMeter);
        }

        double estimatedMeter = EstimateMeter(run, t);
        var bild = new MultiModelBildKontext(t, frameBytes, trace, frameSw, extractionMs, estimatedMeter);
        // Telemetrie-Bypass: Bestandsaufnahme-Sweep, BCD-/BCE-Zone und gesperrter Detektor laufen
        // ohne YOLO-Detect an DINO weiter (Regeln im YOLO-Schritt).
        var yoloUmgehung = MultiModelYoloSchritt.Umgehung.Bestimme(run, t, estimatedMeter, FrameStepSeconds);

        trace.Meter = estimatedMeter;
        trace.YoloBypass = yoloUmgehung.Aktiv;
        if (yoloUmgehung.QualifikationGesperrt)
            MarkTraceDegraded(trace, "detector_unqualified");

        // ── YOLO-cls Vorfilter + Frame-Quality-Gate (CPU-billig, eigene Regeln im Vorfilter) ──
        YoloClassifyResponse? clsResult = null;
        if (UseClsPrefilter)
        {
            var cls = await _clsVorfilter.PruefeAsync(run, bild, ClassifierDecisionEnabled, ct).ConfigureAwait(false);
            if (cls.Abschluss is { } clsAbschluss)
                return clsAbschluss;
            clsResult = cls.Antwort;
        }

        // ── Step 1: YOLO Pre-Screening (Umgehung, Qualifikation, Klassenschwellen, COCO-Warnung im YOLO-Schritt) ──
        var yolo = await _yoloSchritt.PruefeAsync(run, bild, yoloUmgehung, ct).ConfigureAwait(false);
        if (yolo.Abschluss is { } yoloAbschluss)
            return yoloAbschluss;
        var yoloResult = yolo.Antwort!;
        var detectorQualificationBypass = yolo.QualifikationGesperrt;

        // ── Step 2: Grounding DINO Detection (degraded, ohne Box und Grundgeruest im DINO-Schritt) ──
        var dino = await _dinoSchritt.ErkenneAsync(run, bild, ct).ConfigureAwait(false);
        if (dino.Abschluss is { } dinoAbschluss)
            return dinoAbschluss;
        var dinoResult = dino.Antwort!;
        if (dinoResult.Detections.Count == 0)
            return _dinoSchritt.OhneBox(run, bild, clsResult, EstimateMeter(run, t),
                new MultiModelDinoSchritt.GrundgeruestRegel(
                    ClassifierOnlyStructuralEnabled, ClassifierOnlyMinConfidence, EstimatedReachLengthM));

        // ── Step 3+4: SAM-Segmentierung und Quantifizierung (eigene Regeln im SAM-Schritt) ──
        var sam = await _samSchritt.SegmentiereAsync(run, bild, dinoResult, ct).ConfigureAwait(false);
        if (sam.Abschluss is { } samAbschluss)
            return samAbschluss;
        var samResult = sam.Antwort!;
        var samMs = sam.SamMs;
        var frameNeedsRetry = sam.ErneutNoetig;
        var findings = sam.Befunde;
        var proximitySuppressedCount = sam.VorausNichtMetriert;
        var meter = EstimateMeter(run, t);

        // Capture max DINO confidence for EvidenceVector
        var maxDinoConf = dinoResult.Detections.Count > 0
            ? dinoResult.Detections.Max(d => d.Confidence) : 0.0;

        // ── Klassifikator-Entscheidung (Paket 2): fuehrende Code-Quelle vor Qwen ──
        // ResolveFromClassifier (Top-K + Meter + BCD/BCE-Regeln) + Temporal-Voting.
        // Erst ein im Fenster bestaetigter Code ueberschreibt die Label-Heuristik;
        // Qwen darf bestaetigte Codes danach nicht mehr aendern.
        string? classifierCode = null;
        if (ClassifierDecisionEnabled
            && clsResult is { Predictions.Count: > 0 }
            && CanUseClassifierDecision(clsResult))
        {
            var resolved = VsaCodeResolver.ResolveFromClassifier(
                clsResult.Predictions, meter, EstimatedReachLengthM, isBend: clsResult.IsBend);
            var frameDecision = resolved is not null && resolved.Code != "LEER"
                ? resolved.Code
                : null;
            var confirmed = run.CodeVoting.RegisterAndVote(frameDecision, meter);

            trace.ClassifierCode = resolved?.Code;
            trace.ClassifierConfidence = resolved?.Confidence;
            trace.ClassifierSource = resolved?.Source;
            trace.ClassifierModel = ClassifierModelTag(clsResult);
            trace.ClassifierVoteConfirmed = confirmed is not null;

            if (confirmed is not null && findings.Count > 0)
            {
                classifierCode = confirmed;
                for (var i = 0; i < findings.Count; i++)
                    findings[i] = findings[i] with { VsaCodeHint = confirmed };
                _logger.LogDebug(
                    "Frame {Frame}: Klassifikator-Code {Code} bestaetigt ({Source}) → fuehrende Quelle",
                    run.FrameIndex, confirmed, resolved?.Source);
            }
        }

        // Build per-frame EvidenceVector with pipeline signals
        // U7: echte YOLO-Confidence des staerksten Treffers statt binaer 1.0. Auf
        // Telemetrie-Bypass-Frames ("sweep") lief YOLO nie -> null (kein Signal), damit das
        // QualityGate keine erfundene Volltreffer-Confidence bewertet.
        double? yoloConfEvidence = detectorQualificationBypass
            ? null
            : yoloResult.Detections.Count > 0
            ? yoloResult.Detections.Max(d => d.Confidence)
            : string.Equals(yoloResult.FrameClass, "sweep", StringComparison.Ordinal)
                ? null
                : (yoloResult.IsRelevant ? 1.0 : 0.0);
        var frameEvidence = new EvidenceVector(
            YoloConf: yoloConfEvidence,
            DinoConf: maxDinoConf,
            SamMaskStability: null, // populated when SamStabilityCheckEnabled
            QwenVisionConf: null,   // populated after Qwen enrichment
            FrameCount: 1
        );

        // ── Step 5: Qwen VSA-Code enrichment (optional) ──
        long qwenMs = 0;
        var qwenMeterAccepted = false;
        if (run.Qwen is not null && findings.Count > 0)
        {
            var qwenContext = new MultiModelQwenSchritt.QwenFrameContext(meter, run.LastMeter, run.LetzterOsdMeter);
            qwenMs = await run.Qwen.EnrichAsync(
                qwenContext, findings, classifierCode, run.FrameIndex, t, bild.FrameBytes, bild.FrameBase64,
                dinoResult, samResult, yoloResult, run.PipeDiameterMm, run.TotalFrames,
                trace, QwenFrameTimeout, run.Progress, ct).ConfigureAwait(false);
            meter = qwenContext.Meter;
            run.LastMeter = qwenContext.LastMeter;
            qwenMeterAccepted = qwenContext.MeterAccepted;
            if (qwenMeterAccepted && qwenContext.LetzterOsdMeter is { } osdMeter)
                run.UebernimmOsdMeter(osdMeter);
            if (qwenContext.RequiresRetry)
            {
                run.Completeness.RecordQwenFailure();
                frameNeedsRetry = true;
            }
        }

        run.Telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, bild.YoloMs, bild.DinoMs, samMs, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

        var (meterSource, isMeterEstimated) = GetDedupMeterMetadata(qwenMeterAccepted);
        trace.Meter = meter;
        trace.FindingsEndOfFrame = findings.Count;
        trace.CodesAfterQwen = findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint));
        if (trace.DropReason is null)
        {
            if (findings.Count == 0 && proximitySuppressedCount > 0)
                trace.DropReason = "ahead_of_camera";   // erkannt, aber als "voraus" nicht metriert
            else if (findings.Count == 0)
                trace.DropReason = "no_findings";
            else if (trace.CodesAfterQwen == 0)
                trace.DropReason = "all_findings_missing_code";
        }
        return new MultiModelBildErgebnis(MultiModelBildAusgang.Befunde, meter, findings, frameEvidence,
            meterSource, isMeterEstimated, ErneutNoetig: frameNeedsRetry, FrameBytes: bild.FrameBytes);
    }

    /// <summary>
    /// Journaliert einen Frame als RetryRequired: identischer Teilschritt, der in den
    /// Fehlerzweigen von YOLO, DINO und SAM unveraendert wiederholt wurde. Reihenfolge,
    /// Checkpoint-Art und Argumente bleiben exakt wie zuvor inline.
    /// </summary>
    private Task AppendRetryRequiredCheckpointAsync(
        int frameIndex, double t, double estimatedMeter, CancellationToken ct)
        => AppendCheckpointAsync(
            new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()),
            ct);

    /// <summary>Normal weitergeschaltetes Bild ohne Befund journalieren (Checkpoint advance).</summary>
    private Task AppendAdvanceCheckpointAsync(MultiModelLaufZustand run, double t, double meter, CancellationToken ct)
        => AppendCheckpointAsync(
            new(CheckpointFrameKind.Advance, run.FrameIndex, t, meter, null, true, null, Array.Empty<EnhancedFinding>()),
            ct);

    /// <summary>
    /// Trace schreiben, Dedup-Fenster altern lassen (AdvanceAll) und den Frame als
    /// RetryRequired journalieren: identische Dreierfolge, die in allen sechs
    /// Fehlerzweigen von YOLO, DINO und SAM unveraendert wiederholt wurde. Reihenfolge
    /// der drei Schritte, Trace-Inhalt und Checkpoint-Argumente bleiben exakt wie zuvor
    /// inline; nur die Wiederholung entfaellt.
    /// </summary>
    private async Task RecordRetryRequiredFrameAsync(
        MultiModelLaufZustand run, PipelineFrameTrace trace, double t, double estimatedMeter, CancellationToken ct)
    {
        await WriteTraceAsync(trace).ConfigureAwait(false);
        run.Detections.AddRange(run.Deduplicator.AdvanceAll());
        await AppendRetryRequiredCheckpointAsync(run.FrameIndex, t, estimatedMeter, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hält die gemeinsame Nachfolge eines allgemeinen Modellfehlers zusammen:
    /// Trace markieren, RetryRequired erfassen und die gemeinsame Sidecar-Ausfallserie
    /// fortschreiben. Modellbezogenes Logging, Telemetrie und VRAM-Behandlung bleiben
    /// in den jeweiligen Fehlerzweigen.
    /// </summary>
    private async Task<bool> RecordGeneralModelErrorAsync(
        MultiModelLaufZustand run, PipelineFrameTrace trace, string errorCode, double t, double estimatedMeter,
        CancellationToken ct)
    {
        trace.Path = errorCode;
        trace.DropReason = errorCode;
        await RecordRetryRequiredFrameAsync(run, trace, t, estimatedMeter, ct).ConfigureAwait(false);
        // Paket 3/A2: Transportfehler -> Zaehler; am Limit einmalig kontrollierter Neustart
        // statt sofortigem Abbruch (Logik in MultiModelAnalysisService.SidecarRestart.cs).
        return await HandleSidecarTransportErrorAsync(run, ct).ConfigureAwait(false);
    }
}
