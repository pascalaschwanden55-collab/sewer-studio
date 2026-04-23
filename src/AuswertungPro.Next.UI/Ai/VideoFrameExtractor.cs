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

        // -ss vor -i ist schneller. ArgumentList.Add statt Arguments-String:
        // verhindert Command-Injection bei Pfaden mit Anfuehrungszeichen.
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(at.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add("scale='min(1280,iw)':-2");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add(outPng);

        try
        {
            using var p = Process.Start(psi);
            if (p == null)
                return null;

            // Timeout: ffmpeg darf max 30s pro Frame brauchen
            // Manche MPG-Videos blockieren ffmpeg endlos
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(FfmpegTimeoutSeconds));

            try
            {
                await p.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // ffmpeg-Timeout (nicht Batch-Abbruch) → Prozess killen
                try { p.Kill(entireProcessTree: true); } catch { }
                Debug.WriteLine($"[VideoFrameExtractor] ffmpeg Timeout nach {FfmpegTimeoutSeconds}s bei {videoPath} @ {at.TotalSeconds:F1}s");
                return null;
            }

            if (p.ExitCode != 0)
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
