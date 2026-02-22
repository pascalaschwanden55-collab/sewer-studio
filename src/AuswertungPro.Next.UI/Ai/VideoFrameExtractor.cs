using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

public static class VideoFrameExtractor
{
    /// <summary>
    /// Extrahiert ein einzelnes PNG-Frame aus einem Video bei einer Zeitposition.
    /// Benötigt ffmpeg im PATH oder als absoluter Pfad.
    /// </summary>
    public static async Task<byte[]?> TryExtractFramePngAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan at,
        CancellationToken ct)
    {
        if (!File.Exists(videoPath))
            return null;

        var outPng = Path.Combine(Path.GetTempPath(), $"auswertungpro_frame_{Guid.NewGuid():N}.png");

        // -ss vor -i ist schneller
        var args = $"-hide_banner -loglevel error -ss {at.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} -i \"{videoPath}\" -frames:v 1 -vf scale='min(1280,iw)':-2 -y \"{outPng}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            using var p = Process.Start(psi);
            if (p == null)
                return null;

            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            if (p.ExitCode != 0)
            {
                // optional: string err = await p.StandardError.ReadToEndAsync();
                return null;
            }

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
