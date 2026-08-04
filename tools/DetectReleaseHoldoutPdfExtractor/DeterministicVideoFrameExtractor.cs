using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace DetectReleaseHoldoutPdfExtractor;

internal static class DeterministicVideoFrameExtractor
{
    private static readonly double[] AllowedFractions = [0.25, 0.5, 0.75];

    public static async Task<PreparedImage> ExtractAsync(
        string? videoPathValue,
        double? fractionValue,
        string holdingKey,
        string? configuredFfmpeg,
        string? configuredFfprobe,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoPathValue) || fractionValue is null)
            throw new InvalidDataException("video_path und background_fraction müssen gemeinsam gesetzt sein.");
        var fraction = fractionValue.Value;
        if (!AllowedFractions.Any(allowed => Math.Abs(allowed - fraction) < 0.0000001))
            throw new InvalidDataException("background_fraction muss 0.25, 0.5 oder 0.75 sein.");

        var videoPath = PathSafety.RequireExistingFile(videoPathValue, "Video");
        var before = SafeFiles.ReadFileIdentity(videoPath);
        var ffmpeg = ResolveExecutable(configuredFfmpeg, FfmpegLocator.ResolveFfmpeg, "ffmpeg");
        var ffprobe = ResolveExecutable(
            configuredFfprobe,
            () => string.IsNullOrWhiteSpace(configuredFfmpeg)
                ? FfmpegLocator.ResolveFfprobe()
                : FfmpegLocator.DeriveFfprobeFrom(ffmpeg),
            "ffprobe");

        using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeTimeout.CancelAfter(TimeSpan.FromSeconds(60));
        var probe = await new VideoProbeService(ffprobe, ffmpeg)
            .ProbeAsync(videoPath, probeTimeout.Token)
            .ConfigureAwait(false);
        if (!probe.Success || !double.IsFinite(probe.DurationSeconds) || probe.DurationSeconds <= 0)
            throw new InvalidDataException(probe.Error.Length == 0 ? "Die Videodauer ist ungültig." : probe.Error);
        var timestamp = Math.Min(
            probe.DurationSeconds * fraction,
            Math.Max(0, probe.DurationSeconds - 0.001));

        var tempRoot = Path.Combine(Path.GetTempPath(), "SewerStudioDetectEvalExtractor");
        Directory.CreateDirectory(tempRoot);
        PathSafety.RequireNoReparsePoints(tempRoot, "temporärer Videoordner");
        var tempPath = Path.Combine(tempRoot, $"frame_{Guid.NewGuid():N}.png");
        try
        {
            await RunFfmpegAsync(ffmpeg, videoPath, timestamp, tempPath, cancellationToken)
                .ConfigureAwait(false);
            var after = SafeFiles.ReadFileIdentity(videoPath);
            if (before.Length != after.Length || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                throw new IOException("Das Video wurde während der Frame-Extraktion verändert.");

            var bytes = await SafeFiles.ReadAllBytesLimitedAsync(
                    tempPath,
                    100 * 1024 * 1024,
                    cancellationToken)
                .ConfigureAwait(false);
            var header = ImageHeaders.Read(bytes);
            if (!string.Equals(header.Extension, ".png", StringComparison.Ordinal))
                throw new InvalidDataException("ffmpeg hat kein PNG-Bild erzeugt.");
            var sha = Hashing.Sha256(bytes);
            return new PreparedImage(
                bytes,
                ".png",
                sha,
                header.Width,
                header.Height,
                holdingKey,
                HoldingKeys.Physical(holdingKey),
                "deterministic_video_frame",
                SourcePdfName: null,
                SourcePdfSha256: null,
                References: [],
                Video: new VideoSource(
                    Path.GetFileName(videoPath),
                    fraction,
                    timestamp,
                    before.Length,
                    before.LastWriteTimeUtc));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string ResolveExecutable(
        string? configured,
        Func<string> fallback,
        string label)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback() : configured.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new FileNotFoundException($"{label} wurde nicht gefunden.");
        if (Path.IsPathRooted(value))
            return PathSafety.RequireExistingFile(value, label);
        if (value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new InvalidDataException($"Der relative {label}-Pfad ist nicht zulässig.");
        return value;
    }

    private static async Task RunFfmpegAsync(
        string ffmpeg,
        string videoPath,
        double timestamp,
        string targetPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-ss");
        startInfo.ArgumentList.Add(timestamp.ToString("0.######", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("png");
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Der ursprüngliche Abbruch- oder Prozessfehler bleibt maßgebend.
            }

            throw;
        }

        var standardError = await stderr.ConfigureAwait(false);
        _ = await stdout.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffmpeg endete mit Code {process.ExitCode}: {SafeFiles.TrimMessage(standardError)}");
        if (!File.Exists(targetPath))
            throw new FileNotFoundException("ffmpeg hat keinen Frame erzeugt.");
    }
}
