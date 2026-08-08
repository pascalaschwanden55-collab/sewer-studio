using System.Diagnostics;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>Ergebnis eines ffmpeg-Laufs: Rueckgabewert und Fehlerausgabe.</summary>
public sealed record ProcessRunResult(int ExitCode, string StandardError);

/// <summary>
/// Fuehrt genau einen ffmpeg-Durchgang aus und liefert die erzeugten Bilder mit
/// ihrer Videozeit. Der Prozessaufruf ist als Naht injizierbar, damit die Regeln
/// ohne installiertes ffmpeg pruefbar bleiben.
///
/// Bewusst streng: Ein nicht leerer Zielordner, ein Fehlschlag von ffmpeg oder ein
/// Lauf ohne ein einziges Bild sind Fehler. Ein defektes Video liefert stumm null
/// Bilder — das darf niemals als "keine Befunde" durchgehen.
/// </summary>
public sealed class VideoFrameSequenceExtractor : IVideoFrameSequenceExtractor
{
    private readonly Func<string, string, CancellationToken, Task<ProcessRunResult>> _runProcess;

    public VideoFrameSequenceExtractor()
        : this(RunFfmpegAsync)
    {
    }

    internal VideoFrameSequenceExtractor(
        Func<string, string, CancellationToken, Task<ProcessRunResult>> runProcess)
    {
        _runProcess = runProcess ?? throw new ArgumentNullException(nameof(runProcess));
    }

    public async Task<IReadOnlyList<VideoSequenceFrame>> ExtractAsync(
        VideoFrameSequenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.VideoPath))
            throw new FileNotFoundException("Das Video wurde nicht gefunden.", request.VideoPath);
        if (!File.Exists(request.FfmpegPath))
            throw new FileNotFoundException("ffmpeg wurde nicht gefunden.", request.FfmpegPath);

        EnsureTargetIsEmpty(request.TargetDirectory);
        Directory.CreateDirectory(request.TargetDirectory);

        var arguments = VideoFrameSequenceLayout.BuildArguments(
            request.VideoPath, request.TargetDirectory, request.FramesPerSecond);
        var result = await _runProcess(request.FfmpegPath, arguments, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"Rueckgabewert {result.ExitCode}"
                : result.StandardError.Trim();
            throw new InvalidOperationException($"ffmpeg ist fehlgeschlagen: {detail}");
        }

        var frames = ReadFrames(request);
        if (frames.Count == 0)
        {
            throw new InvalidOperationException(
                "ffmpeg hat kein Bild erzeugt. Das Video ist vermutlich defekt oder abgebrochen.");
        }

        return frames;
    }

    private static void EnsureTargetIsEmpty(string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        if (!Directory.Exists(targetDirectory))
            return;
        if (Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            throw new InvalidOperationException(
                $"Der Zielordner muss leer sein, damit keine Bilder eines frueheren Laufs "
                + $"mitgezaehlt werden: {targetDirectory}");
        }
    }

    private static List<VideoSequenceFrame> ReadFrames(VideoFrameSequenceRequest request)
    {
        var frames = new List<VideoSequenceFrame>();
        foreach (var path in Directory.EnumerateFiles(request.TargetDirectory))
        {
            // Fremde Dateien (Protokolle, Vorschauen) werden uebergangen.
            if (VideoFrameSequenceLayout.TryParseIndex(Path.GetFileName(path)) is not { } index)
                continue;

            frames.Add(new VideoSequenceFrame(
                index,
                VideoFrameSequenceLayout.TimeSecondsFor(index, request.FramesPerSecond),
                path));
        }

        frames.Sort((left, right) => left.Index.CompareTo(right.Index));
        return frames;
    }

    private static async Task<ProcessRunResult> RunFfmpegAsync(
        string ffmpegPath,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(ffmpegPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"ffmpeg konnte nicht gestartet werden: {ffmpegPath}");
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken)
            .ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessRunResult(process.ExitCode, standardError);
    }
}
