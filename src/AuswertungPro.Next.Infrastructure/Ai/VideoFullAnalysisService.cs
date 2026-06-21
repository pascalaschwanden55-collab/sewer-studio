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
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
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
        ILogger? logger = null)
    {
        _vision = vision;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath ?? DeriveFFprobePath(ffmpegPath);
        _logger = logger ?? NullLogger.Instance;
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
        await PipelineTraceWriter.WriteSummaryAsync(runId, summary).ConfigureAwait(false);

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Dauer + Fehler
    private async Task<(double duration, string error)> GetVideoDurationWithErrorAsync(string videoPath, CancellationToken ct)
    {
        var probe = ResolveFfprobe(_ffmpegPath, _ffprobePath);
        if (probe is not null)
        {
            try
            {
                var (dur, err) = await TryWithFfprobeWithErrorAsync(probe, videoPath, ct);
                if (dur is not null && dur > 0)
                    return (dur.Value, "");
                // ffprobe gestartet aber kein Ergebnis â†’ ffmpeg-Fallback versuchen
            }
            catch (OperationCanceledException) { throw; }
            catch { /* ffprobe nicht gefunden oder Fehler â†’ ffmpeg-Fallback */ }
        }

        var fallback = await TryWithFfmpegAsync(_ffmpegPath, videoPath, ct);
        return (fallback ?? 0, fallback == null
            ? $"Videodauer konnte nicht ermittelt werden. Bitte ffmpeg/ffprobe im PATH oder per Env SEWERSTUDIO_FFMPEG konfigurieren."
            : "");
    }

    // Neue Methode: ffprobe mit Fehlerausgabe
    private static async Task<(double? duration, string error)> TryWithFfprobeWithErrorAsync(string ffprobeExe, string videoPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffprobeExe,
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
            var output = await ProcessOutputReader.ReadToExitAsync(psi, ct).ConfigureAwait(false);
            if (output is null) return (null, "Process.Start failed");

            var stdout = output.StandardOutput;
            var stderr = output.StandardError;

            if (output.ExitCode != 0)
                return (null, string.IsNullOrWhiteSpace(stderr) ? $"ExitCode {output.ExitCode}" : stderr);

            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return (dur, "");

            return (null, $"stdout: '{stdout.Trim()}', stderr: '{stderr.Trim()}'");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return (null, ex.Message); }
    }

    private static string? ResolveFfprobe(string ffmpegPath, string? ffprobePath)
    {
        // Absoluter Pfad zu ffprobe â†’ direkt nutzen
        if (!string.IsNullOrWhiteSpace(ffprobePath) && File.Exists(ffprobePath))
            return ffprobePath;

        // Absoluter Pfad zu ffmpeg â†’ ffprobe.exe daneben suchen
        if (!string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
        {
            var dir = Path.GetDirectoryName(ffmpegPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var candidate = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        // PATH-basierter Name (z.B. "ffprobe" oder "ffmpeg") â†’ als Fallback direkt verwenden
        if (!string.IsNullOrWhiteSpace(ffprobePath))
            return ffprobePath;

        // Aus ffmpeg-Name "ffprobe" ableiten (z.B. "ffmpeg" â†’ "ffprobe")
        if (!string.IsNullOrWhiteSpace(ffmpegPath))
        {
            var derived = DeriveFFprobePath(ffmpegPath);
            if (!string.IsNullOrWhiteSpace(derived))
                return derived;
        }

        return null;
    }

    private static async Task<double?> TryWithFfmpegAsync(string ffmpegExe, string videoPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ffmpegExe))
            return null;
        // Nur bei absolutem Pfad File.Exists prÃ¼fen; PATH-Namen ("ffmpeg") direkt verwenden
        if (Path.IsPathRooted(ffmpegExe) && !File.Exists(ffmpegExe))
            return null;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            UseShellExecute = false,
            RedirectStandardError = true,  // Duration steht in stderr
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return null;

            var text = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            var m = System.Text.RegularExpressions.Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return null;

            var h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return h * 3600 + min * 60 + s;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
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

    private static string DeriveFFprobePath(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) ||
            string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";
        var dir = Path.GetDirectoryName(ffmpegPath);
        var ext = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(dir) ? "ffprobe" + ext : Path.Combine(dir, "ffprobe" + ext);
    }
}

// â”€â”€ DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€


