using System.Diagnostics;
using System.Globalization;

namespace SidecarE2eSmoke;

public static class FfmpegFrameExtractor
{
    public static async Task<IReadOnlyList<ExtractedFrame>> ExtractAsync(
        SidecarSmokeOptions options,
        CancellationToken ct)
    {
        if (options.ImagePath is not null)
        {
            var bytes = await File.ReadAllBytesAsync(options.ImagePath, ct);
            return [new ExtractedFrame(1, 0, bytes)];
        }

        var count = options.FullPipeline ? options.FrameCount : 1;
        var frames = new List<ExtractedFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var timestamp = options.VideoSecond + i * options.FrameStepSeconds;
            Console.WriteLine($"Frame {i + 1}/{count} bei {timestamp:0.###} s dekodieren...");
            var bytes = await ExtractOneAsync(options.FfmpegPath, options.VideoPath!, timestamp, ct);
            frames.Add(new ExtractedFrame(i + 1, timestamp, bytes));
        }

        return frames;
    }

    private static async Task<byte[]> ExtractOneAsync(
        string ffmpegPath,
        string videoPath,
        double timestamp,
        CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sewer-sidecar-e2e-{Guid.NewGuid():N}.jpg");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(timestamp.ToString("0.###", CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(videoPath);
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-q:v");
            startInfo.ArgumentList.Add("2");
            startInfo.ArgumentList.Add(tempPath);

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
            using var cancellation = ct.Register(() => TryKill(process));
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(ct);
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg ExitCode {process.ExitCode}: {stderr.Trim()}");
            if (!File.Exists(tempPath))
                throw new FileNotFoundException($"ffmpeg hat bei {timestamp:0.###} s kein Bild erzeugt.", tempPath);

            return await File.ReadAllBytesAsync(tempPath, ct);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Temp-Aufraeumen darf das Testergebnis nicht verdecken.
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Der Hauptpfad meldet den Abbruch.
        }
    }
}
