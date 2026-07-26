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

    // Temporal-Voting gegen Einzelbild-Ausreisser (Paket 2, Schritt 5)
    private readonly ITemporalCodeVotingService _codeVoting = new TemporalCodeVotingService();

    // Erwartete Eigengewichte fuer die COCO-Fallback-Warnung. Liefert der Sidecar
    // einen anderen Modellnamen (z.B. yolo11m.pt), wird einmal pro Lauf gewarnt.
    private readonly string _expectedYoloModel;

    // Letzter Befund fuer Qwen-Kontext (Frame-uebergreifende Kohärenz)
    private (string Code, string Description, double Meter, double Confidence)? _lastFinding;

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
        var detections = new List<RawVideoDetection>();
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = DedupWindowFrames,
            NormalizeFallbackLabels = true,
            // Klassifikator-Regime: Ganzbild-Code darf nicht ueber Masken-Uhrlagen
            // aufsplitten (Pilot 2026-06-10: 12x BDD statt 1 Befund)
            ClockInKey = !ClassifierDecisionEnabled,
            NormalizeOutputClock = false,
            MinStretchLengthMeters = 1.0,
            MeterMergeGapMaxMeters = 1.0
        });
        int frameIndex = 0;
        int skippedFrames = 0;
        double lastMeter = 0;
        bool yoloFallbackWarned = false;
        _codeVoting.Reset();   // Voting-Fenster gilt pro Video-Lauf
        _lastFinding = null;   // Vorheriger-Befund-Kontext darf nicht ueber Video-Grenzen lecken
        // Einheitlicher Ausfallschutz (befund-2 erweitert): Folge-Frames mit Sidecar-Transportfehler
        // (YOLO/DINO/SAM) -> Abbruch. Qwen (Ollama): eigener Prozess, ab Limit nur Degraded-Notiz.
        const int sidecarOutageLimit = 8;
        var outageGuard = new SidecarOutageGuard(sidecarOutageLimit);
        var qwenOutage = new QwenOutageTracker(sidecarOutageLimit);
        bool sidecarOutage = false;
        string? vramInsufficientMessage = null;   // Paket 2/A4: erste VRAM-Mangel-Meldung des Laufs (Degraded-Grund)
        _sidecarRestartAttemptedThisRun = false;   // Neustart-Budget: einmalig pro Lauf (Paket 3/A2)
        // Pipe diameter: from config override or default 300mm
        int pipeDiameterMm = _config.PipeDiameterMmOverride ?? 300;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames,
            $"Multi-Model Pipeline: {totalFrames} Frames, DN{pipeDiameterMm}"));

        var telemetry = new PipelineTelemetry();

        // Stufen-Trace pro Lauf (reine Sichtbarkeit, aendert kein Verhalten).
        var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N")[..6];
        _logger.LogInformation("Multi-Model Pipeline runId={RunId}, Stufen-Trace: {TracePath}",
            runId, PipelineTraceWriteGuard.ResolvePath(_pipelineTraceWriter, runId));

        // Qualifikations-Check einmalig zu Beginn. Ein ausdruecklich unqualifizierter
        // Detektor wird nicht mehr als Filter oder Beweis verwendet. DINO/SAM laufen
        // fuer jeden verwertbaren Frame weiter; der ganze Lauf bleibt review-pflichtig.
        var detectorQualification = await ReadDetectorQualificationAsync(ct).ConfigureAwait(false);
        bool? effectiveDetectorQualified = detectorQualification?.Qualified;
        var detectorQualified = effectiveDetectorQualified == true;
        var detectorQualificationReason = detectorQualified
            ? null
            : detectorQualification is null
                ? "Qualifikationsstatus fehlt oder konnte nicht gelesen werden"
                : string.IsNullOrWhiteSpace(detectorQualification.Reason)
                    ? "Detektor wurde nicht freigegeben"
                    : detectorQualification.Reason;
        if (!detectorQualified)
        {
            _logger.LogWarning(
                "Multi-Model Pipeline runId={RunId}: aktiver Detektor NICHT qualifiziert ({Reason}) — Ergebnis nicht qualitaetsgesichert.",
                runId,
                detectorQualificationReason);
            progress?.Report(new VideoAnalysisProgress(
                0,
                totalFrames,
                "WARNUNG: YOLO nicht freigegeben – DINO/SAM laufen ohne YOLO-Filter; manuelle Pruefung erforderlich."));
        }

        var resume = await RestoreCheckpointAsync(videoPath, detections, deduplicator, totalFrames, lastMeter, progress, ct).ConfigureAwait(false);
        lastMeter = resume.LastMeter;

        // Paket 3/A2: Transportfehler -> Zaehler; am Limit einmalig kontrollierter
        // Neustart statt sofortigem Abbruch (Logik in MultiModelAnalysisService.SidecarRestart.cs).
        Task<bool> RegisterSidecarTransportErrorAsync() =>
            HandleSidecarTransportErrorAsync(
                outageGuard, () => sidecarOutage = true, progress, frameIndex, totalFrames, ct);

        // frameSource-Seam: im Test injizierbar; sonst echter VideoFrameStream.
        var frames = _frameSource is not null
            ? _frameSource(_ffmpegPath, videoPath, FrameStepSeconds, duration, ct)
            : DefaultFrameSource(_ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in frames.ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var frameSw = Stopwatch.StartNew();
            frameIndex++;
            if (frameIndex <= resume.LastFrameIndex) continue;   // Resume: journalierte Frames dekodieren, NICHT erneut inferieren (v1)
            var t = frame.TimestampSeconds;

            var trace = new PipelineFrameTrace
            {
                RunId = runId,
                TimestampUtc = DateTimeOffset.UtcNow,
                FrameIndex = frameIndex,
                TimeSec = t,
            };

            // Extraction timing is effectively 0 for streaming (already read)
            var extractionMs = frameSw.ElapsedMilliseconds;
            var frameBytes = frame.PngBytes;

            if (frameBytes is null or { Length: 0 })
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "empty_frame";
                trace.DropReason = "empty_frame";
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, lastMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                continue;
            }

            var frameBase64 = Convert.ToBase64String(frameBytes);

            // ── Telemetrie-Bypass: Frames ohne YOLO-Detection an Qwen schicken ──
            // YOLO erkennt nur Schaeden — Bestandsaufnahme (Anschluesse, Boegen,
            // Ablagerungen, Rohranfang/Ende) wird verpasst.
            // Loesung: Jeden N-ten Frame + BCD/BCE-Zonen immer analysieren.
            double estimatedMeter = EstimateMeter(t, duration, ref lastMeter);
            bool isAfterOsd = t > 20.0; // OSD-Einblendung 10-20 Sekunden je nach Operateur
            bool isBcdZone = isAfterOsd && estimatedMeter < 1.5 && frameIndex <= 10;
            bool isBceZone = duration > 10 && t > (duration - FrameStepSeconds * 2);
            // Jeden 3. Frame immer analysieren (Bestandsaufnahme-Sweep)
            bool isPeriodicSweep = isAfterOsd && (frameIndex % 3 == 0);
            bool detectorQualificationBypass = !detectorQualified;
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

                if (!clsResult.Usable)
                {
                    // Frame unbrauchbar (schwarz/ueberbelichtet/strukturlos/unscharf)
                    skippedFrames++;
                    _logger.LogDebug("Frame {Frame}: Quality-Gate '{Reason}' → skip",
                        frameIndex, clsResult.QualityReason);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex}/{totalFrames} – unbrauchbar ({clsResult.QualityReason}) → skip"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0,
                        frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "cls_quality_skip";
                    trace.YoloRelevant = false;
                    trace.DropReason = $"frame_{clsResult.QualityReason}";
                    await WriteTraceAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                    continue;
                }

                var topPred = clsResult.Predictions.Count > 0 ? clsResult.Predictions[0] : null;

                // LEER-Skip nur im Klassifikator-Regime (Paket 2): das promotete
                // 11-Klassen-Modell kennt kein OTHER/NORMAL, sondern LEER.
                if (ClassifierDecisionEnabled
                    && topPred?.ClassName is "LEER" or "leer"
                    && topPred.Confidence > 0.70)
                {
                    skippedFrames++;
                    _codeVoting.RegisterAndVote(null, estimatedMeter);   // Fenster altern lassen
                    _logger.LogDebug("Frame {Frame}: Klassifikator LEER ({Conf:F0}%) → skip",
                        frameIndex, topPred.Confidence * 100);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex}/{totalFrames} – Klassifikator: LEER ({topPred.Confidence:P0}) → skip"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0,
                        frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "cls_leer_skip";
                    trace.YoloRelevant = false;
                    trace.DropReason = "classifier_leer";
                    trace.ClassifierCode = "LEER";
                    trace.ClassifierConfidence = topPred.Confidence;
                    trace.ClassifierModel = ClassifierModelTag(clsResult);
                    await WriteTraceAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                    continue;
                }

                if (topPred?.ClassName is "OTHER" or "other" or "NORMAL" or "normal"
                    && topPred.Confidence > 0.70)
                {
                    // Frame ist normal → ueberspringen (spart DINO/SAM/Qwen).
                    // Auch im Sweep korrekt: Grundgeruest-Elemente (BCD/BCE/BCA/...)
                    // haetten eine eigene cls-Klasse, nicht OTHER/NORMAL.
                    skippedFrames++;
                    _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → skip",
                        frameIndex, topPred.ClassName, topPred.Confidence * 100);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex}/{totalFrames} – cls: {topPred.ClassName} ({topPred.Confidence:P0}) → skip"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0,
                        frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "yolo_cls_skip";
                    trace.YoloRelevant = false;
                    trace.DropReason = "yolo_cls_normal";
                    await WriteTraceAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                    continue;
                }

                if (topPred != null)
                    _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → weiter zur Detektion",
                        frameIndex, topPred.ClassName, topPred.Confidence * 100);
                if (ClassifierDecisionEnabled && !clsResult.ClassifierLoaded)
                {
                    MarkTraceDegraded(trace, "classifier_not_loaded");
                    _logger.LogWarning("Frame {Frame}: YOLO-cls Modell nicht geladen - Klassifikator-Code wird nicht angewendet.",
                        frameIndex);
                }

                if (ClassifierDecisionEnabled && clsResult.BendVetoFailed)
                {
                    MarkTraceDegraded(trace, "bend_veto_failed");
                    _logger.LogWarning("Frame {Frame}: Bogen-Veto fehlgeschlagen - is_bend=false wird nicht fuer Klassifikator-Code vertraut.",
                        frameIndex);
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
                    _logger.LogWarning(ex, "Frame {Frame}: YOLO-cls im Klassifikator-Entscheidungsmodus nicht verfuegbar; falle auf Detektionspfad zurueck", frameIndex);
                else
                    _logger.LogDebug(ex, "Frame {Frame}: YOLO-cls nicht verfuegbar, ueberspringe Vorfilter", frameIndex);
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
                var zone = detectorQualificationBypass ? "YOLO gesperrt – DINO/SAM-Pruefung"
                    : isBcdZone ? "BCD-Zone (Rohranfang)"
                    : isBceZone ? "BCE-Zone (Rohrende)"
                    : "Bestandsaufnahme-Sweep";
                _logger.LogDebug("Frame {Frame}: Telemetrie-Bypass ({Zone}) @ {Meter:F2}m",
                    frameIndex, zone, estimatedMeter);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – {zone} @ {estimatedMeter:F1}m",
                    FramePreviewPng: frameBytes));
            }
            else
            {
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – YOLO Pre-Screening...",
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
                        effectiveDetectorQualified = yoloResult.DetectorQualified;
                        detectorQualified = false;
                        detectorQualificationReason =
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
                            frameIndex,
                            totalFrames,
                            "WARNUNG: YOLO-Freigabe waehrend des Laufs fehlt – DINO/SAM laufen weiter."));
                    }

                    // COCO-Fallback sichtbar machen: laeuft der Sidecar nicht mit den
                    // eigenen Gewichten (yolo26m), ist die Schadenserkennung faktisch
                    // blind — das darf nie wieder still passieren (realer Vorfall 2026-06-09).
                    if (!detectorQualificationBypass
                        && !yoloFallbackWarned
                        && yoloResult.ModelName is { Length: > 0 } yoloModelName
                        && !yoloModelName.Contains(_expectedYoloModel, StringComparison.OrdinalIgnoreCase))
                    {
                        yoloFallbackWarned = true;
                        _logger.LogWarning(
                            "YOLO laeuft mit '{Model}' statt der eigenen Gewichte ({Expected}) – COCO-Fallback, Schadenserkennung stark eingeschraenkt!",
                            yoloModelName, _expectedYoloModel);
                        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                            $"WARNUNG: YOLO-Fallback aktiv ('{yoloModelName}' statt {_expectedYoloModel}) – Schadenserkennung eingeschraenkt!"));
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
                    _logger.LogWarning(ex, "Frame {Frame}: YOLO wegen VRAM-Mangels uebersprungen", frameIndex);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex} – YOLO uebersprungen: {ex.Message}"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, phaseSw.ElapsedMilliseconds, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "yolo_error";
                    trace.DropReason = "vram_insufficient";
                    MarkTraceDegraded(trace, "vram_insufficient");
                    await WriteTraceAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                    outageGuard.RegisterFailureSkip();
                    vramInsufficientMessage ??= ex.Message;
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Frame {Frame}: YOLO detection failed", frameIndex);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex} – YOLO Fehler: {ex.Message}"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, phaseSw.ElapsedMilliseconds, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "yolo_error";
                    trace.DropReason = "yolo_error";
                    await WriteTraceAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                    if (await RegisterSidecarTransportErrorAsync().ConfigureAwait(false)) break;
                    continue;
                }
                yoloMs = phaseSw.ElapsedMilliseconds;
            }

            trace.YoloRelevant = yoloResult.IsRelevant;
            trace.YoloDetectionCount = yoloResult.Detections.Count;

            if (!yoloResult.IsRelevant)
            {
                skippedFrames++;
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – übersprungen (YOLO: irrelevant, {skippedFrames} gesamt)"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "yolo_irrelevant";
                trace.DropReason = "yolo_irrelevant";
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                continue;
            }

            // ── Step 2: Grounding DINO Detection ──
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – Grounding DINO Detection...",
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
                _logger.LogWarning(ex, "Frame {Frame}: DINO wegen VRAM-Mangels uebersprungen", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – DINO uebersprungen: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "dino_error";
                trace.DropReason = "vram_insufficient";
                MarkTraceDegraded(trace, "vram_insufficient");
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                outageGuard.RegisterFailureSkip();
                vramInsufficientMessage ??= ex.Message;
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: DINO detection failed", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – DINO Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "dino_error";
                trace.DropReason = "dino_error";
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                if (await RegisterSidecarTransportErrorAsync().ConfigureAwait(false)) break;
                continue;
            }
            var dinoMs = phaseSw.ElapsedMilliseconds;
            trace.DinoBoxCount = dinoResult.Detections.Count;

            // degraded != sauber: ein Modell-/Inferenzfehler im Sidecar (degraded=true)
            // darf NICHT als "dino_no_boxes" (kein Befund) verbucht werden, sonst sieht
            // ein verstummtes Modell wie ein sauberes Rohr aus. Frame als Review markieren.
            if (dinoResult.Degraded)
            {
                _logger.LogWarning("Frame {Frame}: DINO degraded ({Code}: {Error}) – als Review markiert, NICHT als sauberer Negativbefund.",
                    frameIndex, dinoResult.ErrorCode, dinoResult.Error);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – DINO degraded (Modellfehler) – Review nötig"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "dino_degraded";
                trace.DropReason = "dino_degraded";
                trace.Degraded = true;
                trace.DegradedReason = dinoResult.ErrorCode ?? "dino_degraded";
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                outageGuard.RegisterFailureSkip();   // Modellfehler: nur Skip-Quote, kein Transport-Ausfall
                continue;
            }

            if (dinoResult.Detections.Count == 0)
            {
                // Fix #1: Bevor der Frame verworfen wird — wenn der Klassifikator einen
                // Grundgeruest-Code (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, einen
                // box-losen Befund erzeugen. Rettet Bestandsaufnahme, die DINO nicht boxt.
                var meterNoBox = EstimateMeter(t, duration, ref lastMeter);
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
                        var confirmed = _codeVoting.RegisterAndVote(resolved.Code, meterNoBox);
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

                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: structuralOnly is null));

                if (structuralOnly is not null)
                {
                    trace.Path = "classifier_only_structural";
                    trace.FindingsBuilt = 1;
                    var evidence = new EvidenceVector(
                        YoloConf: clsResult?.Predictions[0].Confidence ?? 0.0, DinoConf: 0.0, FrameCount: 1);
                    var (mSrc, mEst) = GetDedupMeterMetadata(qwenMeterAccepted: false);
                    detections.AddRange(deduplicator.Update(
                        new List<EnhancedFinding> { structuralOnly },
                        meterNoBox,
                        evidence,
                        meterSource: mSrc,
                        isMeterEstimated: mEst));
                    trace.ActiveCount = deduplicator.ActiveCount;
                    trace.DetectionsTotal = detections.Count;
                    await AppendCheckpointAsync(new(CheckpointFrameKind.Update, frameIndex, t, meterNoBox, mSrc, mEst, evidence, new List<EnhancedFinding> { structuralOnly }), ct).ConfigureAwait(false);
                }
                else
                {
                    trace.Path = "dino_no_boxes";
                    trace.DropReason = "dino_no_boxes";
                    detections.AddRange(deduplicator.AdvanceAll());
                    await AppendCheckpointAsync(new(CheckpointFrameKind.Advance, frameIndex, t, meterNoBox, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                }

                await WriteTraceAsync(trace).ConfigureAwait(false);
                continue;
            }

            // ── Step 3: SAM Segmentation ──
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – SAM Segmentation ({dinoResult.Detections.Count} Boxes)...",
                FramePreviewPng: frameBytes));

            var samBoxes = dinoResult.Detections
                .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
                .ToList();

            phaseSw.Restart();
            SamResponse samResult;
            try
            {
                samResult = await _client.SegmentSamAsync(
                    new SamRequest(frameBase64, samBoxes, pipeDiameterMm), ct).ConfigureAwait(false);
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
                _logger.LogWarning(ex, "Frame {Frame}: SAM wegen VRAM-Mangels uebersprungen", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – SAM uebersprungen: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "sam_error";
                trace.DropReason = "vram_insufficient";
                MarkTraceDegraded(trace, "vram_insufficient");
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                outageGuard.RegisterFailureSkip();
                vramInsufficientMessage ??= ex.Message;
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – SAM Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "sam_error";
                trace.DropReason = "sam_error";
                await WriteTraceAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                await AppendCheckpointAsync(new(CheckpointFrameKind.RetryRequired, frameIndex, t, estimatedMeter, null, true, null, Array.Empty<EnhancedFinding>()), ct).ConfigureAwait(false);
                if (await RegisterSidecarTransportErrorAsync().ConfigureAwait(false)) break;
                continue;
            }
            var samMs = phaseSw.ElapsedMilliseconds;
            trace.SamMaskCount = samResult.Masks.Count;

            // SAM-Teilverlust sichtbar machen: degraded=true heisst, Boxen gingen verloren
            // (Predict-Fehler / ausserhalb Bild). Frame wird weiterverarbeitet (Masken existieren),
            // aber als Review markiert, statt den Teilverlust still zu schlucken.
            if (samResult.Degraded)
            {
                _logger.LogWarning("Frame {Frame}: SAM degraded – {Skipped}/{Requested} Boxen verloren (Review).",
                    frameIndex, samResult.SkippedBoxes, samResult.RequestedBoxes);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – SAM degraded ({samResult.SkippedBoxes} Box(en) verloren) – Review nötig"));
                MarkTraceDegraded(trace, $"sam_skipped_{samResult.SkippedBoxes}_of_{samResult.RequestedBoxes}");
            }

            // ── Step 4: Quantification ──
            var quantified = MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm);
            var meter = EstimateMeter(t, duration, ref lastMeter);

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
                    frameIndex, proximitySuppressedCount);

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
                var confirmed = _codeVoting.RegisterAndVote(frameDecision, meter);

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
                        frameIndex, confirmed, resolved?.Source);
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
            if (_qwenVision is not null && findings.Count > 0)
            {
                var qwenContext = new QwenFrameContext(meter, lastMeter);
                qwenMs = await EnrichFindingsWithQwenAsync(
                    qwenContext, findings, classifierCode, frameIndex, t, frameBytes, frameBase64,
                    dinoResult, samResult, yoloResult, pipeDiameterMm, totalFrames,
                    trace, qwenOutage, progress, ct).ConfigureAwait(false);
                meter = qwenContext.Meter;
                lastMeter = qwenContext.LastMeter;
                qwenMeterAccepted = qwenContext.MeterAccepted;
            }

            telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, samMs, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

            var liveFindings = findings.Select(f => new LiveFrameFinding(
                Label: f.Label,
                Severity: f.Severity,
                PositionClock: f.PositionClock,
                ExtentPercent: f.ExtentPercent,
                VsaCodeHint: f.VsaCodeHint,
                HeightMm: f.HeightMm,
                WidthMm: f.WidthMm,
                IntrusionPercent: f.IntrusionPercent,
                CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                DiameterReductionMm: f.DiameterReductionMm
            )).ToList();

            // Update active findings (dedup)
            var (meterSource, isMeterEstimated) = GetDedupMeterMetadata(qwenMeterAccepted);
            detections.AddRange(deduplicator.Update(
                findings,
                meter,
                frameEvidence,
                meterSource: meterSource,
                isMeterEstimated: isMeterEstimated));

            trace.Meter = meter;
            trace.FindingsEndOfFrame = findings.Count;
            trace.CodesAfterQwen = findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint));
            trace.ActiveCount = deduplicator.ActiveCount;
            trace.DetectionsTotal = detections.Count;
            if (trace.DropReason is null)
            {
                if (findings.Count == 0 && proximitySuppressedCount > 0)
                    trace.DropReason = "ahead_of_camera";   // erkannt, aber als "voraus" nicht metriert
                else if (findings.Count == 0)
                    trace.DropReason = "no_findings";
                else if (trace.CodesAfterQwen == 0)
                    trace.DropReason = "all_findings_missing_code";
            }
            await WriteTraceAsync(trace).ConfigureAwait(false);
            await AppendCheckpointAsync(new(CheckpointFrameKind.Update, frameIndex, t, meter, meterSource, isMeterEstimated, frameEvidence, findings), ct).ConfigureAwait(false);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {findings.Count} Befunde (Multi-Model)",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));
        }

        detections.AddRange(deduplicator.Flush());
        if (!sidecarOutage && _checkpointJournal is not null) await _checkpointJournal.CompleteAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Multi-Model Pipeline complete: {Detections} detections, {Skipped}/{Total} frames skipped, {Duration:F1}s video",
            detections.Count, skippedFrames, frameIndex, duration);

        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"Multi-Model fertig – {detections.Count} Schäden, {skippedFrames} Frames übersprungen."));

        var summary = telemetry.GetSummary();
        _logger.LogInformation(
            "Telemetry: Wall={WallMs}ms, Extraction Mean={ExtMean:F0}ms P95={ExtP95:F0}ms, YOLO Mean={YoloMean:F0}ms P95={YoloP95:F0}ms, DINO Mean={DinoMean:F0}ms, SAM Mean={SamMean:F0}ms, Qwen Mean={QwenMean:F0}ms",
            summary.WallClockMs, summary.Extraction.MeanMs, summary.Extraction.P95Ms,
            summary.Yolo.MeanMs, summary.Yolo.P95Ms, summary.Dino.MeanMs,
            summary.Sam.MeanMs, summary.Qwen.MeanMs);
        await PipelineTraceWriteGuard
            .WriteSummaryAsync(_pipelineTraceWriter, runId, summary)
            .ConfigureAwait(false);

        return BuildResult(videoPath, duration, frameIndex, resume.LastFrameIndex, detections, summary,
            sidecarOutage, detectorQualified, effectiveDetectorQualified, detectorQualificationReason,
            outageGuard, qwenOutage, vramInsufficientMessage);
    }

    // ── Conversion helper ──────────────────────────────────────────────

    // ── Private helpers ────────────────────────────────────────────────

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
}
