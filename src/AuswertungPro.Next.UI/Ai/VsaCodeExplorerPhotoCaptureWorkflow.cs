using System.Globalization;
using System.IO;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai;

public enum VsaCodeExplorerPhotoCaptureOutcome
{
    Captured,
    MissingVideo,
    ExtractionFailed
}

public sealed record VsaCodeExplorerPhotoCaptureRequest(
    int PhotoIndex,
    IList<string> PhotoPaths,
    Func<string?>? LiveSnapshotProvider,
    string? VideoPath,
    TimeSpan? CurrentVideoTime,
    string? TimeText,
    Func<string, bool> FileExists,
    Func<string> ResolveFfmpeg,
    Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> ExtractFramePngAsync,
    Func<int, string> CreateTempPhotoPath,
    Func<string, byte[], CancellationToken, Task> WriteAllBytesAsync,
    CancellationToken CancellationToken);

public sealed record VsaCodeExplorerPhotoCaptureResult(
    VsaCodeExplorerPhotoCaptureOutcome Outcome,
    string? PhotoPath,
    string Message,
    string Title);

public static class VsaCodeExplorerPhotoCaptureWorkflow
{
    public static Task<VsaCodeExplorerPhotoCaptureResult> CaptureWithDefaultsAsync(
        int photoIndex,
        IList<string> photoPaths,
        Func<string?>? liveSnapshotProvider,
        string? videoPath,
        TimeSpan? currentVideoTime,
        string? timeText,
        CancellationToken cancellationToken)
        => CaptureAsync(
            new VsaCodeExplorerPhotoCaptureRequest(
                PhotoIndex: photoIndex,
                PhotoPaths: photoPaths,
                LiveSnapshotProvider: liveSnapshotProvider,
                VideoPath: videoPath,
                CurrentVideoTime: currentVideoTime,
                TimeText: timeText,
                FileExists: File.Exists,
                ResolveFfmpeg: FfmpegLocator.ResolveFfmpeg,
                ExtractFramePngAsync: VideoFrameExtractor.TryExtractFramePngAsync,
                CreateTempPhotoPath: CreateTempPhotoPath,
                WriteAllBytesAsync: File.WriteAllBytesAsync,
                CancellationToken: cancellationToken));

    public static async Task<VsaCodeExplorerPhotoCaptureResult> CaptureAsync(
        VsaCodeExplorerPhotoCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.PhotoPaths);
        ArgumentNullException.ThrowIfNull(request.FileExists);
        ArgumentNullException.ThrowIfNull(request.ResolveFfmpeg);
        ArgumentNullException.ThrowIfNull(request.ExtractFramePngAsync);
        ArgumentNullException.ThrowIfNull(request.CreateTempPhotoPath);
        ArgumentNullException.ThrowIfNull(request.WriteAllBytesAsync);

        var liveSnapshotPath = request.LiveSnapshotProvider?.Invoke();
        if (!string.IsNullOrEmpty(liveSnapshotPath) && request.FileExists(liveSnapshotPath))
            return Captured(request.PhotoPaths, request.PhotoIndex, liveSnapshotPath);

        if (string.IsNullOrWhiteSpace(request.VideoPath) || !request.FileExists(request.VideoPath))
            return MissingVideo();

        var ffmpeg = request.ResolveFfmpeg();
        var captureTime = ResolveCaptureTime(request.CurrentVideoTime, request.TimeText);
        var bytes = await request.ExtractFramePngAsync(
            ffmpeg,
            request.VideoPath,
            captureTime,
            request.CancellationToken).ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
            return ExtractionFailed();

        var tempPhotoPath = request.CreateTempPhotoPath(request.PhotoIndex);
        await request.WriteAllBytesAsync(
            tempPhotoPath,
            bytes,
            request.CancellationToken).ConfigureAwait(false);

        return Captured(request.PhotoPaths, request.PhotoIndex, tempPhotoPath);
    }

    private static TimeSpan ResolveCaptureTime(TimeSpan? currentVideoTime, string? timeText)
    {
        var captureTime = currentVideoTime ?? TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(timeText))
            return captureTime;

        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss" };
        return TimeSpan.TryParseExact(
            timeText.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : captureTime;
    }

    private static VsaCodeExplorerPhotoCaptureResult Captured(
        IList<string> photoPaths,
        int photoIndex,
        string photoPath)
    {
        while (photoPaths.Count <= photoIndex)
            photoPaths.Add("");

        photoPaths[photoIndex] = photoPath;
        return new VsaCodeExplorerPhotoCaptureResult(
            VsaCodeExplorerPhotoCaptureOutcome.Captured,
            photoPath,
            Message: "",
            Title: "");
    }

    private static VsaCodeExplorerPhotoCaptureResult MissingVideo()
        => new(
            VsaCodeExplorerPhotoCaptureOutcome.MissingVideo,
            PhotoPath: null,
            Message: "Kein Video geladen.",
            Title: "Foto");

    private static VsaCodeExplorerPhotoCaptureResult ExtractionFailed()
        => new(
            VsaCodeExplorerPhotoCaptureOutcome.ExtractionFailed,
            PhotoPath: null,
            Message: "Frame-Extraktion fehlgeschlagen.",
            Title: "Foto");

    private static string CreateTempPhotoPath(int photoIndex)
        => Path.Combine(
            Path.GetTempPath(),
            $"vsa_foto{photoIndex + 1}_{Guid.NewGuid():N}.png");
}
