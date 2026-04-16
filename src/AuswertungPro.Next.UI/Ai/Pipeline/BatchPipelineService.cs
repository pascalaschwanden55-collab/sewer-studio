// V4.1 Batch-Pipeline: YOLO Batch → Filter → DINO+SAM → Qwen ×6 parallel
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Batch-Pipeline: Verarbeitet Video-Frames in Batches statt einzeln.
/// Phase 1: YOLO screent alle Frames im Batch (~5ms pro Frame)
/// Phase 2: Nur relevante Frames an DINO+SAM
/// Phase 3: Qwen ×6 parallel (alle 6 Slots gleichzeitig)
/// Ergebnis: ~2-3 Min pro Haltung statt 30+ Min.
/// </summary>
public sealed class BatchPipelineService
{
    private readonly VisionPipelineClient _sidecar;
    private readonly EnhancedVisionAnalysisService _qwen;
    private readonly PipelineConfig _config;
    private readonly string _ffmpegPath;
    private readonly ILogger _logger;

    /// <summary>Batch-Groesse fuer YOLO (Anzahl Frames pro Batch-Request).</summary>
    public int YoloBatchSize { get; set; } = 6;

    /// <summary>Maximale parallele Qwen-Requests.</summary>
    public int QwenParallelism { get; set; } = 6;

    /// <summary>Frame-Intervall in Sekunden.</summary>
    public double FrameStepSeconds { get; set; } = 2.0;

    public BatchPipelineService(
        VisionPipelineClient sidecar,
        EnhancedVisionAnalysisService qwen,
        PipelineConfig config,
        string ffmpegPath,
        ILogger? logger = null)
    {
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _qwen = qwen ?? throw new ArgumentNullException(nameof(qwen));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ffmpegPath = ffmpegPath ?? "ffmpeg";
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// Analysiert ein Video mit der Batch-Pipeline.
    /// Gibt eine Liste von Frame-Analysen zurueck.
    /// </summary>
    public async Task<BatchPipelineResult> AnalyzeVideoAsync(
        string videoPath,
        int pipeDiameterMm = 300,
        IProgress<BatchPipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var allAnalyses = new List<BatchFrameAnalysis>();

        // Video-Dauer ermitteln (via ffprobe)
        var duration = await GetDurationAsync(videoPath, ct).ConfigureAwait(false);

        if (duration <= 0)
        {
            _logger.LogWarning("Video-Dauer konnte nicht ermittelt werden: {Path}", videoPath);
            return new BatchPipelineResult([], TimeSpan.Zero, 0, 0, 0);
        }

        var totalFrames = (int)(duration / FrameStepSeconds) + 1;
        _logger.LogInformation(
            "BatchPipeline: {Video}, {Duration:F0}s, {Frames} Frames (Step={Step}s)",
            Path.GetFileName(videoPath), duration, totalFrames, FrameStepSeconds);

        progress?.Report(new BatchPipelineProgress(0, totalFrames, "Frames extrahieren..."));

        // ── Phase 1: Alle Frames extrahieren ──
        var frames = new List<(int Index, double Timestamp, string Base64)>();
        await using var stream = VideoFrameStream.Open(_ffmpegPath, videoPath, FrameStepSeconds, duration, ct);
        int frameIdx = 0;
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            if (frame.PngBytes is null or { Length: 0 }) continue;
            frames.Add((frameIdx, frame.TimestampSeconds, Convert.ToBase64String(frame.PngBytes)));
            frameIdx++;
        }

        _logger.LogInformation("BatchPipeline: {Count} Frames extrahiert", frames.Count);
        progress?.Report(new BatchPipelineProgress(0, frames.Count, $"{frames.Count} Frames extrahiert, YOLO Batch..."));

        // ── Phase 2: YOLO Batch-Screening ──
        var yoloSw = Stopwatch.StartNew();
        var relevantFrames = new List<(int Index, double Timestamp, string Base64, YoloResponse Yolo)>();
        int skippedFrames = 0;

        for (int batch = 0; batch < frames.Count; batch += YoloBatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = frames.Skip(batch).Take(YoloBatchSize).ToList();

            var batchRequest = new YoloBatchRequestDto(
                chunk.Select(f => new YoloBatchItemDto(f.Base64, f.Index.ToString())).ToList(),
                _config.YoloConfidence);

            YoloBatchResponseDto batchResult;
            try
            {
                batchResult = await _sidecar.DetectYoloBatchAsync(batchRequest, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YOLO Batch fehlgeschlagen — Frames einzeln verarbeiten");
                // Fallback: alle Frames als relevant markieren
                foreach (var f in chunk)
                    relevantFrames.Add((f.Index, f.Timestamp, f.Base64,
                        new YoloResponse(true, Array.Empty<YoloDetectionDto>(), "fallback", 0)));
                continue;
            }

            foreach (var item in batchResult.Results)
            {
                var idx = int.Parse(item.FrameId);
                var original = chunk.First(f => f.Index == idx);

                // BCD/BCE-Zonen und periodischer Sweep immer durchlassen
                var estimatedMeter = original.Timestamp / duration * 100; // grobe Schaetzung
                bool isBcdZone = original.Timestamp < 10 && original.Index <= 5;
                bool isBceZone = original.Timestamp > duration - FrameStepSeconds * 2;
                bool isPeriodicSweep = original.Index % 5 == 0;

                if (item.Result.IsRelevant || isBcdZone || isBceZone || isPeriodicSweep)
                {
                    relevantFrames.Add((original.Index, original.Timestamp, original.Base64, item.Result));
                }
                else
                {
                    skippedFrames++;
                }
            }

            progress?.Report(new BatchPipelineProgress(
                Math.Min(batch + YoloBatchSize, frames.Count), frames.Count,
                $"YOLO: {relevantFrames.Count} relevant, {skippedFrames} uebersprungen"));
        }

        _logger.LogInformation(
            "BatchPipeline YOLO: {Relevant}/{Total} relevant ({Skipped} uebersprungen) in {Ms}ms",
            relevantFrames.Count, frames.Count, skippedFrames, yoloSw.ElapsedMilliseconds);

        // ── Phase 2.5: DINO + SAM fuer relevante Frames ──
        progress?.Report(new BatchPipelineProgress(
            0, relevantFrames.Count,
            $"DINO+SAM: {relevantFrames.Count} Frames segmentieren..."));

        var dinoSamSw = Stopwatch.StartNew();
        var frameContexts = new Dictionary<int, MultiModelFrameResult>();

        foreach (var frame in relevantFrames)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // DINO Grounding
                var dinoResp = await _sidecar.DetectDinoAsync(
                    new DinoRequest(frame.Base64, null, _config.DinoBoxThreshold, _config.DinoTextThreshold), ct)
                    .ConfigureAwait(false);

                if (dinoResp.Detections.Count > 0)
                {
                    // SAM Segmentierung (mit Box-Batching)
                    var samBoxes = dinoResp.Detections.Select(d =>
                        new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence)).ToList();
                    var samResp = await _sidecar.SegmentSamAsync(
                        new SamRequest(frame.Base64, samBoxes,
                            PipeDiameterMm: pipeDiameterMm > 0 ? pipeDiameterMm : null), ct)
                        .ConfigureAwait(false);

                    // Quantifizierung
                    var quantified = MaskQuantificationService.QuantifyAll(samResp, pipeDiameterMm);

                    frameContexts[frame.Index] = new MultiModelFrameResult(
                        frame.Timestamp, null, true,
                        dinoResp.Detections.Select(d => new DinoDetectionDto(
                            d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence, d.Phrase)).ToList(),
                        samResp.Masks,
                        samResp.ImageWidth, samResp.ImageHeight,
                        0, dinoResp.InferenceTimeMs, samResp.InferenceTimeMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DINO/SAM fehlgeschlagen fuer Frame {Frame}", frame.Index);
            }
        }

        _logger.LogInformation("BatchPipeline DINO+SAM: {Count} Frames mit Kontext in {Ms}ms",
            frameContexts.Count, dinoSamSw.ElapsedMilliseconds);

        progress?.Report(new BatchPipelineProgress(
            0, relevantFrames.Count,
            $"Qwen: {relevantFrames.Count} Frames analysieren (2 parallel)..."));

        // ── Phase 3: Qwen 2-fach parallel mit Per-Frame-Timeout ──
        // 6 parallel → Deadlock. 3 parallel = guter Kompromiss.
        var qwenSw = Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(3, 3);
        int qwenDone = 0;
        var analysisResults = new BatchFrameAnalysis[relevantFrames.Count];

        var qwenTasks = relevantFrames.Select(async (frame, i) =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                frameCts.CancelAfter(TimeSpan.FromSeconds(45));

                // YOLO-first: Wenn YOLO Detektionen hat, Qwen ueberspringen
                // YOLO erkennt Klassen direkt → Qwen nur als Fallback wenn nichts gefunden
                EnhancedFrameAnalysis analysis;
                bool hasYoloFindings = frame.Yolo.Detections.Count > 0 && frame.Yolo.IsRelevant;
                bool hasDinoContext = frameContexts.ContainsKey(frame.Index);

                if (hasYoloFindings && hasDinoContext)
                {
                    // YOLO + DINO/SAM Kontext vorhanden → kein Qwen noetig
                    // Erstelle minimale Analyse aus YOLO-Daten
                    var yoloFindings = frame.Yolo.Detections.Select(d => new EnhancedFinding(
                        Label: d.ClassName,
                        VsaCodeHint: d.ClassName,
                        Severity: d.Confidence > 0.7 ? 2 : 1,
                        PositionClock: null,
                        ExtentPercent: null,
                        HeightMm: null, WidthMm: null,
                        IntrusionPercent: null,
                        CrossSectionReductionPercent: null,
                        DiameterReductionMm: null,
                        Notes: $"YOLO direct (conf={d.Confidence:F2})"
                    )).ToList();

                    analysis = new EnhancedFrameAnalysis(
                        Meter: null,
                        PipeMaterial: "unbekannt",
                        PipeDiameterMm: null,
                        Findings: yoloFindings,
                        ImageQuality: "mittel",
                        IsEmptyFrame: false,
                        Error: null,
                        ViewType: "axial");
                }
                else if (frameContexts.TryGetValue(frame.Index, out var ctx))
                {
                    // DINO/SAM Kontext aber kein YOLO → Qwen fragen
                    analysis = await _qwen.AnalyzeWithContextAsync(
                        frame.Base64, ctx, pipeDiameterMm, frameCts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    // Kein Kontext → Qwen als Fallback
                    analysis = await _qwen.AnalyzeAsync(frame.Base64, frameCts.Token)
                        .ConfigureAwait(false);
                }
                var done = Interlocked.Increment(ref qwenDone);
                progress?.Report(new BatchPipelineProgress(
                    done, relevantFrames.Count,
                    hasYoloFindings && hasDinoContext
                        ? $"YOLO direct: {done}/{relevantFrames.Count}"
                        : $"Qwen: {done}/{relevantFrames.Count} Frames analysiert"));

                analysisResults[i] = new BatchFrameAnalysis(
                    frame.Index, frame.Timestamp, analysis,
                    frame.Yolo.Detections.Count);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Qwen Timeout fuer Frame {Frame} — uebersprungen", frame.Index);
                Interlocked.Increment(ref qwenDone);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qwen fehlgeschlagen fuer Frame {Frame}", frame.Index);
                Interlocked.Increment(ref qwenDone);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(qwenTasks).ConfigureAwait(false);

        allAnalyses.AddRange(analysisResults.Where(a => a is not null));
        sw.Stop();

        _logger.LogInformation(
            "BatchPipeline fertig: {Analyses} Analysen, {Duration:F1}s total " +
            "(YOLO={YoloMs}ms, Qwen={QwenMs}ms)",
            allAnalyses.Count, sw.Elapsed.TotalSeconds,
            yoloSw.ElapsedMilliseconds, qwenSw.ElapsedMilliseconds);

        progress?.Report(new BatchPipelineProgress(
            relevantFrames.Count, relevantFrames.Count,
            $"Fertig: {allAnalyses.Count} Analysen in {sw.Elapsed.TotalSeconds:F0}s"));

        return new BatchPipelineResult(
            allAnalyses,
            sw.Elapsed,
            frames.Count,
            relevantFrames.Count,
            skippedFrames);
    }

    private async Task<double> GetDurationAsync(string videoPath, CancellationToken ct)
    {
        try
        {
            var ffprobe = Shared.FfmpegLocator.ResolveFfprobe();
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return 600;
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 600;
        }
        catch { return 600; }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

/// <summary>Ergebnis einer Batch-Pipeline-Analyse.</summary>
public sealed record BatchPipelineResult(
    List<BatchFrameAnalysis> Analyses,
    TimeSpan Duration,
    int TotalFrames,
    int RelevantFrames,
    int SkippedFrames);

/// <summary>Einzelne Frame-Analyse aus der Batch-Pipeline.</summary>
public sealed record BatchFrameAnalysis(
    int FrameIndex,
    double TimestampSeconds,
    EnhancedFrameAnalysis QwenResult,
    int YoloDetectionCount);

/// <summary>Fortschritt der Batch-Pipeline.</summary>
public sealed record BatchPipelineProgress(
    int Done,
    int Total,
    string Status);
