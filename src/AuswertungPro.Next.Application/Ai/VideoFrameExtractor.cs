using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai;

public static class VideoFrameExtractor
{
    /// <summary>
    /// Extrahiert ein einzelnes PNG-Frame aus einem Video bei einer Zeitposition.
    /// Benötigt ffmpeg im PATH oder als absoluter Pfad.
    /// </summary>
    /// <summary>Max Wartezeit fuer ffmpeg pro Frame (Sekunden).</summary>
    private const int FfmpegTimeoutSeconds = 30;

    public static async Task<byte[]?> TryExtractFramePngAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan at,
        CancellationToken ct)
    {
        if (!File.Exists(videoPath))
            return null;

        var outPng = Path.Combine(Path.GetTempPath(), $"auswertungpro_frame_{Guid.NewGuid():N}.png");

        try
        {
            // Phase D2.3: ProcessRunner — sicherer ArgumentList + asynchroner Drain
            // + harter Timeout via Tree-Kill. Loest STAB-H1 (Pipe-Deadlocks) und
            // SEC-H1 (Command-Injection) zentral.
            var result = await ProcessRunner.RunAsync(
                fileName: ffmpegPath,
                arguments: ["-hide_banner",
                            "-loglevel", "error",
                            "-ss", at.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "-i", videoPath,
                            "-frames:v", "1",
                            "-vf", "scale='min(1280,iw)':-2",
                            "-y", outPng],
                timeout: TimeSpan.FromSeconds(FfmpegTimeoutSeconds),
                ct: ct).ConfigureAwait(false);

            if (result.TimedOut)
            {
                Debug.WriteLine($"[VideoFrameExtractor] ffmpeg Timeout nach {FfmpegTimeoutSeconds}s bei {videoPath} @ {at.TotalSeconds:F1}s");
                return null;
            }

            if (!result.IsSuccess)
                return null;

            if (!File.Exists(outPng))
                return null;

            return await File.ReadAllBytesAsync(outPng, ct).ConfigureAwait(false);
        }
        finally
        {
            try { if (File.Exists(outPng)) File.Delete(outPng); } catch { /* ignore */ }
        }
    }
}
