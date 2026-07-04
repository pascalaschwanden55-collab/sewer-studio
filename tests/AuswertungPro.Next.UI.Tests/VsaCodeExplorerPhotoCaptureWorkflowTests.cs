using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoCaptureWorkflowTests
{
    [Fact]
    public async Task CaptureAsync_nutzt_existierenden_live_snapshot_und_setzt_fotoslot()
    {
        var photoPaths = new List<string>();
        var ffmpegCalls = 0;
        var extractCalls = 0;
        var writeCalls = 0;

        var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureAsync(
            new VsaCodeExplorerPhotoCaptureRequest(
                PhotoIndex: 0,
                PhotoPaths: photoPaths,
                LiveSnapshotProvider: () => "snapshot.png",
                VideoPath: "video.mp4",
                CurrentVideoTime: TimeSpan.FromSeconds(12),
                TimeText: "",
                FileExists: path => path == "snapshot.png",
                ResolveFfmpeg: () =>
                {
                    ffmpegCalls++;
                    return "ffmpeg.exe";
                },
                ExtractFramePngAsync: (_, _, _, _) =>
                {
                    extractCalls++;
                    return Task.FromResult<byte[]?>([1, 2, 3]);
                },
                CreateTempPhotoPath: _ => "temp.png",
                WriteAllBytesAsync: (_, _, _) =>
                {
                    writeCalls++;
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None));

        Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.Captured, result.Outcome);
        Assert.Equal("snapshot.png", result.PhotoPath);
        Assert.Equal(["snapshot.png"], photoPaths);
        Assert.Equal(0, ffmpegCalls);
        Assert.Equal(0, extractCalls);
        Assert.Equal(0, writeCalls);
    }

    [Fact]
    public async Task CaptureAsync_meldet_fehlendes_video_wenn_fallback_nicht_moeglich_ist()
    {
        var photoPaths = new List<string>();

        var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureAsync(
            new VsaCodeExplorerPhotoCaptureRequest(
                PhotoIndex: 0,
                PhotoPaths: photoPaths,
                LiveSnapshotProvider: () => null,
                VideoPath: "missing.mp4",
                CurrentVideoTime: null,
                TimeText: "",
                FileExists: _ => false,
                ResolveFfmpeg: () => throw new InvalidOperationException("ffmpeg should not be resolved"),
                ExtractFramePngAsync: (_, _, _, _) => throw new InvalidOperationException("frame should not be extracted"),
                CreateTempPhotoPath: _ => "temp.png",
                WriteAllBytesAsync: (_, _, _) => throw new InvalidOperationException("file should not be written"),
                CancellationToken: CancellationToken.None));

        Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.MissingVideo, result.Outcome);
        Assert.Null(result.PhotoPath);
        Assert.Equal("Kein Video geladen.", result.Message);
        Assert.Equal("Foto", result.Title);
        Assert.Empty(photoPaths);
    }

    [Fact]
    public async Task CaptureAsync_extrahiert_frame_mit_geparster_zeit_und_speichert_tempfoto()
    {
        var photoPaths = new List<string> { "first.png" };
        TimeSpan? extractedAt = null;
        byte[]? writtenBytes = null;
        string? writtenPath = null;

        var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureAsync(
            new VsaCodeExplorerPhotoCaptureRequest(
                PhotoIndex: 1,
                PhotoPaths: photoPaths,
                LiveSnapshotProvider: null,
                VideoPath: "video.mp4",
                CurrentVideoTime: TimeSpan.FromSeconds(5),
                TimeText: "01:02",
                FileExists: path => path == "video.mp4",
                ResolveFfmpeg: () => "ffmpeg.exe",
                ExtractFramePngAsync: (ffmpeg, video, at, _) =>
                {
                    Assert.Equal("ffmpeg.exe", ffmpeg);
                    Assert.Equal("video.mp4", video);
                    extractedAt = at;
                    return Task.FromResult<byte[]?>([4, 5, 6]);
                },
                CreateTempPhotoPath: photoIndex => $"temp{photoIndex}.png",
                WriteAllBytesAsync: (path, bytes, _) =>
                {
                    writtenPath = path;
                    writtenBytes = bytes;
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None));

        Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.Captured, result.Outcome);
        Assert.Equal("temp1.png", result.PhotoPath);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(2), extractedAt);
        Assert.Equal("temp1.png", writtenPath);
        Assert.Equal([4, 5, 6], writtenBytes);
        Assert.Equal(["first.png", "temp1.png"], photoPaths);
    }

    [Fact]
    public async Task CaptureAsync_meldet_extraktionsfehler_ohne_fotoslot_aenderung()
    {
        var photoPaths = new List<string> { "first.png" };

        var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureAsync(
            new VsaCodeExplorerPhotoCaptureRequest(
                PhotoIndex: 1,
                PhotoPaths: photoPaths,
                LiveSnapshotProvider: null,
                VideoPath: "video.mp4",
                CurrentVideoTime: TimeSpan.FromSeconds(5),
                TimeText: "ungueltig",
                FileExists: path => path == "video.mp4",
                ResolveFfmpeg: () => "ffmpeg.exe",
                ExtractFramePngAsync: (_, _, _, _) => Task.FromResult<byte[]?>([]),
                CreateTempPhotoPath: _ => "temp.png",
                WriteAllBytesAsync: (_, _, _) => throw new InvalidOperationException("file should not be written"),
                CancellationToken: CancellationToken.None));

        Assert.Equal(VsaCodeExplorerPhotoCaptureOutcome.ExtractionFailed, result.Outcome);
        Assert.Null(result.PhotoPath);
        Assert.Equal("Frame-Extraktion fehlgeschlagen.", result.Message);
        Assert.Equal("Foto", result.Title);
        Assert.Equal(["first.png"], photoPaths);
    }
}
