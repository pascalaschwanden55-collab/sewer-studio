using System.Globalization;
using System.IO;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.VsaFotos;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai.Vsa;

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
    CancellationToken CancellationToken,
    IList<string>? OriginalPhotoPaths = null,
    Func<string, int, string>? PersistPhoto = null,
    Func<string, bool>? IstTempPfad = null);

/// <param name="NurVorlaeufig">
/// Das Foto liegt im Temp-Ordner (Deepscan 02.10.2026, R3); <see cref="Message"/> nennt den Grund.
/// </param>
public sealed record VsaCodeExplorerPhotoCaptureResult(
    VsaCodeExplorerPhotoCaptureOutcome Outcome,
    string? PhotoPath,
    string Message,
    string Title,
    bool NurVorlaeufig = false);

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
        => CaptureWithDefaultsAsync(
            photoIndex,
            photoPaths,
            photoPaths,
            liveSnapshotProvider,
            videoPath,
            currentVideoTime,
            timeText,
            cancellationToken);

    public static Task<VsaCodeExplorerPhotoCaptureResult> CaptureWithDefaultsAsync(
        int photoIndex,
        IList<string> photoPaths,
        IList<string> originalPhotoPaths,
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
                CancellationToken: cancellationToken,
                OriginalPhotoPaths: originalPhotoPaths,
                PersistPhoto: (quelle, index) =>
                    VsaFotoAblage.Uebernehme(quelle, videoPath, index)));

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
            return Captured(
                request,
                Uebernehme(request, liveSnapshotPath));

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

        return Captured(
            request,
            Uebernehme(request, tempPhotoPath));
    }

    /// <summary>
    /// Bringt das gerade aufgenommene Bild an seinen dauerhaften Ort. Ohne
    /// gesetzten Weg bleibt der Pfad unveraendert; die Aufrufer der
    /// Standardfassung legen ihn immer neben das Video.
    /// </summary>
    private static string Uebernehme(
        VsaCodeExplorerPhotoCaptureRequest request,
        string quelle)
        => request.PersistPhoto is null
            ? quelle
            : request.PersistPhoto(quelle, request.PhotoIndex);

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
        VsaCodeExplorerPhotoCaptureRequest request,
        string photoPath)
    {
        SetPhotoSlot(request.PhotoPaths, request.PhotoIndex, photoPath);
        SetPhotoSlot(request.OriginalPhotoPaths ?? request.PhotoPaths, request.PhotoIndex, photoPath);

        // Deepscan 02.10.2026, R3: Liegt das Foto noch im Temp-Ordner, erfaehrt der Nutzer es
        // sofort und mit Grund, statt erst nach dem naechsten Aufraeumen von Windows.
        var hinweis = VsaFotoTempHinweis.Fuer(
            photoPath,
            request.VideoPath,
            request.IstTempPfad ?? BefundfotoTempOrt.LiegtImTemp);
        return new VsaCodeExplorerPhotoCaptureResult(
            VsaCodeExplorerPhotoCaptureOutcome.Captured,
            photoPath,
            Message: hinweis ?? "",
            Title: hinweis is null ? "" : VsaFotoTempHinweis.Titel,
            NurVorlaeufig: hinweis is not null);
    }

    private static void SetPhotoSlot(IList<string> photoPaths, int photoIndex, string photoPath)
    {
        while (photoPaths.Count <= photoIndex)
            photoPaths.Add("");

        photoPaths[photoIndex] = photoPath;
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
