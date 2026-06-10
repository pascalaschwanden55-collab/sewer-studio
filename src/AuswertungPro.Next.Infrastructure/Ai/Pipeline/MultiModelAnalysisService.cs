using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Orchestrates the Multi-Model pipeline per frame:
/// YOLO (pre-screening) -> DINO (detection) -> SAM (segmentation) -> Quantification -> Qwen VSA-Code.
/// Output is convertible to the existing <see cref="EnhancedFrameAnalysis"/> / <see cref="RawVideoDetection"/>.
/// </summary>
public sealed class MultiModelAnalysisService
{
    private readonly VisionPipelineClient _client;
    private readonly PipelineConfig _config;
    private readonly EnhancedVisionAnalysisService? _qwenVision;
    private readonly ILogger _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public double FrameStepSeconds { get; set; } = 3.0;
    public int DedupWindowFrames { get; set; } = 3;
    // Aeusserer Per-Frame-Qwen-Cap = Standard 120s (#9). Hinweis: der effektiv wirksame Cap
    // ist ohnehin der innere FrameTimeout in EnhancedVisionAnalysisService. Ein separates,
    // groesseres 32B-Budget (z.B. 300s) erfordert, jenen inneren Cap konfigurierbar zu machen
    // (bewusster Folgeschritt). Frueher 300s — faktisch tot, da innen auf 60s gedeckelt.
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>YOLO-cls Vorfilter aktivieren/deaktivieren (Fallback: aus wenn kein Modell).</summary>
    public bool UseClsPrefilter { get; set; } = true;

    /// <summary>
    /// Klassifikator als fuehrende Code-Quelle (Paket 2): ResolveFromClassifier +
    /// Temporal-Voting setzen den VSA-Code, Qwen liefert nur noch OSD/Beschreibung
    /// und fuellt unsichere Faelle. Default AUS, bis der End-to-End-Eval gruen ist
    /// (Env: SEWERSTUDIO_CLASSIFIER_DECISION=1).
    /// </summary>
    public bool ClassifierDecisionEnabled { get; set; } =
        Configuration.AiSettingsFactory.ParseBool(
            Environment.GetEnvironmentVariable("SEWERSTUDIO_CLASSIFIER_DECISION"));

    // Temporal-Voting gegen Einzelbild-Ausreisser (Paket 2, Schritt 5)
    private readonly ITemporalCodeVotingService _codeVoting = new TemporalCodeVotingService();

    // Erwartete Eigengewichte fuer die COCO-Fallback-Warnung. Liefert der Sidecar
    // einen anderen Modellnamen (z.B. yolo11m.pt), wird einmal pro Lauf gewarnt.
    private static readonly string ExpectedYoloModel =
        Environment.GetEnvironmentVariable("SEWERSTUDIO_EXPECTED_YOLO_MODEL")?.Trim() is { Length: > 0 } expected
            ? expected
            : "yolo26m";

    // Letzter Befund fuer Qwen-Kontext (Frame-uebergreifende Kohärenz)
    private (string Code, string Description, double Meter, double Confidence)? _lastFinding;

    // Gecachter minimaler Confidence-Schwellenwert (einmal berechnet statt pro Frame)
    private readonly double _minClassConfidence;

    public MultiModelAnalysisService(
        VisionPipelineClient client,
        PipelineConfig config,
        string ffmpegPath = "ffmpeg",
        EnhancedVisionAnalysisService? qwenVision = null,
        ILogger? logger = null)
    {
        _client = client;
        _config = config;
        _qwenVision = qwenVision;
        _logger = logger ?? NullLogger.Instance;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = DeriveFfprobePath(ffmpegPath);
        _minClassConfidence = config.YoloClassConfidence.Count > 0
            ? config.YoloClassConfidence.Values.Min()
            : config.YoloConfidence;
    }

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
        if (!File.Exists(videoPath))
            return VideoAnalysisResult.Failed($"Video nicht gefunden: {videoPath}");

        progress?.Report(new VideoAnalysisProgress(0, 0, "Multi-Model: Videodauer wird ermittelt..."));

        var duration = await GetVideoDurationAsync(videoPath, ct).ConfigureAwait(false);
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

        // Pipe diameter: from config override or default 300mm
        int pipeDiameterMm = _config.PipeDiameterMmOverride ?? 300;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames,
            $"Multi-Model Pipeline: {totalFrames} Frames, DN{pipeDiameterMm}"));

        var telemetry = new PipelineTelemetry();

        // Stufen-Trace pro Lauf (reine Sichtbarkeit, aendert kein Verhalten).
        var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N")[..6];
        _logger.LogInformation("Multi-Model Pipeline runId={RunId}, Stufen-Trace: {TracePath}",
            runId, PipelineTraceWriter.ResolvePath(runId));

        await using var stream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var frameSw = Stopwatch.StartNew();
            frameIndex++;
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
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
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
            bool telemetryBypass = isBcdZone || isBceZone || isPeriodicSweep;

            trace.Meter = estimatedMeter;
            trace.YoloBypass = telemetryBypass;

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
                    await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
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
                    await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
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
                    await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
                    continue;
                }

                if (topPred != null)
                    _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → weiter zur Detektion",
                        frameIndex, topPred.ClassName, topPred.Confidence * 100);
            }
            catch (Exception ex)
            {
                // cls-Modell nicht verfuegbar → normal weiter (kein harter Fehler)
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
                    FrameClass: "sweep",
                    InferenceTimeMs: 0);
                yoloMs = 0;
                var zone = isBcdZone ? "BCD-Zone (Rohranfang)"
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

                    // COCO-Fallback sichtbar machen: laeuft der Sidecar nicht mit den
                    // eigenen Gewichten (yolo26m), ist die Schadenserkennung faktisch
                    // blind — das darf nie wieder still passieren (realer Vorfall 2026-06-09).
                    if (!yoloFallbackWarned && yoloResult.ModelName is { Length: > 0 } yoloModelName
                        && !yoloModelName.Contains(ExpectedYoloModel, StringComparison.OrdinalIgnoreCase))
                    {
                        yoloFallbackWarned = true;
                        _logger.LogWarning(
                            "YOLO laeuft mit '{Model}' statt der eigenen Gewichte ({Expected}) – COCO-Fallback, Schadenserkennung stark eingeschraenkt!",
                            yoloModelName, ExpectedYoloModel);
                        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                            $"WARNUNG: YOLO-Fallback aktiv ('{yoloModelName}' statt {ExpectedYoloModel}) – Schadenserkennung eingeschraenkt!"));
                    }

                    // Klassenspezifische Filterung: Jede Klasse hat ihren eigenen Schwellenwert
                    if (yoloResult.Detections.Count > 0 && _config.YoloClassConfidence.Count > 0)
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Frame {Frame}: YOLO detection failed", frameIndex);
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex} – YOLO Fehler: {ex.Message}"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, phaseSw.ElapsedMilliseconds, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                    trace.Path = "yolo_error";
                    trace.DropReason = "yolo_error";
                    await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                    detections.AddRange(deduplicator.AdvanceAll());
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
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: DINO detection failed", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – DINO Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "dino_error";
                trace.DropReason = "dino_error";
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
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
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
                continue;
            }

            if (dinoResult.Detections.Count == 0)
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: false));
                trace.Path = "dino_no_boxes";
                trace.DropReason = "dino_no_boxes";
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – SAM Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                trace.Path = "sam_error";
                trace.DropReason = "sam_error";
                await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);
                detections.AddRange(deduplicator.AdvanceAll());
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
                trace.Degraded = true;
                trace.DegradedReason = $"sam_skipped_{samResult.SkippedBoxes}_of_{samResult.RequestedBoxes}";
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

                var bbox = GetNormalizedBbox(seg.Mask, samResult.ImageWidth, samResult.ImageHeight);
                findings.Add(new EnhancedFinding(
                    Label: q.Label,
                    VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                    Severity: EstimateSeverity(q),
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
            if (ClassifierDecisionEnabled && clsResult is { Predictions.Count: > 0 })
            {
                var resolved = VsaCodeResolver.ResolveFromClassifier(
                    clsResult.Predictions, meter, EstimatedReachLengthM);
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
            var frameEvidence = new EvidenceVector(
                YoloConf: yoloResult.IsRelevant ? 1.0 : 0.0,
                DinoConf: maxDinoConf,
                SamMaskStability: null, // populated when SamStabilityCheckEnabled
                QwenVisionConf: null,   // populated after Qwen enrichment
                FrameCount: 1
            );

            // ── Step 5: Qwen VSA-Code enrichment (optional) ──
            long qwenMs = 0;
            if (_qwenVision is not null && findings.Count > 0)
            {
                trace.QwenCalled = true;
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – Qwen VSA-Code-Mapping...",
                    FramePreviewPng: frameBytes));

                phaseSw.Restart();
                try
                {
                    var multiModelContext = new MultiModelFrameResult(
                        TimestampSec: t,
                        Meter: meter,
                        IsRelevant: true,
                        DinoDetections: dinoResult.Detections,
                        SamMasks: samResult.Masks,
                        ImageWidth: samResult.ImageWidth,
                        ImageHeight: samResult.ImageHeight,
                        YoloTimeMs: yoloResult.InferenceTimeMs,
                        DinoTimeMs: dinoResult.InferenceTimeMs,
                        SamTimeMs: samResult.InferenceTimeMs);

                    using var qwenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    qwenCts.CancelAfter(QwenFrameTimeout);
                    // Vorherigen Befund als Kontext uebergeben (nur wenn < 1m entfernt)
                    var prevCtx = _lastFinding is var (pc, pd, pm, pconf) && Math.Abs(meter - pm) < 1.0
                        ? _lastFinding : null;
                    var qwenResult = await _qwenVision.AnalyzeWithContextAsync(
                        frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token,
                        previousFinding: prevCtx).ConfigureAwait(false);

                    trace.QwenImageQuality = qwenResult.ImageQuality;
                    trace.QwenRawFindingCount = qwenResult.Findings.Count;

                    var badQuality = string.Equals(qwenResult.ImageQuality, "schlecht", StringComparison.OrdinalIgnoreCase);

                    // OSD-Meter nur uebernehmen, wenn plausibel (0..500 m) UND nicht aus einem schlechten
                    // Bild — sonst vergiftet ein halluzinierter/fehlgelesener Meter die fortlaufende
                    // Timeline (lastMeter). Bei schlechtem Bild ist auch das OSD-Lesen unzuverlaessig. (Audit R7)
                    if (qwenResult.Meter.HasValue && !badQuality
                        && AuswertungPro.Next.Infrastructure.Ai.MeterPlausibility.IsPlausible(qwenResult.Meter.Value))
                    {
                        meter = qwenResult.Meter.Value;
                        lastMeter = meter;
                    }
                    else if (qwenResult.Meter.HasValue)
                    {
                        _logger.LogDebug("Frame {Frame}: OSD-Meter {Meter} verworfen ({Reason})",
                            frameIndex, qwenResult.Meter.Value, badQuality ? "schlechtes Bild" : "unplausibel");
                    }

                    // ImageQuality-Gate: Bei schlechter Bildqualitaet Findings verwerfen
                    if (badQuality)
                    {
                        _logger.LogDebug("Frame {Frame}: ImageQuality=schlecht, {Count} Findings verworfen",
                            frameIndex, findings.Count);
                        trace.DropReason = "image_quality_bad";
                        findings.Clear();
                    }

                    if (qwenResult.HasFindings)
                    {
                        // Match Qwen findings to our quantified findings by label similarity
                        foreach (var qf in qwenResult.Findings)
                        {
                            var match = findings.FirstOrDefault(f =>
                                f.Label.Equals(qf.Label, StringComparison.OrdinalIgnoreCase) ||
                                qf.Label.Contains(f.Label, StringComparison.OrdinalIgnoreCase) ||
                                f.Label.Contains(qf.Label, StringComparison.OrdinalIgnoreCase));

                            // Klassifikator fuehrt (Paket 2): bestaetigte Codes darf Qwen
                            // nicht ueberschreiben — nur noch leere Hints fuellen.
                            if (match is not null && !string.IsNullOrWhiteSpace(qf.VsaCodeHint)
                                && (classifierCode is null || string.IsNullOrWhiteSpace(match.VsaCodeHint)))
                            {
                                var idx = findings.IndexOf(match);
                                // Replace with enriched finding (keep SAM quantification, add Qwen VSA code)
                                findings[idx] = match with { VsaCodeHint = qf.VsaCodeHint };
                            }
                        }

                        // Letzten Befund merken fuer Qwen-Kontext beim naechsten Frame
                        var topFinding = qwenResult.Findings
                            .Where(f => !string.IsNullOrEmpty(f.VsaCodeHint))
                            .OrderByDescending(f => f.Severity)
                            .FirstOrDefault();
                        if (topFinding != null)
                        {
                            _lastFinding = (
                                topFinding.VsaCodeHint ?? topFinding.Label,
                                topFinding.Label,
                                meter,
                                topFinding.Severity / 5.0); // Severity 1-5 → Confidence 0.2-1.0
                        }

                        _logger.LogDebug("Frame {Frame}: Qwen enriched {Count} findings with VSA codes",
                            frameIndex, qwenResult.Findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint)));
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    trace.DropReason = "qwen_timeout";
                    _logger.LogWarning("Frame {Frame}: Qwen VSA-Code-Mapping timeout ({Timeout}s)",
                        frameIndex, QwenFrameTimeout.TotalSeconds);
                }
                catch (Exception ex)
                {
                    trace.DropReason = "qwen_error";
                    _logger.LogWarning(ex, "Frame {Frame}: Qwen VSA-Code-Mapping fehlgeschlagen", frameIndex);
                }
                qwenMs = phaseSw.ElapsedMilliseconds;
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
            detections.AddRange(deduplicator.Update(
                findings,
                meter,
                frameEvidence,
                meterSource: "LinearEstimate",
                isMeterEstimated: true));

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
            await PipelineTraceWriter.WriteAsync(trace).ConfigureAwait(false);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {findings.Count} Befunde (Multi-Model)",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));
        }

        detections.AddRange(deduplicator.Flush());

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
        await PipelineTraceWriter.WriteSummaryAsync(runId, summary).ConfigureAwait(false);

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary);
    }

    /// <summary>Modell-Tag fuer den Trace: Name + Kurz-Hash aus der Sidecar-Response.</summary>
    private static string? ClassifierModelTag(YoloClassifyResponse? cls)
    {
        if (cls is null || string.IsNullOrEmpty(cls.ModelName))
            return null;
        var sha = cls.ModelSha256;
        return string.IsNullOrEmpty(sha) ? cls.ModelName : $"{cls.ModelName}@{sha[..Math.Min(12, sha.Length)]}";
    }

    // ── Conversion helper ──────────────────────────────────────────────

    /// <summary>
    /// Convert a MultiModelFrameResult to EnhancedFrameAnalysis
    /// (for compatibility with the existing pipeline).
    /// </summary>
    public static EnhancedFrameAnalysis ToEnhancedAnalysis(
        MultiModelFrameResult result,
        int pipeDiameterMm)
    {
        if (!result.IsRelevant)
            return EnhancedFrameAnalysis.Empty();

        var quantified = new List<MaskQuantificationService.QuantifiedMask>();
        foreach (var mask in result.SamMasks)
        {
            quantified.Add(MaskQuantificationService.Quantify(
                mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm));
        }

        var findings = new List<EnhancedFinding>(quantified.Count);
        for (var i = 0; i < quantified.Count; i++)
        {
            var q = quantified[i];
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;

            var bbox = i < result.SamMasks.Count ? GetNormalizedBbox(result.SamMasks[i], result.ImageWidth, result.ImageHeight) : default;
            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: EstimateSeverity(q),
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
                Notes: null
            ));
        }

        return new EnhancedFrameAnalysis(
            Meter: result.Meter,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: pipeDiameterMm,
            Findings: findings,
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null);
    }

    // ── Private helpers ────────────────────────────────────────────────

    private static int EstimateSeverity(MaskQuantificationService.QuantifiedMask q)
    {
        // Heuristic based on physical dimensions
        if (q.CrossSectionReductionPercent is > 50) return 5;
        if (q.CrossSectionReductionPercent is > 25) return 4;
        if (q.ExtentPercent is > 50) return 4;
        if (q.HeightMm is > 50) return 3;
        if (q.ExtentPercent is > 25) return 3;
        if (q.HeightMm is > 10) return 2;
        return 1;
    }

    private static (double? X1, double? Y1, double? X2, double? Y2) GetNormalizedBbox(
        SamMaskResult mask,
        int imageWidth,
        int imageHeight)
    {
        if (mask.Bbox == null || mask.Bbox.Count < 4 || imageWidth <= 0 || imageHeight <= 0)
            return default;

        return (
            Clamp01(mask.Bbox[0] / imageWidth),
            Clamp01(mask.Bbox[1] / imageHeight),
            Clamp01(mask.Bbox[2] / imageWidth),
            Clamp01(mask.Bbox[3] / imageHeight));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
    public double EstimatedReachLengthM { get; set; } = 50.0; // Typisch 15-80m, Fallback 50m

    private double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Lineare Schaetzung basierend auf geschaetzter Haltungslaenge (wird durch Qwen OSD korrigiert)
        var estimated = t / Math.Max(duration, 1.0) * EstimatedReachLengthM;
        lastMeter = Math.Max(lastMeter, estimated);
        return Math.Round(lastMeter, 2);
    }

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;
        return Path.GetFullPath(path);
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var probePath = DeriveFfprobePath(_ffmpegPath);
        var psi = new ProcessStartInfo
        {
            FileName = probePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return 0;
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return dur;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultiModelAnalysis] ffprobe fehlgeschlagen: {ex.Message}");
        }
        return 0;
    }

    private static string DeriveFfprobePath(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) ||
            string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";
        var dir = Path.GetDirectoryName(ffmpegPath);
        var ext = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(dir) ? "ffprobe" + ext : Path.Combine(dir, "ffprobe" + ext);
    }

    /// <summary>
    /// Normalisiert Clock-Positionen auf ganzzahlige Stunden.
    /// "3:00" → "3", "12" → "12", "Scheitel" → "12", "Sohle" → "6", "rechts" → "3", "links" → "9".
    /// </summary>
    private static string? NormalizeClockPosition(string? clock)
    {
        var normalized = NormalizeClock(clock);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized;
    }

    private static string? NormalizeClock(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim().ToLowerInvariant();
        if (text.Contains("oben") || text.Contains("scheitel") || text.Contains("krone"))
            return "12:00";
        if (text.Contains("unten") || text.Contains("sohle"))
            return "6:00";
        if (text.Contains("rechts")) return "3:00";
        if (text.Contains("links")) return "9:00";

        var match = Regex.Match(raw, @"\b(1[0-2]|0?[1-9])\b");
        if (match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            && hour >= 1
            && hour <= 12)
        {
            return $"{hour}:00";
        }

        return raw.Trim();
    }

}
