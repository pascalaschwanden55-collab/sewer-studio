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
using AuswertungPro.Next.Domain.VsaCatalog;
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
    /// Entscheidung und Qwen. Liefert nur den Ausgang des Bildes; Trace-Schreiben, Dedup,
    /// Checkpoint und Fehlerzaehlung bucht <see cref="BookFrameResultAsync"/>. Ein Nutzerabbruch
    /// wird als Ausnahme weitergereicht und zaehlt nie als Fehler.
    /// </summary>
    private async Task<MultiModelBildErgebnis> ProcessFrameAsync(
        MultiModelLaufZustand run, FrameData frame, PipelineFrameTrace trace, Stopwatch frameSw, CancellationToken ct)
    {
        var progress = run.Progress;
        var totalFrames = run.TotalFrames;
        var telemetry = run.Telemetry;
        var duration = run.Duration;
        var pipeDiameterMm = run.PipeDiameterMm;
        var t = frame.TimestampSeconds;

        // Extraction timing is effectively 0 for streaming (already read)
        var extractionMs = frameSw.ElapsedMilliseconds;
        var frameBytes = frame.PngBytes;

        if (frameBytes is null or { Length: 0 })
        {
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "empty_frame";
            trace.DropReason = "empty_frame";
            return MultiModelBildErgebnis.Uebersprungen(run.LastMeter);
        }

        var frameBase64 = Convert.ToBase64String(frameBytes);

        // ── Telemetrie-Bypass: Frames ohne YOLO-Detection an Qwen schicken ──
        // YOLO erkennt nur Schaeden — Bestandsaufnahme (Anschluesse, Boegen,
        // Ablagerungen, Rohranfang/Ende) wird verpasst.
        // Loesung: Jeden N-ten Frame + BCD/BCE-Zonen immer analysieren.
        double estimatedMeter = EstimateMeter(run, t);
        bool isAfterOsd = t > 20.0; // OSD-Einblendung 10-20 Sekunden je nach Operateur
        bool isBcdZone = isAfterOsd && estimatedMeter < 1.5 && run.FrameIndex <= 10;
        bool isBceZone = duration > 10 && t > (duration - FrameStepSeconds * 2);
        // Jeden 3. Frame immer analysieren (Bestandsaufnahme-Sweep)
        bool isPeriodicSweep = isAfterOsd && (run.FrameIndex % 3 == 0);
        bool detectorQualificationBypass = !run.DetectorQualified;
        bool telemetryBypass =
            detectorQualificationBypass || isBcdZone || isBceZone || isPeriodicSweep;

        trace.Meter = estimatedMeter;
        trace.YoloBypass = telemetryBypass;
        if (detectorQualificationBypass)
            MarkTraceDegraded(trace, "detector_unqualified");

        // ── YOLO-cls Vorfilter + Frame-Quality-Gate (CPU-billig) ──
        // Gilt bewusst AUCH fuer Sweep-/BCD-/BCE-Frames: vorher konnten schwarze
        // oder strukturlose Bypass-Frames ungefiltert bis zu Qwen (120s-Cap) laufen.
        var phaseSw = Stopwatch.StartNew();
        YoloClassifyResponse? clsResult = null;
        if (UseClsPrefilter) try
        {
            clsResult = await _client.ClassifyYoloAsync(
                new YoloClassifyRequest(frameBase64, 3), ct).ConfigureAwait(false);

            if (ClsPrefilterRule.Decide(clsResult, ClassifierDecisionEnabled) is { } skip)
            {
                run.SkippedFrames++;
                if (skip.EmptyPrediction is { } empty)
                {
                    run.CodeVoting.RegisterAndVote(null, estimatedMeter);   // Fenster altern lassen
                    trace.ClassifierCode = "LEER";
                    trace.ClassifierConfidence = empty.Confidence;
                    trace.ClassifierModel = ClassifierModelTag(clsResult);
                }
                _logger.LogDebug("Frame {Frame}: cls-Vorfilter {Reason} → skip", run.FrameIndex, skip.ProgressText);
                progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                    $"Frame {run.FrameIndex}/{totalFrames} – {skip.ProgressText} → skip"));
                telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, 0, 0, 0, 0,
                    frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = skip.TracePath;
                trace.YoloRelevant = false;
                trace.DropReason = skip.DropReason;
                return MultiModelBildErgebnis.Uebersprungen(estimatedMeter);
            }

            var topPred = clsResult.Predictions.Count > 0 ? clsResult.Predictions[0] : null;

            if (topPred != null)
                _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → weiter zur Detektion",
                    run.FrameIndex, topPred.ClassName, topPred.Confidence * 100);
            if (ClassifierDecisionEnabled && !clsResult.ClassifierLoaded)
            {
                MarkTraceDegraded(trace, "classifier_not_loaded");
                _logger.LogWarning("Frame {Frame}: YOLO-cls Modell nicht geladen - Klassifikator-Code wird nicht angewendet.",
                    run.FrameIndex);
            }

            if (ClassifierDecisionEnabled && clsResult.BendVetoFailed)
            {
                MarkTraceDegraded(trace, "bend_veto_failed");
                _logger.LogWarning("Frame {Frame}: Bogen-Veto fehlgeschlagen - is_bend=false wird nicht fuer Klassifikator-Code vertraut.",
                    run.FrameIndex);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Fehler zaehlen.
            throw;
        }
        catch (Exception ex)
        {
            // cls-Modell nicht verfuegbar → normal weiter (kein harter Fehler)
            if (ClassifierDecisionEnabled)
                _logger.LogWarning(ex, "Frame {Frame}: YOLO-cls im Klassifikator-Entscheidungsmodus nicht verfuegbar; falle auf Detektionspfad zurueck", run.FrameIndex);
            else
                _logger.LogDebug(ex, "Frame {Frame}: YOLO-cls nicht verfuegbar, ueberspringe Vorfilter", run.FrameIndex);
        }

        // ── Step 1: YOLO Pre-Screening ──
        phaseSw.Restart();
        YoloResponse yoloResult;
        long yoloMs;

        if (telemetryBypass)
        {
            // YOLO-Detect ueberspringen — Frame direkt an DINO/Qwen weiterleiten.
            // frame_class ehrlich als "sweep" markieren: BCD/BCE sind hier nur
            // Zonen-Heuristiken, keine Detektionen.
            yoloResult = new YoloResponse(
                IsRelevant: true,
                Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: detectorQualificationBypass ? "detector_unqualified" : "sweep",
                InferenceTimeMs: 0);
            yoloMs = 0;
            var zone = detectorQualificationBypass ? "YOLO gesperrt – DINO/SAM-Prüfung"
                : isBcdZone ? "BCD-Zone (Rohranfang)"
                : isBceZone ? "BCE-Zone (Rohrende)"
                : "Bestandsaufnahme-Sweep";
            _logger.LogDebug("Frame {Frame}: Telemetrie-Bypass ({Zone}) @ {Meter:F2}m",
                run.FrameIndex, zone, estimatedMeter);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – {zone} @ {estimatedMeter:F1}m",
                FramePreviewPng: frameBytes));
        }
        else
        {
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – YOLO Pre-Screening...",
                FramePreviewPng: frameBytes));

            try
            {
                // Niedrigsten klassenspezifischen Threshold senden (mehr Kandidaten),
                // dann in C# pro Klasse nachfiltern
                double minConf = _minClassConfidence;
                yoloResult = await _client.DetectYoloAsync(
                    new YoloRequest(frameBase64, minConf), ct).ConfigureAwait(false);

                // Die Qualifikation kann sich zwischen /health und Inferenz aendern.
                // Auch die konkrete Antwort muss deshalb ein ausdrueckliches true tragen.
                if (yoloResult.DetectorQualified != true)
                {
                    run.EffectiveDetectorQualified = yoloResult.DetectorQualified;
                    run.DetectorQualified = false;
                    run.DetectorQualificationReason =
                        yoloResult.DetectorQualificationReason
                        ?? "YOLO-Antwort ohne positive Detektorqualifikation";
                    detectorQualificationBypass = true;
                    trace.YoloBypass = true;
                    MarkTraceDegraded(trace, "detector_unqualified_response");
                    yoloResult = yoloResult with
                    {
                        IsRelevant = true,
                        Detections = Array.Empty<YoloDetectionDto>(),
                        FrameClass = "detector_unqualified",
                    };
                    progress?.Report(new VideoAnalysisProgress(
                        run.FrameIndex,
                        totalFrames,
                        "WARNUNG: YOLO-Freigabe während des Laufs fehlt – DINO/SAM laufen weiter."));
                }

                // COCO-Fallback sichtbar machen: laeuft der Sidecar nicht mit den
                // eigenen Gewichten (yolo26m), ist die Schadenserkennung faktisch
                // blind — das darf nie wieder still passieren (realer Vorfall 2026-06-09).
                if (!detectorQualificationBypass
                    && !run.YoloFallbackWarned
                    && yoloResult.ModelName is { Length: > 0 } yoloModelName
                    && !yoloModelName.Contains(_expectedYoloModel, StringComparison.OrdinalIgnoreCase))
                {
                    run.YoloFallbackWarned = true;
                    _logger.LogWarning(
                        "YOLO laeuft mit '{Model}' statt der eigenen Gewichte ({Expected}) – COCO-Fallback, Schadenserkennung stark eingeschraenkt!",
                        yoloModelName, _expectedYoloModel);
                    progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                        $"WARNUNG: YOLO-Fallback aktiv ('{yoloModelName}' statt {_expectedYoloModel}) – Schadenserkennung eingeschränkt!"));
                }

                // Klassenspezifische Filterung: Jede Klasse hat ihren eigenen Schwellenwert
                if (!detectorQualificationBypass
                    && yoloResult.Detections.Count > 0
                    && _config.YoloClassConfidence.Count > 0)
                {
                    var filtered = yoloResult.Detections
                        .Where(d =>
                        {
                            // VSA-Hauptcode aus YOLO-Klassenname ableiten ("crack" → BAB,
                            // legacy "BAB_crack" → BAB); ohne Zuordnung gilt die Default-Schwelle
                            var baseCode = YoloClassVsaMapper.ToVsaMainCode(d.ClassName);
                            var threshold = baseCode is not null
                                ? _config.YoloClassConfidence.GetValueOrDefault(baseCode, _config.YoloConfidence)
                                : _config.YoloConfidence;
                            return d.Confidence >= threshold;
                        })
                        .ToList();
                    yoloResult = yoloResult with
                    {
                        Detections = filtered,
                        IsRelevant = filtered.Count > 0
                    };
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Nutzerabbruch: sofort weiterwerfen, nie als Sidecar-Ausfall zaehlen.
                throw;
            }
            catch (SidecarInsufficientVramException ex)
            {
                // Paket 2/A4: VRAM-Mangel ist ein Kapazitaetsfehler, KEIN Transport-Ausfall:
                // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
                // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
                _logger.LogWarning(ex, "Frame {Frame}: YOLO wegen VRAM-Mangels uebersprungen", run.FrameIndex);
                progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                    $"Frame {run.FrameIndex} – YOLO übersprungen: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, phaseSw.ElapsedMilliseconds, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "yolo_error";
                trace.DropReason = "vram_insufficient";
                MarkTraceDegraded(trace, "vram_insufficient");
                return MultiModelBildErgebnis.VramMangel(ex.Message, estimatedMeter);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: YOLO detection failed", run.FrameIndex);
                progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                    $"Frame {run.FrameIndex} – YOLO Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, phaseSw.ElapsedMilliseconds, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                return MultiModelBildErgebnis.Transportfehler("yolo_error", estimatedMeter);
            }
            yoloMs = phaseSw.ElapsedMilliseconds;
        }

        trace.YoloRelevant = yoloResult.IsRelevant;
        trace.YoloDetectionCount = yoloResult.Detections.Count;

        if (!yoloResult.IsRelevant)
        {
            run.SkippedFrames++;
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – übersprungen (YOLO: irrelevant, {run.SkippedFrames} gesamt)"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "yolo_irrelevant";
            trace.DropReason = "yolo_irrelevant";
            return MultiModelBildErgebnis.Uebersprungen(estimatedMeter);
        }

        // ── Step 2: Grounding DINO Detection ──
        progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
            $"Frame {run.FrameIndex}/{totalFrames} – Grounding DINO Detection...",
            FramePreviewPng: frameBytes));

        phaseSw.Restart();
        DinoResponse dinoResult;
        try
        {
            dinoResult = await _client.DetectDinoAsync(
                new DinoRequest(
                    frameBase64,
                    null, // use default labels from sidecar config
                    _config.DinoBoxThreshold,
                    _config.DinoTextThreshold), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Sidecar-Ausfall zaehlen.
            throw;
        }
        catch (SidecarInsufficientVramException ex)
        {
            // Paket 2/A4: VRAM-Mangel = Kapazitaetsfehler, KEIN Transport-Ausfall:
            // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
            // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
            _logger.LogWarning(ex, "Frame {Frame}: DINO wegen VRAM-Mangels uebersprungen", run.FrameIndex);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex} – DINO übersprungen: {ex.Message}"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "dino_error";
            trace.DropReason = "vram_insufficient";
            MarkTraceDegraded(trace, "vram_insufficient");
            return MultiModelBildErgebnis.VramMangel(ex.Message, estimatedMeter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Frame {Frame}: DINO detection failed", run.FrameIndex);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex} – DINO Fehler: {ex.Message}"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            return MultiModelBildErgebnis.Transportfehler("dino_error", estimatedMeter);
        }
        var dinoMs = phaseSw.ElapsedMilliseconds;
        trace.DinoBoxCount = dinoResult.Detections.Count;

        // degraded != sauber: ein Modell-/Inferenzfehler im Sidecar (degraded=true)
        // darf NICHT als "dino_no_boxes" (kein Befund) verbucht werden, sonst sieht
        // ein verstummtes Modell wie ein sauberes Rohr aus. Frame als Review markieren.
        if (dinoResult.Degraded)
        {
            _logger.LogWarning("Frame {Frame}: DINO degraded ({Code}: {Error}) – als Review markiert, NICHT als sauberer Negativbefund.",
                run.FrameIndex, dinoResult.ErrorCode, dinoResult.Error);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex} – DINO degraded (Modellfehler) – Review nötig"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "dino_degraded";
            trace.DropReason = "dino_degraded";
            trace.Degraded = true;
            trace.DegradedReason = dinoResult.ErrorCode ?? "dino_degraded";
            return MultiModelBildErgebnis.Modellfehler(estimatedMeter);   // Modellfehler: nur Skip-Quote, kein Transport-Ausfall
        }

        if (dinoResult.Detections.Count == 0)
        {
            // Fix #1: Bevor der Frame verworfen wird — wenn der Klassifikator einen
            // Grundgeruest-Code (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, einen
            // box-losen Befund erzeugen. Rettet Bestandsaufnahme, die DINO nicht boxt.
            var meterNoBox = EstimateMeter(run, t);
            EnhancedFinding? structuralOnly = null;
            if (ClassifierOnlyStructuralEnabled
                && clsResult is { Predictions.Count: > 0 }
                && CanUseClassifierDecision(clsResult))
            {
                var resolved = ClassifierOnlyStructuralPolicy.TryResolve(
                    clsResult.Predictions, meterNoBox, EstimatedReachLengthM,
                    isBend: clsResult.IsBend, minConfidence: ClassifierOnlyMinConfidence);
                if (resolved is not null)
                {
                    var confirmed = run.CodeVoting.RegisterAndVote(resolved.Code, meterNoBox);
                    if (confirmed is not null)
                    {
                        structuralOnly = new EnhancedFinding(
                            Label: VsaCodeTree.LookupLabel(confirmed) ?? confirmed,
                            VsaCodeHint: confirmed,
                            Severity: 1,
                            PositionClock: null,
                            ExtentPercent: null, HeightMm: null, WidthMm: null,
                            IntrusionPercent: null, CrossSectionReductionPercent: null,
                            DiameterReductionMm: null,
                            BboxX1: null, BboxY1: null, BboxX2: null, BboxY2: null,
                            Notes: $"classifier-only (DINO 0 Boxen), conf={resolved.Confidence:F2}, {resolved.Source}");
                        trace.ClassifierCode = confirmed;
                        trace.ClassifierConfidence = resolved.Confidence;
                        trace.ClassifierModel = ClassifierModelTag(clsResult);
                        trace.ClassifierVoteConfirmed = true;
                    }
                }
            }

            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: structuralOnly is null));

            if (structuralOnly is not null)
            {
                trace.Path = "classifier_only_structural";
                trace.FindingsBuilt = 1;
                var evidence = new EvidenceVector(
                    YoloConf: clsResult?.Predictions[0].Confidence ?? 0.0, DinoConf: 0.0, FrameCount: 1);
                var (mSrc, mEst) = GetDedupMeterMetadata(qwenMeterAccepted: false);
                return new MultiModelBildErgebnis(MultiModelBildAusgang.Grundgeruestbefund, meterNoBox,
                    new List<EnhancedFinding> { structuralOnly }, evidence, mSrc, mEst);
            }

            trace.Path = "dino_no_boxes";
            trace.DropReason = "dino_no_boxes";
            return MultiModelBildErgebnis.OhneBox(meterNoBox);
        }

        // ── Step 3: SAM Segmentation ──
        progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
            $"Frame {run.FrameIndex}/{totalFrames} – SAM Segmentation ({dinoResult.Detections.Count} Boxes)...",
            FramePreviewPng: frameBytes));

        var samBoxes = dinoResult.Detections
            .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
            .ToList();

        phaseSw.Restart();
        SamResponse samResult;
        try
        {
            samResult = await _client.SegmentSamAsync(
                new SamRequest(frameBase64, samBoxes, pipeDiameterMm > 0 ? pipeDiameterMm : null), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Sidecar-Ausfall zaehlen.
            throw;
        }
        catch (SidecarInsufficientVramException ex)
        {
            // Paket 2/A4: VRAM-Mangel = Kapazitaetsfehler, KEIN Transport-Ausfall:
            // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
            // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
            _logger.LogWarning(ex, "Frame {Frame}: SAM wegen VRAM-Mangels uebersprungen", run.FrameIndex);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex} – SAM übersprungen: {ex.Message}"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            trace.Path = "sam_error";
            trace.DropReason = "vram_insufficient";
            MarkTraceDegraded(trace, "vram_insufficient");
            return MultiModelBildErgebnis.VramMangel(ex.Message, estimatedMeter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed", run.FrameIndex);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex} – SAM Fehler: {ex.Message}"));
            telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
            return MultiModelBildErgebnis.Transportfehler("sam_error", estimatedMeter);
        }
        var samMs = phaseSw.ElapsedMilliseconds;
        trace.SamMaskCount = samResult.Masks.Count;

        var frameNeedsRetry = RecordSamCompletion(run.Completeness, samResult, samBoxes.Count,
            run.FrameIndex, totalFrames, trace, progress);

        // ── Step 4: Quantification ──
        var quantified = MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm);
        var meter = EstimateMeter(run, t);

        // Capture max DINO confidence for EvidenceVector
        var maxDinoConf = dinoResult.Detections.Count > 0
            ? dinoResult.Detections.Max(d => d.Confidence) : 0.0;

        // Build findings via SegmentedFinding + Naehe-Gate.
        // Ohne Kalibrierung im Batch: Fluchtpunkt = Bildmitte, Rohrradius-Fallback 0.5.
        var segmented = SegmentedFindingBuilder.Build(
            samResult, dinoResult.Detections, quantified,
            vanishX: 0.5, vanishY: 0.5, pipeRadiusNorm: 0.5,
            AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);

        int proximitySuppressedCount = 0;
        var findings = new List<EnhancedFinding>(segmented.Count);
        foreach (var seg in segmented)
        {
            var q = seg.Quant;
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;
            if (!seg.Proximity.IsCodierbar)
            {
                proximitySuppressedCount++;   // ahead_of_camera: erkannt, aber nicht metriert
                continue;
            }

            var bbox = MultiModelFrameAnalysisMapper.GetNormalizedBbox(
                seg.Mask,
                samResult.ImageWidth,
                samResult.ImageHeight);
            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: QuantificationSeverityPolicy.Estimate(
                    q.CrossSectionReductionPercent,
                    q.IntrusionPercent,
                    q.HeightMm,
                    q.ExtentPercent),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: bbox.X1,
                BboxY1: bbox.Y1,
                BboxX2: bbox.X2,
                BboxY2: bbox.Y2,
                Notes: $"DINO conf={(seg.Dino?.Confidence ?? q.Confidence):F2}"
            ));
        }
        if (proximitySuppressedCount > 0)
            _logger.LogDebug("Frame {Frame}: {Count} Befund(e) als 'ahead_of_camera' nicht metriert.",
                run.FrameIndex, proximitySuppressedCount);

        trace.FindingsBuilt = findings.Count;
        trace.CodesFromLabel = findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint));

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
            var qwenContext = new MultiModelQwenSchritt.QwenFrameContext(meter, run.LastMeter);
            qwenMs = await run.Qwen.EnrichAsync(
                qwenContext, findings, classifierCode, run.FrameIndex, t, frameBytes, frameBase64,
                dinoResult, samResult, yoloResult, pipeDiameterMm, totalFrames,
                trace, QwenFrameTimeout, progress, ct).ConfigureAwait(false);
            meter = qwenContext.Meter;
            run.LastMeter = qwenContext.LastMeter;
            qwenMeterAccepted = qwenContext.MeterAccepted;
            if (qwenContext.RequiresRetry)
            {
                run.Completeness.RecordQwenFailure();
                frameNeedsRetry = true;
            }
        }

        telemetry.RecordFrame(new FrameTiming(run.FrameIndex, t, extractionMs, yoloMs, dinoMs, samMs, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

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
            meterSource, isMeterEstimated, ErneutNoetig: frameNeedsRetry, FrameBytes: frameBytes);
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
