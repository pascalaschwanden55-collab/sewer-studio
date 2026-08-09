using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Schneidet mit ffmpeg einen kurzen Clip aus dem Originalvideo (nur lesend).
/// Dieselbe Parameterwahl wie das Prototypskript des Bogen-Copiloten: h264,
/// veryfast, +faststart — kurze Datei, sofort abspielbar.
///
/// Strenge wie beim Bildfolgen-Extraktor, weil diese Haerten dort schon einen
/// defekten Videostumpf gefangen haben: Die Zieldatei wird frisch erzeugt (eine
/// vorhandene waere ein Fehler), die ffmpeg-Fehlerausgabe wird woertlich
/// durchgereicht, und ein Lauf ohne Ergebnis ist ein Fehler, kein leerer Clip.
/// </summary>
public sealed class VideoClipExtractionService : IVideoClipExtractor
{
    private readonly IProcessOutputReader _processOutputs;

    public VideoClipExtractionService(IProcessOutputReader processOutputs)
    {
        _processOutputs = processOutputs ?? throw new ArgumentNullException(nameof(processOutputs));
    }

    public async Task<string> CutClipAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan from,
        TimeSpan to,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(videoPath))
            throw new FileNotFoundException("Das Video wurde nicht gefunden.", videoPath);
        if (!File.Exists(ffmpegPath))
            throw new FileNotFoundException("ffmpeg wurde nicht gefunden.", ffmpegPath);

        var von = Math.Max(0.0, from.TotalSeconds);
        var dauer = Math.Max(1.0, to.TotalSeconds - from.TotalSeconds);
        var outClip = Path.Combine(
            Path.GetTempPath(),
            $"auswertungpro_clip_{Guid.NewGuid():N}.mp4");
        if (File.Exists(outClip))
        {
            // Praktisch unmoeglich (GUID), aber fail-closed wie der leere
            // Zielordner des Bildfolgen-Extraktors: Nie eine Alt-Datei als
            // Ergebnis unterschieben.
            throw new InvalidOperationException(
                $"Die Zieldatei existiert bereits und wird nicht ueberschrieben: {outClip}");
        }

        var args = string.Join(' ',
            "-hide_banner -loglevel error",
            $"-ss {von.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"-i \"{videoPath}\"",
            $"-t {dauer.ToString("0.00", CultureInfo.InvariantCulture)}",
            "-an -c:v libx264 -preset veryfast -crf 23",
            "-pix_fmt yuv420p -movflags +faststart -y",
            $"\"{outClip}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        ProcessOutputResult? output;
        try
        {
            output = await _processOutputs
                .ReadToExitAsync(startInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryDelete(outClip);
            throw;
        }

        if (output is null || output.ExitCode != 0)
        {
            TryDelete(outClip);
            var detail = output is null || string.IsNullOrWhiteSpace(output.StandardError)
                ? $"Rueckgabewert {output?.ExitCode.ToString() ?? "unbekannt"}"
                : output.StandardError.Trim();
            throw new InvalidOperationException($"ffmpeg ist fehlgeschlagen: {detail}");
        }

        if (!File.Exists(outClip) || new FileInfo(outClip).Length == 0)
        {
            TryDelete(outClip);
            throw new InvalidOperationException(
                "ffmpeg hat keinen Clip erzeugt. Das Video ist vermutlich defekt "
                + "oder der Bereich liegt ausserhalb.");
        }

        return outClip;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Temp-Aufraeumen bleibt Best Effort.
        }
    }
}
