using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public double FrameStepSeconds { get; set; } = 3.0;
    public int DedupWindowFrames { get; set; } = 3;
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(300);

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

            // ── Step 1: YOLO Pre-Screening ──
            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} – YOLO Pre-Screening...",
                FramePreviewPng: frameBytes));

            var phaseSw = Stopwatch.StartNew();
            YoloResponse yoloResult;
            try
            {
                yoloResult = await _client.DetectYoloAsync(
                    new YoloRequest(frameBase64, _config.YoloConfidence), ct).ConfigureAwait(false);
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
            var yoloMs = phaseSw.ElapsedMilliseconds;

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
            var findings = quantified
                .Where(q => !string.IsNullOrWhiteSpace(q.Label))
                .Select(q => new EnhancedFinding(
                    Label: q.Label,
                    VsaCodeHint: null,
                    Severity: EstimateSeverity(q),
                    PositionClock: q.ClockPosition,
                    ExtentPercent: q.ExtentPercent,
                    HeightMm: q.HeightMm,
                    WidthMm: q.WidthMm,
                    IntrusionPercent: q.IntrusionPercent,
                    CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                    DiameterReductionMm: null,
                    Notes: $"DINO conf={q.Confidence:F2}"
                ))
                .ToList();

            // Build per-frame EvidenceVector with pipeline signals
            var frameEvidence = new QualityGate.EvidenceVector(
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
                    var qwenResult = await _qwenVision.AnalyzeWithContextAsync(
                        frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token).ConfigureAwait(false);

                    if (qwenResult.HasFindings)
                    {
                        // Merge Qwen VSA codes and meter reading into our findings
                        if (qwenResult.Meter.HasValue)
                        {
                            meter = qwenResult.Meter.Value;
                            lastMeter = meter;
                        }

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

        var findings = quantified
            .Where(q => !string.IsNullOrWhiteSpace(q.Label))
            .Select(q => new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: null,
                Severity: EstimateSeverity(q),
                PositionClock: q.ClockPosition,
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                Notes: null
            ))
            .ToList();

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

    private static double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Simple linear estimation (to be improved with OSD reading from Qwen)
        var estimated = t / Math.Max(duration, 1.0) * 100.0; // assume ~100m max
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

    private static string BuildFindingKey(EnhancedFinding f)
    {
        var label = f.Label.Trim();
        if (!string.IsNullOrWhiteSpace(f.PositionClock))
            return $"{label}|{f.PositionClock.Trim()}";
        return label;
    }

    // ── ActiveFindingState (mirrors VideoFullAnalysisService.ActiveFinding) ──

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
        public QualityGate.EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        public ActiveFindingState(
            string name, double start, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            QualityGate.EvidenceVector? evidence = null)
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
            QualityGate.EvidenceVector? evidence = null)
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
            new(Name, MeterStart, MeterEnd, SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
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
