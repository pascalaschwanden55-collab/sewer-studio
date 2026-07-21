using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Kompletter Video-Workflow: Video -> alle SchÃ¤den in einem Durchgang.
///
/// FIXES in dieser Version:
/// - Bug 1.2: AdvanceActiveFindings nutzt jetzt DedupWindowFrames (nicht hardcoded 3)
/// - Bug 1.4: EnhancedVisionAnalysisService eingebunden (detaillierterer Prompt,
///            Uhrzeitlage, Rohrmaterial, vsa_code_hint direkt aus Vision)
/// </summary>
public sealed class VideoFullAnalysisService
{
    // BUG 1.4 FIX: EnhancedVisionAnalysisService statt OllamaVisionFindingsService
    private readonly EnhancedVisionAnalysisService _vision;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly ILogger _logger;
    private readonly IPipelineTraceWriter _pipelineTraceWriter;
    private readonly IProcessOutputReader _processOutputs;
    // Gemeinsame Videodauer-Ermittlung (ffprobe -> ffmpeg-Fallback) mit Timeout + Kill-Baum,
    // identisch zum Multi-Model-Pfad statt eigener Inline-Prozessaufrufe.
    private readonly Training.Services.VideoProbeService _videoProbe;

    public double FrameStepSeconds { get; set; } = 3.0;
    public int DedupWindowFrames { get; set; } = 3;
    public int MinSeverity { get; set; } = 1;
    // Aeusserer Per-Frame-Cap = Standard 120s (#9). Effektiv wirksam ist ohnehin der innere
    // FrameTimeout in EnhancedVisionAnalysisService; frueher 300s und damit faktisch tot.
    public TimeSpan VisionFrameTimeout { get; set; } = TimeSpan.FromSeconds(120);

    public VideoFullAnalysisService(
        EnhancedVisionAnalysisService vision,
        string ffmpegPath = "ffmpeg",
        string? ffprobePath = null,
        ILogger? logger = null,
        IProcessOutputReader? processOutputs = null)
        : this(PipelineTraceWriter.Current, vision, ffmpegPath, ffprobePath, logger, processOutputs)
    {
    }

    public VideoFullAnalysisService(
        IPipelineTraceWriter pipelineTraceWriter,
        EnhancedVisionAnalysisService vision,
        string ffmpegPath = "ffmpeg",
        string? ffprobePath = null,
        ILogger? logger = null,
        IProcessOutputReader? processOutputs = null)
    {
        _pipelineTraceWriter = pipelineTraceWriter ?? throw new ArgumentNullException(nameof(pipelineTraceWriter));
        _vision = vision;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath ?? DeriveFFprobePath(ffmpegPath);
        _logger = logger ?? NullLogger.Instance;
        _processOutputs = processOutputs ?? ProcessOutputReader.Current;
        _videoProbe = new Training.Services.VideoProbeService(
            ffprobePath: _ffprobePath, ffmpegPath: _ffmpegPath, processOutputs: _processOutputs);
    }

    /// <summary>
    /// RÃ¼ckwÃ¤rtskompatibel: Erstellt aus OllamaClient direkt (kein separater Service nÃ¶tig).
    /// </summary>
    public static VideoFullAnalysisService Create(
        OllamaClient client,
        string visionModel,
        string ffmpegPath = "ffmpeg",
        ICodeCatalogProvider? codeCatalog = null,
        ILogger? logger = null)
        => new(new EnhancedVisionAnalysisService(client, visionModel, codeCatalog), ffmpegPath, logger: logger);

    public static VideoFullAnalysisService Create(
        IPipelineTraceWriter pipelineTraceWriter,
        OllamaClient client,
        string visionModel,
        string ffmpegPath = "ffmpeg",
        ICodeCatalogProvider? codeCatalog = null,
        ILogger? logger = null,
        IProcessOutputReader? processOutputs = null)
        => new(
            pipelineTraceWriter,
            new EnhancedVisionAnalysisService(client, visionModel, codeCatalog),
            ffmpegPath,
            logger: logger,
            processOutputs: processOutputs);

    public async Task<VideoAnalysisResult> AnalyzeAsync(
        string videoPath,
        IProgress<VideoAnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Pfad normalisieren
        videoPath = videoPath.Trim();
        if (videoPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            videoPath = new Uri(videoPath).LocalPath;
        videoPath = Path.GetFullPath(videoPath);

        if (!File.Exists(videoPath))
            return VideoAnalysisResult.Failed($"Video nicht gefunden: {videoPath}");

        _lastKnownMeter = 0;
        progress?.Report(new VideoAnalysisProgress(0, 0, "Videodauer wird ermittelt..."));

        var (duration, probeError) = await GetVideoDurationWithErrorAsync(videoPath, ct).ConfigureAwait(false);
        if (duration <= 0)
            return VideoAnalysisResult.Failed($"Videodauer konnte nicht ermittelt werden (ffprobe): {probeError}");

        var totalFrames = (int)Math.Ceiling(duration / FrameStepSeconds);
        // runId zur Korrelation aller Log-Zeilen eines Laufs (wie im Multi-Model-Pfad).
        var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N")[..6];
        _logger.LogInformation(
            "Video-Vollanalyse (Ollama-Only) runId={RunId} gestartet: {Video}, Dauer={Duration:F1}s, ~{Frames} Frames, Step={Step}s",
            runId, Path.GetFileName(videoPath), duration, totalFrames, FrameStepSeconds);
        var detections = new List<RawVideoDetection>();
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = DedupWindowFrames,
            NormalizeFallbackLabels = false,
            NormalizeOutputClock = true,
            MinStretchLengthMeters = 1.0,
            MeterMergeGapMaxMeters = 1.0
        });
        var frameIndex = 0;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames, "Analyse gestartet..."));

        var telemetry = new PipelineTelemetry();

        await using var frameStream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in frameStream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var frameSw = System.Diagnostics.Stopwatch.StartNew();
            frameIndex++;
            var t = frame.TimestampSeconds;

            var extractionMs = frameSw.ElapsedMilliseconds;
            var frameBytes = frame.PngBytes;

            if (frameBytes is null or { Length: 0 })
            {
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                detections.AddRange(deduplicator.AdvanceAll());
                _logger.LogDebug("runId={RunId} Frame {Frame}/{Total}: leeres Bild, uebersprungen", runId, frameIndex, totalFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} â€“ kein Bild"));
                continue;
            }

            progress?.Report(new VideoAnalysisProgress(
                frameIndex,
                totalFrames,
                $"Frame {frameIndex}/{totalFrames} â€“ Bild extrahiert",
                FramePreviewPng: frameBytes));

            EnhancedFrameAnalysis analysis;
            var visionSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                progress?.Report(new VideoAnalysisProgress(
                    frameIndex,
                    totalFrames,
                    $"Frame {frameIndex}/{totalFrames} â€“ KI analysiert Bild...",
                    FramePreviewPng: frameBytes));

                using var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                visionCts.CancelAfter(VisionFrameTimeout);
                analysis = await _vision.AnalyzeAsync(
                    Convert.ToBase64String(frameBytes), visionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("runId={RunId} Frame {Frame}/{Total}: Timeout bei KI-Analyse nach {Timeout:0}s",
                    runId, frameIndex, totalFrames, VisionFrameTimeout.TotalSeconds);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} â€“ Timeout bei KI-Analyse ({VisionFrameTimeout.TotalSeconds:0}s)"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, visionSw.ElapsedMilliseconds, frameSw.ElapsedMilliseconds, Skipped: true));
                detections.AddRange(deduplicator.AdvanceAll());
                continue;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "runId={RunId} Frame {Frame}/{Total}: Fehler bei KI-Analyse", runId, frameIndex, totalFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} â€“ Fehler: {ex.Message}"));
                telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, visionSw.ElapsedMilliseconds, frameSw.ElapsedMilliseconds, Skipped: true));
                detections.AddRange(deduplicator.AdvanceAll());
                continue;
            }
            var qwenMs = visionSw.ElapsedMilliseconds;

            telemetry.RecordFrame(new FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

            var meterWasEstimated = !analysis.Meter.HasValue;
            var meter = analysis.Meter ?? EstimateMeter(t, duration);
            // Always update _lastKnownMeter so EstimateMeter doesn't stagnate at 0.01
            _lastKnownMeter = meter;

            var current = (analysis.Findings ?? Array.Empty<EnhancedFinding>())
                .Where(f => !string.IsNullOrWhiteSpace(f.Label) && f.Severity >= MinSeverity)
                .ToList();

            var liveFindings = current
                .Select(f => new LiveFrameFinding(
                    Label: f.Label.Trim(),
                    Severity: f.Severity,
                    PositionClock: f.PositionClock,
                    ExtentPercent: f.ExtentPercent,
                    VsaCodeHint: f.VsaCodeHint,
                    HeightMm: f.HeightMm,
                    WidthMm: f.WidthMm,
                    IntrusionPercent: f.IntrusionPercent,
                    CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                    DiameterReductionMm: f.DiameterReductionMm))
                .ToList();

            detections.AddRange(deduplicator.Update(
                current,
                meter,
                meterSource: meterWasEstimated ? "LinearEstimate" : "Analysis",
                isMeterEstimated: meterWasEstimated));

            progress?.Report(new VideoAnalysisProgress(
                frameIndex,
                totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m â€“ {current.Count} Befunde",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));
        }

        detections.AddRange(deduplicator.Flush());

        _logger.LogInformation(
            "Video-Vollanalyse runId={RunId} fertig: {Count} Befunde aus {Frames} analysierten Frames",
            runId, detections.Count, frameIndex);
        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"Fertig â€“ {detections.Count} SchÃ¤den erkannt."));

        var summary = telemetry.GetSummary();
        await PipelineTraceWriteGuard
            .WriteSummaryAsync(_pipelineTraceWriter, runId, summary)
            .ConfigureAwait(false);

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Dauer + Fehler — delegiert an den gemeinsamen VideoProbeService (ffprobe -> ffmpeg-Fallback),
    // der die Prozessaufrufe ueber den ProcessOutputReader mit Timeout und Kill-Baum absichert.
    private async Task<(double duration, string error)> GetVideoDurationWithErrorAsync(string videoPath, CancellationToken ct)
    {
        var result = await _videoProbe.ProbeAsync(videoPath, ct).ConfigureAwait(false);
        return (result.DurationSeconds, result.Success ? "" : result.Error);
    }

    private double _lastKnownMeter;

    private double EstimateMeter(double t, double dur)
    {
        // SchÃ¤tze Meter-Inkrement basierend auf Zeitfortschritt.
        // Annahme: konstante Kamerageschwindigkeit Ã¼ber die gesamte Haltung.
        if (dur <= 0)
            return Math.Round(_lastKnownMeter + 0.01, 2);

        // Wenn noch kein Meter bekannt, schÃ¤tze ~0.1m/s Kamerageschwindigkeit als Default
        var estimatedPipeLength = _lastKnownMeter > 0 ? _lastKnownMeter * (dur / Math.Max(t, 1.0)) : dur * 0.1;
        var step = FrameStepSeconds / dur * estimatedPipeLength;
        return Math.Round(_lastKnownMeter + Math.Max(step, 0.01), 2);
    }

    // Delegiert an gemeinsamen Helfer in FfmpegLocator (verhaltensneutral).
    private static string DeriveFFprobePath(string ffmpegPath) =>
        FfmpegLocator.DeriveFfprobeFrom(ffmpegPath);
}

// â”€â”€ DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€


