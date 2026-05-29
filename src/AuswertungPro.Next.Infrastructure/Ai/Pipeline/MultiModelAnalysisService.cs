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
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(300);

    /// <summary>YOLO-cls Vorfilter aktivieren/deaktivieren (Fallback: aus wenn kein Modell).</summary>
    public bool UseClsPrefilter { get; set; } = true;

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
        var active = new Dictionary<string, ActiveFindingState>(StringComparer.OrdinalIgnoreCase);
        int frameIndex = 0;
        int skippedFrames = 0;
        double lastMeter = 0;

        // Pipe diameter: from config override or default 300mm
        int pipeDiameterMm = _config.PipeDiameterMmOverride ?? 300;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames,
            $"Multi-Model Pipeline: {totalFrames} Frames, DN{pipeDiameterMm}"));

        var telemetry = new PipelineTelemetry();

        await using var stream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
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
                        && topPred.Confidence > 0.70)
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

            if (!yoloResult.IsRelevant)
            {
                skippedFrames++;
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – übersprungen (YOLO: irrelevant, {skippedFrames} gesamt)"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, yoloMs, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
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
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var samMs = phaseSw.ElapsedMilliseconds;

            // ── Step 4: Quantification ──
            var quantified = MaskQuantificationService.QuantifyAll(samResult, pipeDiameterMm);
            var meter = EstimateMeter(t, duration, ref lastMeter);

            // Capture max DINO confidence for EvidenceVector
            var maxDinoConf = dinoResult.Detections.Count > 0
                ? dinoResult.Detections.Max(d => d.Confidence) : 0.0;

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
                    BboxX1: bbox.X1,
                    BboxY1: bbox.Y1,
                    BboxX2: bbox.X2,
                    BboxY2: bbox.Y2,
                    Notes: $"DINO conf={q.Confidence:F2}"
                ));
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

                    // OSD-Meterstand IMMER uebernehmen (auch ohne Findings)
                    if (qwenResult.Meter.HasValue)
                    {
                        meter = qwenResult.Meter.Value;
                        lastMeter = meter;
                    }

                    // ImageQuality-Gate: Bei schlechter Bildqualitaet Findings verwerfen
                    // (OSD-Meter wird trotzdem uebernommen, nur Schadens-Findings sind unzuverlaessig)
                    if (string.Equals(qwenResult.ImageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Frame {Frame}: ImageQuality=schlecht, {Count} Findings verworfen",
                            frameIndex, findings.Count);
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

            // Update active findings (dedup)
            UpdateActive(active, findings, meter, detections, frameEvidence);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {findings.Count} Befunde (Multi-Model)",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));
        }

        // Flush remaining active findings
        foreach (var a in active.Values)
            detections.Add(a.ToDetection());

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

    private void UpdateActive(
        Dictionary<string, ActiveFindingState> active,
        List<EnhancedFinding> current,
        double meter,
        List<RawVideoDetection> completed,
        EvidenceVector? evidence = null)
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
                active[key].Update(meter, finding.Severity, finding.VsaCodeHint, finding.PositionClock,
                    finding.ExtentPercent, finding.HeightMm, finding.WidthMm,
                    finding.IntrusionPercent, finding.CrossSectionReductionPercent, finding.DiameterReductionMm,
                    evidence);
            }
            else
            {
                active[key].MissedFrames++;
                if (active[key].MissedFrames >= DedupWindowFrames)
                {
                    completed.Add(active[key].ToDetection());
                    active.Remove(key);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (!active.ContainsKey(pair.Key))
            {
                var f = pair.Value;
                active[pair.Key] = new ActiveFindingState(
                    f.Label.Trim(), meter, f.Severity, f.VsaCodeHint, f.PositionClock,
                    f.ExtentPercent, f.HeightMm, f.WidthMm,
                    f.IntrusionPercent, f.CrossSectionReductionPercent, f.DiameterReductionMm,
                    evidence);
            }
        }
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
        return string.IsNullOrEmpty(clock) ? label : $"{label}|{clock}";
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
        var normalized = NormalizeClock(clock);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized;
    }

    // ── ActiveFindingState (mirrors VideoFullAnalysisService.ActiveFinding) ──

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

    /// <summary>
    /// Bestimmt den effektiven MeterEnd: Streckenschaeden behalten den beobachteten
    /// Bereich (MeterStart..MeterEnd), Punktschaeden kollabieren auf eine Stelle
    /// (MeterEnd = MeterStart) -- sonst entstuenden kuenstliche Mini-Strecken.
    /// </summary>
    internal static double ResolveMeterEnd(string? vsaCode, double meterStart, double observedMeterEnd)
        => VsaCodeResolver.IsStreckenschadenCode(vsaCode ?? string.Empty)
            ? observedMeterEnd   // Streckenschaden: beobachteten Bereich behalten
            : meterStart;        // Punktschaden / unbekannt: auf eine Stelle kollabieren

    private sealed class ActiveFindingState
    {
        public string Name { get; }
        public double MeterStart { get; }
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
        public EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        public ActiveFindingState(
            string name, double start, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            EvidenceVector? evidence = null)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            MaxSeverity = severity; VsaCodeHint = hint; PositionClock = clock;
            ExtentPercent = extent; HeightMm = height; WidthMm = width;
            IntrusionPercent = intrusion; CrossSectionReductionPercent = crossSection;
            DiameterReductionMm = diameterReduction;
            Evidence = evidence;
        }

        public void Update(double meter, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            EvidenceVector? evidence = null)
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
            // Merge evidence: keep max of each signal
            if (evidence is not null)
            {
                Evidence = Evidence is null ? evidence : MergeEvidence(Evidence, evidence);
            }
        }

        public RawVideoDetection ToDetection() =>
            new(Name, MeterStart, ResolveMeterEnd(VsaCodeHint, MeterStart, MeterEnd), SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";

        private static EvidenceVector MergeEvidence(EvidenceVector a, EvidenceVector b) =>
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
