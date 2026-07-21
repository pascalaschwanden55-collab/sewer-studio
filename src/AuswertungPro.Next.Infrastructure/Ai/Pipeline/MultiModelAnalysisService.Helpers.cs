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
}
