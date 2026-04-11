using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

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

    /// <summary>
    /// Temporale Aggregation: Verdichtet hunderte YOLO-Einzeldetektionen zu ~20 Schadensereignissen.
    /// Qwen wird nur fuer Peak-Frames aufgerufen statt fuer jeden Frame.
    /// </summary>
    /// <summary>Erzeugt pro Analyse-Aufruf einen frischen Aggregator (kein Zustand zwischen Videos).</summary>
    private static DetectionAggregator CreateAggregator() => new(
        minConsecutiveFrames: 3,
        minConfidence: 0.4,
        meterMergeRadius: 1.5,
        maxGapFrames: 5);

    public double FrameStepSeconds { get; set; } = 1.5;
    public int DedupWindowFrames { get; set; } = 3;
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Maximale Zeit pro Frame (YOLO+DINO+SAM+Qwen zusammen). Ueberschrittene Frames werden uebersprungen.</summary>
    public TimeSpan PerFrameTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>YOLO-cls Vorfilter aktivieren/deaktivieren (Fallback: aus wenn kein Modell).</summary>
    public bool UseClsPrefilter { get; set; } = true;

    /// <summary>
    /// Konfidenzschwelle fuer YOLO-cls Skip bei "OTHER/NORMAL".
    /// Hoeherer Wert = weniger aggressive Skips (bessere Recall, mehr Rechenlast).
    /// </summary>
    public double ClsSkipConfidence { get; set; } = 0.90;

    /// <summary>
    /// Fuehrt trotz YOLO-"irrelevant" periodisch DINO aus, um YOLO-Misses abzufangen.
    /// </summary>
    public bool EnableDinoFallbackOnIrrelevantFrames { get; set; } = true;

    /// <summary>
    /// Jeder N-te YOLO-irrelevante Frame wird dennoch an DINO weitergeleitet.
    /// </summary>
    public int DinoFallbackEveryNIrrelevantFrames { get; set; } = 2;

    /// <summary>
    /// Startzeit fuer Recall-Fallbacks nach der OSD-Phase.
    /// </summary>
    public double DinoFallbackStartSeconds { get; set; } = 20.0;

    // Letzter Befund fuer Qwen-Kontext (Frame-uebergreifende Kohärenz)
    // Lock schuetzt vor Race Condition bei parallelen async Frames
    private readonly object _lastFindingLock = new();
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
        var active = new Dictionary<string, ActiveFindingState>(StringComparer.OrdinalIgnoreCase);
        int frameIndex = 0;
        int skippedFrames = 0;
        int irrelevantFrames = 0;
        double lastMeter = 0;

        // Aggregierte Schadensereignisse: YOLO-Detektionen werden temporaer verdichtet,
        // sodass Qwen nur fuer Peak-Frames aufgerufen wird (~20 statt hunderte).
        var aggregator = CreateAggregator();
        var aggregatedEvents = new List<DetectionEvent>();
        // Peak-Frame-Cache: Speichert die Frame-Bytes des aktuell hoechsten Confidence-Frames
        // pro aktiver Detektion. Key = "classId_meterStart", Value = PNG-Bytes.
        var peakFrameCache = new Dictionary<string, byte[]>();

        // Pipe diameter: from config override or default 300mm
        int pipeDiameterMm = _config.PipeDiameterMmOverride ?? 300;

        // Material-Voting: Qwen erkennt Material visuell/OSD, Mehrheitsentscheid
        var materialVotes = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        // Material-Wechsel-Tracking (AED): letztes erkanntes Material + Meter
        string? lastDetectedMaterial = null;
        double lastMaterialChangeMeter = -10.0; // Mindestabstand zwischen Wechseln

        progress?.Report(new VideoAnalysisProgress(0, totalFrames,
            $"Multi-Model Pipeline: {totalFrames} Frames, DN{pipeDiameterMm}"));

        var telemetry = new PipelineTelemetry();

        await using var stream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            // Per-Frame-Timeout: Jedes Frame bekommt 45s. Haengende Frames werden
            // uebersprungen statt den gesamten Blindtest abzubrechen.
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(PerFrameTimeout);

            var frameSw = Stopwatch.StartNew();
            frameIndex++;
            var t = frame.TimestampSeconds;

            // Extraction timing is effectively 0 for streaming (already read)
            var extractionMs = frameSw.ElapsedMilliseconds;
            var frameBytes = frame.PngBytes;

            if (frameBytes is null or { Length: 0 })
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            var frameBase64 = Convert.ToBase64String(frameBytes);

            try // Per-Frame-Timeout: Haengende Frames uebersprungen
            {

            // ── Telemetrie-Bypass: Frames ohne YOLO-Detection an Qwen schicken ──
            // YOLO erkennt nur Schaeden — Bestandsaufnahme (Anschluesse, Boegen,
            // Ablagerungen, Rohranfang/Ende) wird verpasst.
            // Loesung: Jeden N-ten Frame + BCD/BCE-Zonen immer analysieren.
            double estimatedMeter = EstimateMeter(t, duration, ref lastMeter);
            bool isAfterOsd = t > DinoFallbackStartSeconds; // OSD-Einblendung 10-20 Sekunden je nach Operateur
            bool isBcdZone = isAfterOsd && estimatedMeter < 1.5 && frameIndex <= 10;
            bool isBceZone = duration > 10 && t > (duration - FrameStepSeconds * 2);
            // Jeden 2. Frame immer analysieren (Recall-optimierter Bestandsaufnahme-Sweep)
            bool isPeriodicSweep = isAfterOsd && (frameIndex % 2 == 0);
            bool telemetryBypass = isBcdZone || isBceZone || isPeriodicSweep;

            // ── Step 1: YOLO Pre-Screening ──
            var phaseSw = Stopwatch.StartNew();
            YoloResponse yoloResult;
            long yoloMs;

            if (telemetryBypass)
            {
                // YOLO ueberspringen — Frame direkt an DINO/Qwen weiterleiten
                yoloResult = new YoloResponse(
                    IsRelevant: true,
                    Detections: Array.Empty<YoloDetectionDto>(),
                    FrameClass: isBcdZone ? "BCD" : "BCE",
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
                // ── YOLO-cls Vorfilter: 90% der normalen Frames ueberspringen ──
                if (UseClsPrefilter) try
                {
                    var clsResult = await _client.ClassifyYoloAsync(
                        new YoloClassifyRequest(frameBase64, 3), ct).ConfigureAwait(false);
                    var topPred = clsResult.Predictions.Count > 0 ? clsResult.Predictions[0] : null;

                    if (topPred?.ClassName is "OTHER" or "other" or "NORMAL" or "normal"
                        && topPred.Confidence >= ClsSkipConfidence)
                    {
                        // Frame ist normal → ueberspringen (spart DINO/SAM/Qwen)
                        skippedFrames++;
                        _logger.LogDebug("Frame {Frame}: YOLO-cls '{Class}' ({Conf:F0}%) → skip",
                            frameIndex, topPred.ClassName, topPred.Confidence * 100);
                        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                            $"Frame {frameIndex}/{totalFrames} – cls: {topPred.ClassName} ({topPred.Confidence:P0}) → skip"));
                        telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0,
                            frameSw.ElapsedMilliseconds, Skipped: true));
                        AdvanceAll(active, detections, DedupWindowFrames);
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

                    // Klassenspezifische Filterung: Jede Klasse hat ihren eigenen Schwellenwert
                    if (yoloResult.Detections.Count > 0 && _config.YoloClassConfidence.Count > 0)
                    {
                        var filtered = yoloResult.Detections
                            .Where(d =>
                            {
                                // VSA-Hauptcode aus YOLO-Klassenname extrahieren (z.B. "BAB_crack" → "BAB")
                                var baseCode = d.ClassName.Split('_')[0].ToUpperInvariant();
                                var threshold = _config.YoloClassConfidence.GetValueOrDefault(baseCode, _config.YoloConfidence);
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
                    AdvanceAll(active, detections, DedupWindowFrames);
                    continue;
                }
                yoloMs = phaseSw.ElapsedMilliseconds;
            }

            // ── DetectionAggregator: YOLO-Detektionen einspeisen ──
            // Jede YOLO-Detektion wird als FrameDetection an den Aggregator uebergeben.
            // Der Aggregator verdichtet hunderte Einzeldetektionen zu ~20 Schadensereignissen.
            if (yoloResult.Detections.Count > 0)
            {
                foreach (var det in yoloResult.Detections)
                {
                    var defectClass = YoloDefectTaxonomy.AllClasses
                        .FirstOrDefault(c => c.ClassName.Equals(det.ClassName, StringComparison.OrdinalIgnoreCase));
                    int classId = defectClass.ClassName != null ? defectClass.ClassId : -1;
                    string className = defectClass.ClassName ?? det.ClassName;

                    var effectiveClassId = classId >= 0 ? classId : 0;
                    // Eindeutiger Frame-Key — Aggregator speichert den Key des Peak-Frames
                    var frameKey = $"f{frameIndex}";
                    var frameDet = new FrameDetection
                    {
                        YoloClassId = effectiveClassId,
                        YoloClassName = className,
                        Confidence = det.Confidence,
                        TimeSeconds = t,
                        Meter = estimatedMeter,
                        FramePath = frameKey,
                        Bbox = new[] { det.X1, det.Y1, det.X2, det.Y2 }
                    };

                    // Frame-Bytes cachen (Aggregator waehlt spaeter den Peak-Frame)
                    peakFrameCache.TryAdd(frameKey, frameBytes);

                    var closedEvent = aggregator.Feed(frameDet);
                    if (closedEvent != null)
                    {
                        aggregatedEvents.Add(closedEvent);
                        _logger.LogDebug(
                            "Aggregator Event geschlossen: {Class} @ {MeterStart:F1}-{MeterEnd:F1}m, " +
                            "Peak={PeakConf:F2}, Frames={FrameCount}",
                            closedEvent.YoloClassName, closedEvent.MeterStart, closedEvent.MeterEnd,
                            closedEvent.PeakConfidence, closedEvent.FrameCount);
                    }
                }
            }

            if (!yoloResult.IsRelevant)
            {
                irrelevantFrames++;
                var runDinoFallback = EnableDinoFallbackOnIrrelevantFrames
                    && isAfterOsd
                    && DinoFallbackEveryNIrrelevantFrames > 0
                    && (irrelevantFrames % DinoFallbackEveryNIrrelevantFrames == 0);

                if (!runDinoFallback)
                {
                    skippedFrames++;
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex}/{totalFrames} – übersprungen (YOLO: irrelevant, {skippedFrames} gesamt)"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                    AdvanceAll(active, detections, DedupWindowFrames);
                    continue;
                }

                yoloResult = yoloResult with { IsRelevant = true };
                _logger.LogDebug(
                    "Frame {Frame}: YOLO irrelevant -> DINO-Fallback (irrelevant#{IrrelevantCount})",
                    frameIndex, irrelevantFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} - YOLO irrelevant -> DINO-Fallback",
                    FramePreviewPng: frameBytes));
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
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var dinoMs = phaseSw.ElapsedMilliseconds;

            if (dinoResult.Detections.Count == 0)
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: false));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            // ── Tiefenfilter: Punkt vs. Streckenschaden unterscheiden ──
            // VSA-Konvention: Metrierung bei Oberkante des Ereignisses im Bild.
            // Objekte im oberen Bilddrittel (nahe Fluchtpunkt) sind ~1m entfernt.
            //
            // Streckenschaden (BA=Riss/Bruch, BB=Wurzeln/Ablagerung, BDD=Wasserspiegel):
            //   → Tief schauen erlaubt — Ausdehnung des Schadens muss erfasst werden.
            // Punktereignis (BC=Anschluss/Bogen, BD ohne BDD, AE=Aenderungen):
            //   → Muss auf Kamerahoehe sein — obere 30% des Bildes filtern.
            var estimatedImageH = dinoResult.Detections.Max(d => d.Y2);
            if (estimatedImageH < 100) estimatedImageH = 720; // Fallback
            const double MinYCenterRatio = 0.30; // Obere 30% des Bildes = zu weit weg

            var filteredDino = dinoResult.Detections
                .Where(d =>
                {
                    double yCenterNorm = ((d.Y1 + d.Y2) / 2.0) / estimatedImageH;
                    // VSA-Code aus DINO-Label ableiten → Schadenstyp bestimmen
                    var vsaCode = VsaCodeResolver.InferCodeFromLabel(d.Label);
                    var prefix = vsaCode?.Length >= 2 ? vsaCode[..2] : "";
                    // BA (Riss/Bruch/Deformation) + BB (Wurzeln/Ablagerung/Infiltration)
                    // + BDD (Wasserspiegel/Rueckstau) = potenziell Streckenschaden
                    var code3 = vsaCode?.Length >= 3 ? vsaCode[..3].ToUpperInvariant() : "";
                    bool canBeStrecke = prefix is "BA" or "BB" || code3 is "BDD";
                    if (canBeStrecke) return true;
                    // BC (Anschluss/Bogen/Rohranfang), BD (Steuercodes), AE (Aenderungen)
                    // = Punktereignis → muss auf Kamerahoehe sein
                    return yCenterNorm >= MinYCenterRatio;
                })
                .ToList();

            if (filteredDino.Count < dinoResult.Detections.Count)
            {
                _logger.LogDebug("Frame {Frame}: {Removed} DINO-Boxen zu tief im Rohr gefiltert (Y < {Thresh:P0})",
                    frameIndex, dinoResult.Detections.Count - filteredDino.Count, MinYCenterRatio);
            }

            if (filteredDino.Count == 0)
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: false));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            // ── Step 3: SAM Segmentation ──
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – SAM Segmentation ({filteredDino.Count} Boxes)...",
                FramePreviewPng: frameBytes));

            var samBoxes = filteredDino
                .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
                .ToList();

            phaseSw.Restart();
            SamResponse samResult;
            try
            {
                samResult = await _client.SegmentSamAsync(
                    new SamRequest(frameBase64, samBoxes, PipeDiameterMm: pipeDiameterMm), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed", frameIndex);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex} – SAM Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var samMs = phaseSw.ElapsedMilliseconds;

            // ── Step 4: Quantification ──
            var quantified = MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm);
            var meter = EstimateMeter(t, duration, ref lastMeter);

            // Capture max DINO confidence for EvidenceVector (nach Tiefenfilter)
            var maxDinoConf = filteredDino.Count > 0
                ? filteredDino.Max(d => d.Confidence) : 0.0;

            // Build findings from quantified masks
            var findings = new List<EnhancedFinding>(quantified.Count);
            for (var i = 0; i < quantified.Count; i++)
            {
                var q = quantified[i];
                if (string.IsNullOrWhiteSpace(q.Label))
                    continue;

                var bbox = i < samResult.Masks.Count ? GetNormalizedBbox(samResult.Masks[i], samResult.ImageWidth, samResult.ImageHeight) : default;
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
                    BboxX1Norm: bbox.X1,
                    BboxY1Norm: bbox.Y1,
                    BboxX2Norm: bbox.X2,
                    BboxY2Norm: bbox.Y2,
                    Notes: $"DINO conf={q.Confidence:F2}"
                ));
            }

            // Build per-frame EvidenceVector with pipeline signals
            var yoloEvidence = yoloResult.Detections.Count > 0
                ? yoloResult.Detections.Max(d => d.Confidence)
                : (telemetryBypass ? 0.60 : (yoloResult.IsRelevant ? 0.50 : 0.0));

            // SAM Mask-Stability: Mittelwert der Masken-Konfidenzen (0-1)
            var samStability = samResult.Masks.Count > 0
                ? samResult.Masks.Average(m => m.Confidence)
                : (double?)null;

            var frameEvidence = new QualityGate.EvidenceVector(
                YoloConf: yoloEvidence,
                DinoConf: maxDinoConf,
                SamMaskStability: samStability,
                QwenVisionConf: null,   // populated after Qwen enrichment
                FrameCount: 1
            );

            // ── Step 5: Qwen VSA-Code enrichment (optional) ──
            long qwenMs = 0;
            if (_qwenVision is not null && findings.Count > 0)
            {
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
                    (string Code, string Description, double Meter, double Confidence)? prevCtx;
                    lock (_lastFindingLock)
                    {
                        prevCtx = _lastFinding is var (pc, pd, pm, pconf) && Math.Abs(meter - pm) < 1.0
                            ? _lastFinding : null;
                    }
                    var qwenResult = await _qwenVision.AnalyzeWithContextAsync(
                        frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token).ConfigureAwait(false);

                    frameEvidence = frameEvidence with
                    {
                        QwenVisionConf = EstimateQwenVisionConfidence(qwenResult.ImageQuality, qwenResult.HasFindings)
                    };

                    // OSD-Meterstand IMMER uebernehmen (auch ohne Findings)
                    if (qwenResult.Meter.HasValue)
                    {
                        meter = qwenResult.Meter.Value;
                        lastMeter = meter;
                    }

                    // Material-Selbsterkennung + Wechsel-Erkennung (AED)
                    if (!string.IsNullOrWhiteSpace(qwenResult.PipeMaterial)
                        && qwenResult.PipeMaterial != "unbekannt")
                    {
                        materialVotes.AddOrUpdate(qwenResult.PipeMaterial,
                            1, (_, count) => count + 1);

                        // AED: Rohrmaterialwechsel erkennen
                        // Nur wenn (a) vorher ein anderes Material lief und
                        // (b) mindestens 3m seit letztem Wechsel (Spam vermeiden)
                        if (lastDetectedMaterial is not null
                            && !lastDetectedMaterial.Equals(qwenResult.PipeMaterial, StringComparison.OrdinalIgnoreCase)
                            && meter - lastMaterialChangeMeter > 3.0)
                        {
                            var aedCode = MapMaterialToAedCode(qwenResult.PipeMaterial);
                            findings.Add(new EnhancedFinding(
                                Label: $"material_change_{qwenResult.PipeMaterial}",
                                VsaCodeHint: aedCode,
                                Severity: 1,
                                PositionClock: null,
                                ExtentPercent: null,
                                HeightMm: null, WidthMm: null,
                                IntrusionPercent: null,
                                CrossSectionReductionPercent: null,
                                DiameterReductionMm: null,
                                Notes: $"Materialwechsel: {lastDetectedMaterial} → {qwenResult.PipeMaterial}"));
                            lastMaterialChangeMeter = meter;
                            _logger.LogInformation(
                                "AED erkannt @ {Meter:F1}m: {Von} → {Nach} (Code: {Code})",
                                meter, lastDetectedMaterial, qwenResult.PipeMaterial, aedCode);
                        }
                        lastDetectedMaterial = qwenResult.PipeMaterial;
                    }

                    // ImageQuality-Gate (recall-optimiert):
                    // Bei schlechter Bildqualitaet nur dann verwerfen, wenn auch DINO schwach ist.
                    // So bleiben plausible SAM/DINO-Treffer erhalten.
                    if (ShouldSuppressByImageQuality(qwenResult.ImageQuality, maxDinoConf))
                    {
                        _logger.LogDebug(
                            "Frame {Frame}: ImageQuality=schlecht + DINO={Dino:F2} -> {Count} Findings verworfen",
                            frameIndex, maxDinoConf, findings.Count);
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

                            if (match is not null && !string.IsNullOrWhiteSpace(qf.VsaCodeHint))
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
                            lock (_lastFindingLock)
                            {
                                _lastFinding = (
                                    topFinding.VsaCodeHint ?? topFinding.Label,
                                    topFinding.Label,
                                    meter,
                                    topFinding.Severity / 5.0); // Severity 1-5 → Confidence 0.2-1.0
                            }
                        }

                        _logger.LogDebug("Frame {Frame}: Qwen enriched {Count} findings with VSA codes",
                            frameIndex, qwenResult.Findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint)));
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Frame {Frame}: Qwen VSA-Code-Mapping timeout ({Timeout}s)",
                        frameIndex, QwenFrameTimeout.TotalSeconds);
                }
                catch (Exception ex)
                {
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

            // ── BCD-Regel: Erster Befund bei 0.00m ist immer Rohranfang ──
            // In der BCD-Zone (Meter < 1.5, Anfang Video) sieht die Kamera den
            // Schacht: Wasser, Schmutz, Rohrwand — keine echten Schaeden.
            // Alle Findings werden zu einem BCD konsolidiert.
            if (isBcdZone && findings.Count > 0)
            {
                findings.Clear();
                findings.Add(new EnhancedFinding(
                    Label: "pipe_start",
                    VsaCodeHint: "BCD",
                    Severity: 0,
                    PositionClock: null,
                    ExtentPercent: null,
                    HeightMm: null, WidthMm: null,
                    IntrusionPercent: null,
                    CrossSectionReductionPercent: null,
                    DiameterReductionMm: null,
                    Notes: "BCD-Regel: Rohranfang bei 0.00m"));
            }

            // Update active findings (dedup)
            UpdateActive(active, findings, meter, detections, frameEvidence);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {findings.Count} Befunde (Multi-Model)",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));

            } // end try (Per-Frame-Timeout)
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Frame-Timeout (45s) — nur dieses Frame uebersprungen, Blindtest laeuft weiter
                _logger.LogWarning("Frame {Index} @ {Time:F1}s: Per-Frame-Timeout ({Sec}s) — ueberspringe",
                    frameIndex, frame.TimestampSeconds, PerFrameTimeout.TotalSeconds);
                telemetry.RecordFrame(new FrameTiming(frameIndex, frame.TimestampSeconds,
                    0, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
            }
            // OperationCanceledException von ct (Benutzer-Abbruch) fliegt durch → Pipeline stoppt
        }

        // ── DetectionAggregator: Verbleibende aktive Detektionen schliessen ──
        var flushedEvents = aggregator.Flush();
        aggregatedEvents.AddRange(flushedEvents);

        _logger.LogInformation(
            "DetectionAggregator: {EventCount} aggregierte Schadensereignisse aus Video " +
            "(vor Qwen-Klassifikation)",
            aggregatedEvents.Count);

        // ── Qwen-Klassifikation fuer aggregierte Peak-Frames ──
        // Statt Qwen fuer jeden Frame aufzurufen, nur fuer die ~20 Peak-Frames
        if (_qwenVision is not null && aggregatedEvents.Count > 0)
        {
            _logger.LogInformation(
                "Starte Qwen-Klassifikation fuer {Count} aggregierte Events...",
                aggregatedEvents.Count);

            foreach (var evt in aggregatedEvents)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Peak-Frame-Bytes aus Cache holen
                    if (!peakFrameCache.TryGetValue(evt.PeakFramePath, out var peakBytes))
                    {
                        _logger.LogWarning(
                            "Peak-Frame {Key} nicht im Cache — ueberspringe Qwen fuer {Class}",
                            evt.PeakFramePath, evt.YoloClassName);
                        continue;
                    }

                    var peakBase64 = Convert.ToBase64String(peakBytes);
                    // YOLO-Klasse als DINO-Detection-Kontext mitgeben,
                    // damit Qwen weiss was YOLO erkannt hat
                    var yoloHint = new DinoDetectionDto(
                        X1: evt.PeakBbox?[0] ?? 0, Y1: evt.PeakBbox?[1] ?? 0,
                        X2: evt.PeakBbox?[2] ?? 1, Y2: evt.PeakBbox?[3] ?? 1,
                        Label: evt.YoloClassName,
                        Confidence: evt.PeakConfidence,
                        Phrase: $"YOLO-Detektion: {evt.YoloClassName}");
                    var peakContext = new MultiModelFrameResult(
                        TimestampSec: evt.PeakTimeSeconds,
                        Meter: evt.MeterStart,
                        IsRelevant: true,
                        DinoDetections: [yoloHint],
                        SamMasks: [],
                        ImageWidth: 0, ImageHeight: 0,
                        YoloTimeMs: 0, DinoTimeMs: 0, SamTimeMs: 0);

                    using var qwenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    qwenCts.CancelAfter(QwenFrameTimeout);

                    var qwenResult = await _qwenVision.AnalyzeWithContextAsync(
                        peakBase64, peakContext, pipeDiameterMm, qwenCts.Token)
                        .ConfigureAwait(false);

                    // Ergebnis auf Event schreiben
                    if (qwenResult.HasFindings && qwenResult.Findings.Count > 0)
                    {
                        var topFinding = qwenResult.Findings[0];
                        evt.VsaCode = topFinding.VsaCodeHint;
                        evt.Severity = topFinding.Severity;
                        evt.ClockPosition = topFinding.PositionClock;
                        evt.IsClassified = true;

                        _logger.LogInformation(
                            "Qwen: {YoloClass} → {VsaCode} (Severity {Sev}, Uhr {Clock}) @ {Meter:F1}m",
                            evt.YoloClassName, evt.VsaCode, evt.Severity,
                            evt.ClockPosition, evt.MeterStart);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Qwen: Kein Befund fuer {Class} @ {Meter:F1}m",
                            evt.YoloClassName, evt.MeterStart);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Qwen-Timeout fuer {Class} @ {Meter:F1}m",
                        evt.YoloClassName, evt.MeterStart);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Qwen-Fehler fuer {Class} @ {Meter:F1}m",
                        evt.YoloClassName, evt.MeterStart);
                }
            }
        }

        // Nicht mehr benoetigte Frame-Bytes freigeben
        peakFrameCache.Clear();

        foreach (var evt in aggregatedEvents)
        {
            _logger.LogDebug(
                "Aggregiertes Event: {Class} (ID={ClassId}), Peak={PeakConf:F2}, " +
                "Meter={MeterStart:F1}-{MeterEnd:F1}, Frames={FrameCount}, " +
                "VSA={VsaCode}, Severity={Sev}",
                evt.YoloClassName, evt.YoloClassId, evt.PeakConfidence,
                evt.MeterStart, evt.MeterEnd, evt.FrameCount,
                evt.VsaCode ?? "(n/a)", evt.Severity);
        }

        // ── Aggregierte Events in RawVideoDetections konvertieren ──
        foreach (var evt in aggregatedEvents)
        {
            if (!evt.IsClassified && string.IsNullOrEmpty(evt.VsaCode))
                evt.VsaCode = evt.YoloClassName;  // Fallback: YOLO-Klasse als Hint

            detections.Add(new RawVideoDetection(
                FindingLabel: evt.VsaCode ?? evt.YoloClassName,
                MeterStart: evt.MeterStart,
                MeterEnd: evt.MeterEnd,
                Severity: (evt.Severity ?? 2).ToString(),
                VsaCodeHint: evt.VsaCode,
                PositionClock: evt.ClockPosition,
                BboxX1: evt.PeakBbox?[0],
                BboxY1: evt.PeakBbox?[1],
                BboxX2: evt.PeakBbox?[2],
                BboxY2: evt.PeakBbox?[3]
            ));
        }

        // Verbleibende aktive Findings: Steuercodes (BCD/BCE) finalisieren
        foreach (var a in active.Values)
        {
            if (a.ShouldFinalize)
                detections.Add(a.ToDetection());
        }

        // Konsens-Filter: nur Detektionen mit 2+ Modell-Bestaetigung und nicht Red
        ApplyConsensusAndQualityFilter(detections);

        // Material-Selbsterkennung auswerten: Mehrheitsentscheid ueber alle Frames
        // Ueberschreibt _config.PipeMaterial wenn (a) Config leer und (b) Qwen >= 3 Stimmen
        string? detectedMaterial = null;
        if (materialVotes.Count > 0)
        {
            var topMaterial = materialVotes.OrderByDescending(kv => kv.Value).First();
            if (topMaterial.Value >= 3) // Mindestens 3 Frames muessen uebereinstimmen
            {
                detectedMaterial = topMaterial.Key;
                _logger.LogInformation(
                    "Material-Selbsterkennung: {Material} ({Votes} Stimmen von {Total} Frames)",
                    detectedMaterial, topMaterial.Value, materialVotes.Values.Sum());

                // Wenn Config kein Material hat → erkanntes verwenden fuer Plausibilitaetsfilter
            }
        }

        // Materialplausibilitaet: Config-Material hat Vorrang, sonst Qwen-Erkennung
        var effectiveMaterial = _config.PipeMaterial ?? detectedMaterial;
        ApplyMaterialPlausibilityFilter(detections, effectiveMaterial);

        _logger.LogInformation(
            "Multi-Model Pipeline complete: {Detections} detections, {Skipped}/{Total} frames skipped, {Duration:F1}s video, Material={Material}",
            detections.Count, skippedFrames, frameIndex, duration, effectiveMaterial ?? "unbekannt");

        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"Multi-Model fertig – {detections.Count} Schäden, Material: {effectiveMaterial ?? "unbekannt"}"));

        var summary = telemetry.GetSummary();
        _logger.LogInformation(
            "Telemetry: Wall={WallMs}ms, Extraction Mean={ExtMean:F0}ms P95={ExtP95:F0}ms, YOLO Mean={YoloMean:F0}ms P95={YoloP95:F0}ms, DINO Mean={DinoMean:F0}ms, SAM Mean={SamMean:F0}ms, Qwen Mean={QwenMean:F0}ms",
            summary.WallClockMs, summary.Extraction.MeanMs, summary.Extraction.P95Ms,
            summary.Yolo.MeanMs, summary.Yolo.P95Ms, summary.Dino.MeanMs,
            summary.Sam.MeanMs, summary.Qwen.MeanMs);

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary);
    }

    // ── NVDEC Dual-Path ────────────────────────────────────────────────────

    /// <summary>
    /// NVDEC-beschleunigter Analysepfad: Sidecar dekodiert + YOLO in einem Schritt.
    /// Spart den PNG→Base64→HTTP-Overhead fuer 80-90% der Frames (irrelevante werden nicht uebertragen).
    /// Fuer relevante Frames: DINO/SAM/Qwen laufen normal in C# weiter.
    ///
    /// Voraussetzung: Sidecar meldet nvdec_available=true oder software-Fallback.
    /// Aufruf: Nur wenn /health → nvdec.nvdec_available=true oder generell als schnellere Alternative.
    /// </summary>
    public async Task<VideoAnalysisResult> AnalyzeWithNvdecAsync(
        string videoPath,
        IProgress<VideoAnalysisProgress>? progress = null,
        bool useVsr = false,
        CancellationToken ct = default)
    {
        videoPath = NormalizePath(videoPath);
        if (!File.Exists(videoPath))
            return VideoAnalysisResult.Failed($"Video nicht gefunden: {videoPath}");

        progress?.Report(new VideoAnalysisProgress(0, 0, "NVDEC-Pipeline: Stream wird gestartet..."));

        var detections = new List<RawVideoDetection>();
        var active = new Dictionary<string, ActiveFindingState>(StringComparer.OrdinalIgnoreCase);
        int frameIndex = 0;
        int skippedFrames = 0;
        int irrelevantFrames = 0;
        double lastMeter = 0;
        double duration = 0;
        int totalFrames = 0;

        // Aggregierte Schadensereignisse (NVDEC-Pfad)
        var aggregatorNvdec = CreateAggregator();
        var aggregatedEventsNvdec = new List<DetectionEvent>();

        int pipeDiameterMm = _config.PipeDiameterMmOverride ?? 300;

        var request = new VideoProcessRequest(
            VideoPath: videoPath,
            StepSeconds: FrameStepSeconds,
            Confidence: _config.YoloConfidence,
            Enhance: useVsr,
            EnhanceTargetHeight: 1080,
            MaxWidth: 1280
        );

        var telemetry = new PipelineTelemetry();

        await foreach (var item in _client.ProcessVideoStreamAsync(request, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            // ── Header: Metadaten empfangen ──
            if (item.Type == "header")
            {
                duration = item.DurationSec ?? 0;
                totalFrames = item.TotalFramesEstimate ?? 0;
                progress?.Report(new VideoAnalysisProgress(0, totalFrames,
                    $"NVDEC-Pipeline: {totalFrames} Frames, DN{pipeDiameterMm}, " +
                    $"Backend: {(item.NvdecAvailable == true ? "NVDEC" : "Software")}"));
                continue;
            }

            // ── Footer: Stream-Ende ──
            if (item.Type == "footer")
            {
                _logger.LogInformation("NVDEC-Stream beendet: {Frames} Frames verarbeitet", item.FramesProcessed);
                continue;
            }

            // ── Fehler ──
            if (item.Type == "error" || item.Error is not null)
            {
                _logger.LogWarning("NVDEC-Stream Fehler @ Frame {Frame}: {Error}", item.FrameIndex, item.Error);
                continue;
            }

            // ── Frame ──
            if (item.Type != "frame" || item.TimestampSec is null)
                continue;

            var frameSw = Stopwatch.StartNew();
            frameIndex++;
            double t = item.TimestampSec.Value;
            long yoloMs = (long)(item.YoloMs ?? 0);

            // Per-Frame-Timeout (NVDEC-Pfad)
            using var frameCtsNvdec = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCtsNvdec.CancelAfter(PerFrameTimeout);

            try
            {

            if (item.IsRelevant != true)
            {
                irrelevantFrames++;
                var runDinoFallback = EnableDinoFallbackOnIrrelevantFrames
                    && t > DinoFallbackStartSeconds
                    && DinoFallbackEveryNIrrelevantFrames > 0
                    && (irrelevantFrames % DinoFallbackEveryNIrrelevantFrames == 0)
                    && !string.IsNullOrWhiteSpace(item.ImageBase64);

                if (!runDinoFallback)
                {
                    skippedFrames++;
                    progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                        $"Frame {frameIndex}/{totalFrames} – {item.FrameClass ?? "irrelevant"} (NVDEC)"));
                    telemetry.RecordFrame(new FrameTiming(frameIndex, t, 0, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                    AdvanceAll(active, detections, DedupWindowFrames);
                    continue;
                }

                _logger.LogDebug(
                    "Frame {Frame}: NVDEC/YOLO irrelevant -> DINO-Fallback (irrelevant#{IrrelevantCount})",
                    frameIndex, irrelevantFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} - YOLO irrelevant -> DINO-Fallback (NVDEC)"));
            }

            // Relevanter Frame: image_base64 vorhanden → DINO/SAM/Qwen
            var frameBase64 = item.ImageBase64;
            if (string.IsNullOrEmpty(frameBase64))
            {
                _logger.LogWarning("Frame {Frame}: is_relevant=true aber kein image_base64", frameIndex);
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            // Fuer frameBytes (Preview): nur bei Bedarf dekodieren
            byte[]? frameBytes = null;

            // ── YOLO-Ergebnis aus Stream wiederverwenden ──
            var yoloResult = new YoloResponse(
                IsRelevant: true,
                Detections: item.Detections ?? Array.Empty<YoloDetectionDto>(),
                FrameClass: item.FrameClass ?? "relevant",
                InferenceTimeMs: item.YoloMs ?? 0
            );

            // ── Telemetrie-Bypass-Logik (gleich wie FFmpeg-Pfad) ──
            double estimatedMeter = EstimateMeter(t, duration > 0 ? duration : 300, ref lastMeter);
            bool isAfterOsd = t > DinoFallbackStartSeconds;
            bool isBcdZone = isAfterOsd && estimatedMeter < 1.5 && frameIndex <= 10;
            bool isBceZone = duration > 10 && t > (duration - FrameStepSeconds * 2);

            // ── DetectionAggregator: YOLO-Detektionen einspeisen (NVDEC-Pfad) ──
            if (yoloResult.Detections.Count > 0)
            {
                foreach (var det in yoloResult.Detections)
                {
                    var defectClass = YoloDefectTaxonomy.AllClasses
                        .FirstOrDefault(c => c.ClassName.Equals(det.ClassName, StringComparison.OrdinalIgnoreCase));
                    int classId = defectClass.ClassName != null ? defectClass.ClassId : -1;

                    var frameDet = new FrameDetection
                    {
                        YoloClassId = classId >= 0 ? classId : 0,
                        YoloClassName = defectClass.ClassName ?? det.ClassName,
                        Confidence = det.Confidence,
                        TimeSeconds = t,
                        Meter = estimatedMeter,
                        FramePath = $"nvdec_frame_{frameIndex}_{t:F1}s",
                        Bbox = new[] { det.X1, det.Y1, det.X2, det.Y2 }
                    };

                    var closedEvent = aggregatorNvdec.Feed(frameDet);
                    if (closedEvent != null)
                    {
                        aggregatedEventsNvdec.Add(closedEvent);
                        _logger.LogDebug(
                            "Aggregator Event (NVDEC): {Class} @ {MeterStart:F1}-{MeterEnd:F1}m, " +
                            "Peak={PeakConf:F2}, Frames={FrameCount}",
                            closedEvent.YoloClassName, closedEvent.MeterStart, closedEvent.MeterEnd,
                            closedEvent.PeakConfidence, closedEvent.FrameCount);
                    }
                }
            }

            // Frame als Preview-PNG dekodieren (nur fuer relevante Frames)
            try
            {
                frameBytes = Convert.FromBase64String(frameBase64);
            }
            catch { /* preview optional */ }

            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – DINO Detection (NVDEC)...",
                FramePreviewPng: frameBytes));

            // ── Step 2: Grounding DINO ──
            var phaseSw = Stopwatch.StartNew();
            DinoResponse dinoResult;
            try
            {
                dinoResult = await _client.DetectDinoAsync(
                    new DinoRequest(
                        frameBase64,
                        null,
                        _config.DinoBoxThreshold,
                        _config.DinoTextThreshold), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: DINO detection failed (NVDEC-Pfad)", frameIndex);
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, 0, yoloMs, phaseSw.ElapsedMilliseconds, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var dinoMs = phaseSw.ElapsedMilliseconds;

            if (dinoResult.Detections.Count == 0)
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, 0, yoloMs, dinoMs, 0, 0, frameSw.ElapsedMilliseconds, Skipped: false));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            // ── Step 3: SAM Segmentation ──
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – SAM Segmentation ({dinoResult.Detections.Count} Boxes, NVDEC)...",
                FramePreviewPng: frameBytes));

            var samBoxes = dinoResult.Detections
                .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
                .ToList();

            phaseSw.Restart();
            SamResponse samResult;
            try
            {
                samResult = await _client.SegmentSamAsync(
                    new SamRequest(frameBase64, samBoxes, PipeDiameterMm: pipeDiameterMm), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed (NVDEC-Pfad)", frameIndex);
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, 0, yoloMs, dinoMs, phaseSw.ElapsedMilliseconds, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var samMs = phaseSw.ElapsedMilliseconds;

            // ── Step 4: Quantification ──
            var quantified = MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm);
            var meter = EstimateMeter(t, duration > 0 ? duration : 300, ref lastMeter);

            var maxDinoConf = dinoResult.Detections.Count > 0
                ? dinoResult.Detections.Max(d => d.Confidence) : 0.0;

            var findings = new List<EnhancedFinding>(quantified.Count);
            for (var i = 0; i < quantified.Count; i++)
            {
                var q = quantified[i];
                if (string.IsNullOrWhiteSpace(q.Label))
                    continue;

                var bbox = i < samResult.Masks.Count
                    ? GetNormalizedBbox(samResult.Masks[i], samResult.ImageWidth, samResult.ImageHeight)
                    : default;
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
                    BboxX1Norm: bbox.X1,
                    BboxY1Norm: bbox.Y1,
                    BboxX2Norm: bbox.X2,
                    BboxY2Norm: bbox.Y2,
                    Notes: $"NVDEC/DINO conf={q.Confidence:F2}"
                ));
            }

            var yoloEvidence = yoloResult.Detections.Count > 0
                ? yoloResult.Detections.Max(d => d.Confidence)
                : 0.60;

            // SAM Mask-Stability: Mittelwert der Masken-Konfidenzen (0-1)
            var samStability = samResult.Masks.Count > 0
                ? samResult.Masks.Average(m => m.Confidence)
                : (double?)null;

            var frameEvidence = new QualityGate.EvidenceVector(
                YoloConf: yoloEvidence,
                DinoConf: maxDinoConf,
                SamMaskStability: samStability,
                QwenVisionConf: null,
                FrameCount: 1
            );

            // ── Step 5: Qwen VSA-Code enrichment (optional) ──
            long qwenMs = 0;
            if (_qwenVision is not null && findings.Count > 0)
            {
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – Qwen VSA-Code-Mapping (NVDEC)...",
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
                        YoloTimeMs: item.YoloMs ?? 0,
                        DinoTimeMs: dinoResult.InferenceTimeMs,
                        SamTimeMs: samResult.InferenceTimeMs);

                    using var qwenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    qwenCts.CancelAfter(QwenFrameTimeout);

                    var qwenResult = await _qwenVision.AnalyzeWithContextAsync(
                        frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token).ConfigureAwait(false);

                    frameEvidence = frameEvidence with
                    {
                        QwenVisionConf = EstimateQwenVisionConfidence(qwenResult.ImageQuality, qwenResult.HasFindings)
                    };

                    if (qwenResult.Meter.HasValue)
                    {
                        meter = qwenResult.Meter.Value;
                        lastMeter = meter;
                    }

                    if (ShouldSuppressByImageQuality(qwenResult.ImageQuality, maxDinoConf))
                        findings.Clear();

                    if (qwenResult.HasFindings)
                    {
                        foreach (var qf in qwenResult.Findings)
                        {
                            var match = findings.FirstOrDefault(f =>
                                f.Label.Equals(qf.Label, StringComparison.OrdinalIgnoreCase) ||
                                qf.Label.Contains(f.Label, StringComparison.OrdinalIgnoreCase) ||
                                f.Label.Contains(qf.Label, StringComparison.OrdinalIgnoreCase));

                            if (match is not null && !string.IsNullOrWhiteSpace(qf.VsaCodeHint))
                            {
                                var idx = findings.IndexOf(match);
                                findings[idx] = match with { VsaCodeHint = qf.VsaCodeHint };
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Frame {Frame}: Qwen Timeout (NVDEC-Pfad)", frameIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Frame {Frame}: Qwen fehlgeschlagen (NVDEC-Pfad)", frameIndex);
                }
                qwenMs = phaseSw.ElapsedMilliseconds;
            }

            telemetry.RecordFrame(new FrameTiming(frameIndex, t, 0, yoloMs, dinoMs, samMs, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

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

            UpdateActive(active, findings, meter, detections, frameEvidence);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {findings.Count} Befunde (NVDEC)",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));

            } // end try (Per-Frame-Timeout NVDEC)
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("NVDEC Frame {Index} @ {Time:F1}s: Per-Frame-Timeout ({Sec}s) — ueberspringe",
                    frameIndex, t, PerFrameTimeout.TotalSeconds);
                telemetry.RecordFrame(new FrameTiming(frameIndex, t,
                    0, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
            }
        }

        // ── DetectionAggregator: Verbleibende aktive Detektionen schliessen (NVDEC) ──
        var flushedEventsNvdec = aggregatorNvdec.Flush();
        aggregatedEventsNvdec.AddRange(flushedEventsNvdec);

        _logger.LogInformation(
            "DetectionAggregator (NVDEC): {EventCount} aggregierte Schadensereignisse",
            aggregatedEventsNvdec.Count);

        // ── Aggregierte Events in RawVideoDetections konvertieren (NVDEC-Pfad) ──
        foreach (var evt in aggregatedEventsNvdec)
        {
            if (!evt.IsClassified && string.IsNullOrEmpty(evt.VsaCode))
                evt.VsaCode = evt.YoloClassName;

            detections.Add(new RawVideoDetection(
                FindingLabel: evt.VsaCode ?? evt.YoloClassName,
                MeterStart: evt.MeterStart,
                MeterEnd: evt.MeterEnd,
                Severity: (evt.Severity ?? 2).ToString(),
                VsaCodeHint: evt.VsaCode,
                PositionClock: evt.ClockPosition,
                BboxX1: evt.PeakBbox?[0],
                BboxY1: evt.PeakBbox?[1],
                BboxX2: evt.PeakBbox?[2],
                BboxY2: evt.PeakBbox?[3]
            ));

            _logger.LogDebug(
                "Aggregiertes Event (NVDEC): {Class} → {VsaCode}, Severity={Sev}, " +
                "Meter={MeterStart:F1}-{MeterEnd:F1}, Frames={FrameCount}",
                evt.YoloClassName, evt.VsaCode ?? "(n/a)", evt.Severity,
                evt.MeterStart, evt.MeterEnd, evt.FrameCount);
        }

        // Verbleibende aktive Findings: Steuercodes finalisieren
        foreach (var a in active.Values)
        {
            if (a.ShouldFinalize)
                detections.Add(a.ToDetection());
        }

        // Konsens-Filter: nur Detektionen mit 2+ Modell-Bestaetigung und nicht Red
        ApplyConsensusAndQualityFilter(detections);

        // Materialplausibilitaet: unplausible Codes fuer Rohrmaterial entfernen
        ApplyMaterialPlausibilityFilter(detections);

        _logger.LogInformation(
            "NVDEC-Pipeline fertig: {Detections} Detektionen, {Skipped}/{Total} Frames uebersprungen, {Duration:F1}s Video",
            detections.Count, skippedFrames, frameIndex, duration);

        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"NVDEC-Pipeline fertig – {detections.Count} Schäden, {skippedFrames} Frames übersprungen."));

        var summary = telemetry.GetSummary();
        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary);
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
                BboxX1Norm: bbox.X1,
                BboxY1Norm: bbox.Y1,
                BboxX2Norm: bbox.X2,
                BboxY2Norm: bbox.Y2,
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

    private static bool ShouldSuppressByImageQuality(string? imageQuality, double dinoConf)
    {
        if (!string.Equals(imageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
            return false;

        // Niedrige DINO-Konfidenz + schlechte Bildqualitaet => likely false positive.
        // Bei starken DINO-Hinweisen behalten wir Findings fuer bessere Recall.
        return dinoConf < 0.35;
    }

    private static double EstimateQwenVisionConfidence(string? imageQuality, bool hasFindings)
    {
        var baseConf = imageQuality?.ToLowerInvariant() switch
        {
            "gut" => 0.85,
            "mittel" => 0.65,
            "schlecht" => 0.35,
            _ => 0.55
        };

        if (hasFindings)
            baseConf += 0.05;

        return Math.Clamp(baseConf, 0.0, 1.0);
    }

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

    private void UpdateActive(
        Dictionary<string, ActiveFindingState> active,
        List<EnhancedFinding> current,
        double meter,
        List<RawVideoDetection> completed,
        QualityGate.EvidenceVector? evidence = null)
    {
        var currentMap = new Dictionary<string, EnhancedFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in current)
        {
            var key = BuildFindingKey(f);
            if (!currentMap.ContainsKey(key))
                currentMap[key] = f;
        }

        foreach (var key in active.Keys.ToList())
        {
            if (currentMap.TryGetValue(key, out var finding))
            {
                // BBox Y-Zentrum fuer Bestaetigungs-Tracking berechnen
                double yCenter = (finding.BboxY1Norm ?? 0 + (finding.BboxY2Norm ?? 1)) / 2.0;
                active[key].Update(meter, finding.Severity, finding.VsaCodeHint, finding.PositionClock,
                    finding.ExtentPercent, finding.HeightMm, finding.WidthMm,
                    finding.IntrusionPercent, finding.CrossSectionReductionPercent, finding.DiameterReductionMm,
                    evidence, yCenter);
            }
            else
            {
                active[key].MissedFrames++;
                if (active[key].MissedFrames >= DedupWindowFrames)
                {
                    FinalizeOrDiscard(active, key, completed);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (!active.ContainsKey(pair.Key))
            {
                var f = pair.Value;
                double yCenter = (f.BboxY1Norm ?? 0 + (f.BboxY2Norm ?? 1)) / 2.0;
                active[pair.Key] = new ActiveFindingState(
                    f.Label.Trim(), meter, f.Severity, f.VsaCodeHint, f.PositionClock,
                    f.ExtentPercent, f.HeightMm, f.WidthMm,
                    f.IntrusionPercent, f.CrossSectionReductionPercent, f.DiameterReductionMm,
                    evidence, yCenter);
            }
        }
    }

    /// <summary>
    /// Finalisiert oder verwirft einen Befund basierend auf Bestaetigung.
    /// Unbestaetigte Ferndetektionen werden still verworfen (Selbstkorrektur).
    /// </summary>
    private void FinalizeOrDiscard(
        Dictionary<string, ActiveFindingState> active,
        string key,
        List<RawVideoDetection> completed)
    {
        var state = active[key];
        if (state.ShouldFinalize)
        {
            completed.Add(state.ToDetection());
        }
        else
        {
            _logger.LogDebug(
                "Selbstkorrektur: '{Name}' verworfen — {Frames} Frames, MaxY={Y:F2}, bestaetigt={Confirmed}",
                state.Name, state.FrameCount, state.MaxYCenter, state.IsConfirmed);
        }
        active.Remove(key);
    }

    private static void AdvanceAll(
        Dictionary<string, ActiveFindingState> active,
        List<RawVideoDetection> completed,
        int dedupWindow)
    {
        foreach (var key in active.Keys.ToList())
        {
            active[key].MissedFrames++;
            if (active[key].MissedFrames >= dedupWindow)
            {
                // Nur bestaetigte Befunde finalisieren
                if (active[key].ShouldFinalize)
                    completed.Add(active[key].ToDetection());
                active.Remove(key);
            }
        }
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
    /// Baut einen stabilen Dedup-Key fuer ein Finding.
    /// Normalisiert Labels gegen DINO-Phrasen-Drift (crack/fracture/break → gleicher Key)
    /// und Clock-Positionen (3:00/3/rechts → normalisierte Stunde).
    /// </summary>
    private static string BuildFindingKey(EnhancedFinding f)
    {
        var label = VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(f.Label)
            ?? NormalizeFindingLabel(f.Label.Trim());
        var clock = NormalizeClockPosition(f.PositionClock);
        // Keine Dimensionen im Key — wachsende Schaeden (z.B. 5x3 → 8x5mm)
        // wuerden sonst neue Keys erzeugen und die Dedup brechen.
        // Maximalwerte werden stattdessen in UpdateActive() aktualisiert.
        if (string.IsNullOrEmpty(clock))
            return label;
        return $"{label}|{clock}";
    }

    /// <summary>
    /// Normalisiert DINO-Labels auf kanonische Gruppen.
    /// "crack", "fracture", "break" → "crack"
    /// "root intrusion", "roots" → "roots"
    /// Reduziert Label-Drift zwischen Frames.
    /// </summary>
    private static string NormalizeFindingLabel(string label)
    {
        var lower = label.ToLowerInvariant();

        // Risse/Brueche
        if (lower.Contains("crack") || lower.Contains("fracture") || lower.Contains("riss"))
            return "crack";
        if (lower.Contains("break") || lower.Contains("bruch") || lower.Contains("collapse") || lower.Contains("einsturz"))
            return "break";

        // Deformation
        if (lower.Contains("deform") || lower.Contains("verform") || lower.Contains("dent") || lower.Contains("oval"))
            return "deformation";

        // Wurzeln
        if (lower.Contains("root") || lower.Contains("wurzel"))
            return "roots";

        // Korrosion / Oberflaechenschaden
        if (lower.Contains("corros") || lower.Contains("erosion") || lower.Contains("surface damage") || lower.Contains("abplatz"))
            return "corrosion";

        // Ablagerung
        if (lower.Contains("deposit") || lower.Contains("sediment") || lower.Contains("buildup")
            || lower.Contains("ablagerung") || lower.Contains("inkrust"))
            return "deposit";

        // Infiltration
        if (lower.Contains("infiltrat") || lower.Contains("ingress") || lower.Contains("leak")
            || lower.Contains("undicht") || lower.Contains("fremdwasser"))
            return "infiltration";

        // Versatz
        if (lower.Contains("displace") || lower.Contains("offset") || lower.Contains("versatz") || lower.Contains("joint"))
            return "displacement";

        // Hindernis
        if (lower.Contains("obstacle") || lower.Contains("blockage") || lower.Contains("obstruct") || lower.Contains("hindernis"))
            return "obstacle";

        // Anschluss
        if (lower.Contains("connection") || lower.Contains("anschluss") || lower.Contains("intrud") || lower.Contains("protrud"))
            return "connection";

        return lower;
    }

    /// <summary>
    /// Normalisiert Clock-Positionen auf ganzzahlige Stunden.
    /// "3:00" → "3", "12" → "12", "Scheitel" → "12", "Sohle" → "6", "rechts" → "3", "links" → "9".
    /// </summary>
    private static string? NormalizeClockPosition(string? clock)
    {
        var normalized = VsaCodeResolver.NormalizeClock(clock);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized;
    }

    // ── Materialplausibilitaet ─────────────────────────────────────────────

    /// <summary>
    /// Filtert Detektionen die fuer das verbaute Rohrmaterial unplausibel sind.
    /// Kunststoffrohre (PE, PVC, PP, GFK) sind dicht — Infiltration (BBF) ist nur
    /// bei gleichzeitigem Strukturschaden (BA) oder defekter Verbindung (BAJ/BAH)
    /// moeglich. Ohne Begleitschaden wird BBF bei Kunststoff verworfen.
    /// </summary>
    private void ApplyMaterialPlausibilityFilter(List<RawVideoDetection> detections, string? materialOverride = null)
    {
        var material = materialOverride ?? _config.PipeMaterial;
        if (string.IsNullOrWhiteSpace(material)) return;

        bool isKunststoff = IsKunststoffMaterial(material);
        if (!isKunststoff) return;

        // Pruefen ob ein Strukturschaden in der Naehe ist (±2m)
        bool HasNearbyStructuralDamage(double meter)
        {
            return detections.Any(d =>
            {
                var code = d.VsaCodeHint;
                if (string.IsNullOrEmpty(code) || code.Length < 2) return false;
                var prefix = code[..2].ToUpperInvariant();
                // BA = Strukturell (Riss, Bruch, Versatz), oder BAJ/BAH (defekte Verbindung)
                return prefix == "BA" && Math.Abs(d.MeterStart - meter) < 2.0;
            });
        }

        var before = detections.Count;
        detections.RemoveAll(d =>
        {
            var code = d.VsaCodeHint?.ToUpperInvariant() ?? "";
            // BBF = Infiltration: bei Kunststoff nur mit Begleitschaden
            if (code.StartsWith("BBF") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                _logger.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            // BBD = Eindringender Boden: bei intaktem Kunststoff ebenfalls nur mit Schaden
            if (code.StartsWith("BBD") && !HasNearbyStructuralDamage(d.MeterStart))
            {
                _logger.LogInformation(
                    "Materialplausibilitaet: {Code} @ {Meter:F1}m verworfen — Kunststoffrohr ({Material}) ohne Begleitschaden",
                    code, d.MeterStart, material);
                return true;
            }
            return false;
        });

        if (before != detections.Count)
        {
            _logger.LogInformation(
                "Materialplausibilitaet ({Material}): {Before} → {After} Detektionen",
                material, before, detections.Count);
        }
    }

    /// <summary>
    /// Mappt ein Qwen-Material auf den spezifischen AED-Untercode.
    /// VSA: AEDXO=PE, AEDXP=PP, AEDXQ=PVC, AEDXG=Beton, AEDXU=Steinzeug, AED=generisch.
    /// </summary>
    private static string MapMaterialToAedCode(string material)
    {
        var m = material.ToUpperInvariant();
        if (m.Contains("PE") || m.Contains("POLYETHYL")) return "AEDXO";
        if (m.Contains("PP") || m.Contains("POLYPROP")) return "AEDXP";
        if (m.Contains("PVC") || m.Contains("POLYVINYL")) return "AEDXQ";
        if (m.Contains("BETON") || m.Contains("CONCRETE")) return "AEDXG";
        if (m.Contains("STEINZEUG") || m.Contains("VITRIF")) return "AEDXU";
        if (m.Contains("GFK") || m.Contains("FIBERGLASS")) return "AEDXH";
        if (m.Contains("STAHL") || m.Contains("STEEL")) return "AEDXI";
        if (m.Contains("GUSS") || m.Contains("CAST")) return "AEDXJ";
        if (m.Contains("FASER") || m.Contains("ASBESTOS")) return "AEDXK";
        return "AED"; // Generisch
    }

    /// <summary>
    /// Prueft ob das Material ein Kunststoff ist (dichte Rohrwand).
    /// Erkennt: Polyethylen, PE, PVC, PP, GFK, Kunststoff, Plastik etc.
    /// </summary>
    private static bool IsKunststoffMaterial(string material)
    {
        var m = material.ToUpperInvariant();
        return m.Contains("PE") || m.Contains("PVC") || m.Contains("PP")
            || m.Contains("GFK") || m.Contains("KUNSTSTOFF") || m.Contains("PLASTIK")
            || m.Contains("POLYETHYL") || m.Contains("POLYPROP") || m.Contains("POLYVINYL")
            || m.Contains("HDPE") || m.Contains("FASERZ"); // Faserzement = auch dicht
    }

    // ── ActiveFindingState (mirrors VideoFullAnalysisService.ActiveFinding) ──

    /// <summary>
    /// Filtert Detektionen die nicht von mindestens 2 Modellen bestaetigt werden,
    /// entfernt Red-QualityGate-Ergebnisse und verwirft zu kleine/weit entfernte Objekte.
    /// </summary>
    private void ApplyConsensusAndQualityFilter(List<RawVideoDetection> detections)
    {
        const double YoloMin = 0.20;  // YOLO bestaetigt
        const double DinoMin = 0.25;  // DINO bestaetigt
        const double QwenMin = 0.55;  // Qwen bestaetigt (vorher 0.40 — zu nah an Zufall)

        // Minimale BBox-Flaeche (normiert): Objekte unter ~3% der Bildflaeche sind zu weit weg
        // (max ~20cm Entfernung bei typischen Kanalrohren DN100-DN600)
        const double MinBboxArea = 0.03;

        var qg = new QualityGate.QualityGateService();
        var before = detections.Count;

        detections.RemoveAll(d =>
        {
            // 0. Zu kleine/weit entfernte Objekte verwerfen
            if (d.BboxX1 is not null && d.BboxY1 is not null &&
                d.BboxX2 is not null && d.BboxY2 is not null)
            {
                var bboxW = Math.Abs(d.BboxX2.Value - d.BboxX1.Value);
                var bboxH = Math.Abs(d.BboxY2.Value - d.BboxY1.Value);
                var area = bboxW * bboxH;
                if (area < MinBboxArea)
                    return true;
            }

            if (d.Evidence is not { } ev) return false; // Kein Evidence → behalten (Legacy)

            // 1. QualityGate Red → raus
            var result = qg.Evaluate(ev);
            if (result.IsRed)
                return true;

            // 2. Multi-Model-Konsens: mindestens 2 Modelle muessen bestaetigen
            int confirmations = 0;
            if (ev.YoloConf is >= YoloMin) confirmations++;
            if (ev.DinoConf is >= DinoMin) confirmations++;
            if (ev.QwenVisionConf is >= QwenMin) confirmations++;

            return confirmations < 2;
        });

        if (before != detections.Count)
        {
            _logger.LogInformation(
                "Konsens+QualityGate-Filter: {Before} → {After} Detektionen ({Removed} entfernt)",
                before, detections.Count, before - detections.Count);
        }
    }

    private sealed class ActiveFindingState
    {
        public string Name { get; }
        public double MeterStart { get; private set; }
        public double MeterEnd { get; private set; }
        public int MaxSeverity { get; private set; }
        public string? VsaCodeHint { get; private set; }
        public string? PositionClock { get; private set; }
        public int? ExtentPercent { get; private set; }
        public int? HeightMm { get; private set; }
        public int? WidthMm { get; private set; }
        public int? IntrusionPercent { get; private set; }
        public int? CrossSectionReductionPercent { get; private set; }
        public int? DiameterReductionMm { get; private set; }
        public QualityGate.EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        // ── Bestaetigungs-Tracking ──────────────────────────────────
        // Ein Befund gilt erst als bestaetigt wenn er mindestens einmal
        // auf Kamerahoehe (Y >= 0.30) gesehen wurde. Ferndetektionen
        // (oberes Bilddrittel) allein reichen nicht.

        /// <summary>True wenn die Detection mindestens einmal auf Kamerahoehe bestaetigt wurde.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Naechste Y-Position (normiert) an der die Detection gesehen wurde. Hoeher = naeher.</summary>
        public double MaxYCenter { get; private set; }

        /// <summary>Meterstand bei Bestaetigung (naeher = genauer).</summary>
        public double? ConfirmedMeter { get; private set; }

        /// <summary>Mindestanzahl Frames bevor ein Befund finalisiert wird.</summary>
        public const int MinConfirmationFrames = 2;

        /// <summary>Y-Schwelle ab der eine Detection als "auf Kamerahoehe" gilt (normiert).</summary>
        private const double ConfirmationYThreshold = 0.30;

        public ActiveFindingState(
            string name, double start, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            MaxSeverity = severity; VsaCodeHint = hint; PositionClock = clock;
            ExtentPercent = extent; HeightMm = height; WidthMm = width;
            IntrusionPercent = intrusion; CrossSectionReductionPercent = crossSection;
            DiameterReductionMm = diameterReduction;
            Evidence = evidence;
            MaxYCenter = bboxYCenterNorm;
            if (bboxYCenterNorm >= ConfirmationYThreshold)
            {
                IsConfirmed = true;
                ConfirmedMeter = start;
            }
        }

        public void Update(double meter, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            MeterEnd = meter;
            MissedFrames = 0;
            FrameCount++;
            if (severity > MaxSeverity) MaxSeverity = severity;
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
            if (!string.IsNullOrWhiteSpace(clock)) PositionClock = clock;
            if (extent is { } e) ExtentPercent = Math.Max(ExtentPercent ?? 0, Math.Clamp(e, 1, 100));
            if (height is { } h) HeightMm = Math.Max(HeightMm ?? 0, h);
            if (width is { } w) WidthMm = Math.Max(WidthMm ?? 0, w);
            if (intrusion is { } ip) IntrusionPercent = Math.Max(IntrusionPercent ?? 0, ip);
            if (crossSection is { } csr) CrossSectionReductionPercent = Math.Max(CrossSectionReductionPercent ?? 0, csr);
            if (diameterReduction is { } dr) DiameterReductionMm = Math.Max(DiameterReductionMm ?? 0, dr);
            if (evidence is not null)
            {
                Evidence = Evidence is null ? evidence : MergeEvidence(Evidence, evidence);
            }

            // Bestaetigungs-Tracking: wenn naeher gesehen → Meter korrigieren
            if (bboxYCenterNorm > MaxYCenter)
            {
                MaxYCenter = bboxYCenterNorm;
                if (bboxYCenterNorm >= ConfirmationYThreshold && !IsConfirmed)
                {
                    IsConfirmed = true;
                    ConfirmedMeter = meter;
                    // Meter korrigieren: Bestaetigung auf Kamerahoehe ist genauer
                    MeterStart = meter;
                }
            }
        }

        /// <summary>
        /// True wenn der Befund finalisiert werden soll (genug Frames + bestaetigt).
        /// Unbestaetigte Ferndetektionen werden still verworfen.
        /// </summary>
        public bool ShouldFinalize => IsConfirmed && FrameCount >= MinConfirmationFrames;

        public RawVideoDetection ToDetection() =>
            new(Name,
                ConfirmedMeter ?? MeterStart, // Bestaetigung-Meter hat Vorrang
                MeterEnd,
                SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";

        private static QualityGate.EvidenceVector MergeEvidence(QualityGate.EvidenceVector a, QualityGate.EvidenceVector b) =>
            new(
                YoloConf: Max(a.YoloConf, b.YoloConf),
                DinoConf: Max(a.DinoConf, b.DinoConf),
                SamMaskStability: Max(a.SamMaskStability, b.SamMaskStability),
                QwenVisionConf: Max(a.QwenVisionConf, b.QwenVisionConf),
                LlmCodeConf: Max(a.LlmCodeConf, b.LlmCodeConf),
                KbSimilarity: Max(a.KbSimilarity, b.KbSimilarity),
                KbCodeAgreement: a.KbCodeAgreement ?? b.KbCodeAgreement,
                PlausibilityScore: Max(a.PlausibilityScore, b.PlausibilityScore),
                DamageCategory: a.DamageCategory ?? b.DamageCategory,
                FrameCount: (a.FrameCount ?? 0) + (b.FrameCount ?? 0)
            );

        private static double? Max(double? a, double? b) =>
            a.HasValue && b.HasValue ? Math.Max(a.Value, b.Value)
            : a ?? b;
    }
}


