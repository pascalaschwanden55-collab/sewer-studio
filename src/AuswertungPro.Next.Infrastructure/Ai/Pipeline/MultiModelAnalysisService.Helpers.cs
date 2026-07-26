using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Microsoft.Extensions.Logging;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

// Ausgelagerte, gekapselte Hilfsmethoden der Multi-Model-Pipeline (reiner Move aus
// MultiModelAnalysisService.cs, kein Verhaltensunterschied) — haelt die Hauptdatei unter dem
// 1000-Zeilen-Deckel, damit der Frame-Loop Raum fuer neue Logik behaelt.
public sealed partial class MultiModelAnalysisService
{
    public double FrameStepSeconds { get; set; } = 3.0;
    public int DedupWindowFrames { get; set; } = 3;

    // Aeusserer Per-Frame-Qwen-Cap = Standard 120s (#9). Der effektiv wirksame Cap
    // bleibt der innere FrameTimeout in EnhancedVisionAnalysisService.
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>YOLO-cls Vorfilter aktivieren/deaktivieren (Fallback: aus wenn kein Modell).</summary>
    public bool UseClsPrefilter { get; set; } = true;

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Standard-Frame-Quelle: oeffnet einen VideoFrameStream und gibt seine Frames zurueck.
    /// Als separater Helper, damit der await-using-Dispose korrekt ablaeuft.
    /// </summary>
    private static async IAsyncEnumerable<FrameData> DefaultFrameSource(
        string ffmpegPath,
        string videoPath,
        double stepSeconds,
        double duration,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = VideoFrameStream.Open(ffmpegPath, videoPath, stepSeconds, duration, ct);
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
            yield return frame;
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var result = await _videoProbe.ProbeAsync(videoPath, ct).ConfigureAwait(false);
        if (result.Success)
            return result.DurationSeconds;

        _logger.LogWarning("Videodauer konnte nicht ermittelt werden: {Error}", result.Error);
        return 0;
    }

    // Delegiert an gemeinsamen Helfer in FfmpegLocator (verhaltensneutral).
    private static string DeriveFfprobePath(string ffmpegPath) =>
        FfmpegLocator.DeriveFfprobeFrom(ffmpegPath);

    /// <summary>
    /// Normalisiert Clock-Positionen — delegiert an kanonische Implementierung in VsaCodeResolver.
    /// </summary>
    private static string? NormalizeClockPosition(string? clock) =>
        VsaCodeResolver.NormalizeClock(clock);

    private static bool CanUseClassifierDecision(YoloClassifyResponse cls)
        => cls.ClassifierLoaded && !cls.BendVetoFailed;

    private static void MarkTraceDegraded(PipelineFrameTrace trace, string reason)
    {
        trace.Degraded = true;
        if (string.IsNullOrWhiteSpace(trace.DegradedReason))
        {
            trace.DegradedReason = reason;
            return;
        }

        if (!trace.DegradedReason.Contains(reason, StringComparison.OrdinalIgnoreCase))
            trace.DegradedReason += $";{reason}";
    }

    /// <summary>
    /// Liest die Detektor-Qualifikation aus der Sidecar-Gesundheit.
    /// Null bei Fehler oder altem Sidecar bleibt bewusst "nicht freigegeben".
    /// </summary>
    private async Task<SidecarDetectorQualification?> ReadDetectorQualificationAsync(CancellationToken ct)
    {
        try
        {
            var health = await _client.HealthCheckAsync(ct).ConfigureAwait(false);
            return health?.DetectorQualification;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Modell-Tag fuer den Trace: Name + Kurz-Hash aus der Sidecar-Response.</summary>
    private static string? ClassifierModelTag(YoloClassifyResponse? cls)
    {
        if (cls is null || string.IsNullOrEmpty(cls.ModelName))
            return null;
        var sha = cls.ModelSha256;
        return string.IsNullOrEmpty(sha) ? cls.ModelName : $"{cls.ModelName}@{sha[..Math.Min(12, sha.Length)]}";
    }

    /// <summary>
    /// Convert a MultiModelFrameResult to EnhancedFrameAnalysis
    /// (for compatibility with the existing pipeline).
    /// </summary>
    public static EnhancedFrameAnalysis ToEnhancedAnalysis(
        MultiModelFrameResult result,
        int pipeDiameterMm)
        => MultiModelFrameAnalysisMapper.Map(result, pipeDiameterMm);

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
    private Task WriteTraceAsync(PipelineFrameTrace trace)
        => PipelineTraceWriteGuard.WriteAsync(
            _pipelineTraceWriter,
            PipelineTraceEntryMapper.Map(trace));

    public double EstimatedReachLengthM { get; set; } = 50.0; // Typisch 15-80m, Fallback 50m

    private double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Lineare Schaetzung basierend auf geschaetzter Haltungslaenge (wird durch Qwen OSD korrigiert)
        var estimated = t / Math.Max(duration, 1.0) * EstimatedReachLengthM;
        lastMeter = Math.Max(lastMeter, estimated);
        return Math.Round(lastMeter, 2);
    }
}
