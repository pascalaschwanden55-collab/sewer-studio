using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionFrameCaptureServiceTests
{
    [Fact]
    public async Task CaptureAsync_returns_null_without_snapshot_when_player_is_unavailable()
    {
        var takeSnapshotCalled = false;
        var service = new LiveDetectionFrameCaptureService(
            (_, _) =>
            {
                takeSnapshotCalled = true;
                return true;
            },
            () => @"C:\Temp\sewer_live_test.png",
            (_, _) => Task.CompletedTask,
            _ => true,
            (_, _) => Task.FromResult(Array.Empty<byte>()),
            _ => { });

        var result = await service.CaptureAsync(() => true, CancellationToken.None);

        Assert.Null(result);
        Assert.False(takeSnapshotCalled);
    }

    [Fact]
    public async Task CaptureAsync_reads_bytes_and_deletes_temp_file_when_snapshot_succeeds()
    {
        string? snapshotPath = null;
        string? deletedPath = null;
        var expected = new byte[] { 1, 2, 3 };
        var service = new LiveDetectionFrameCaptureService(
            (path, width) =>
            {
                snapshotPath = $"{path}|{width}";
                return true;
            },
            () => @"C:\Temp\sewer_live_test.png",
            (_, _) => Task.CompletedTask,
            path => path.EndsWith("sewer_live_test.png", StringComparison.Ordinal),
            (_, _) => Task.FromResult(expected),
            path => deletedPath = path);

        var result = await service.CaptureAsync(() => false, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(@"C:\Temp\sewer_live_test.png|640", snapshotPath);
        Assert.Equal(@"C:\Temp\sewer_live_test.png", deletedPath);
    }

    [Fact]
    public async Task CaptureAsync_returns_null_when_snapshot_file_is_missing()
    {
        var readCalled = false;
        var service = new LiveDetectionFrameCaptureService(
            (_, _) => true,
            () => @"C:\Temp\missing.png",
            (_, _) => Task.CompletedTask,
            _ => false,
            (_, _) =>
            {
                readCalled = true;
                return Task.FromResult(Array.Empty<byte>());
            },
            _ => { });

        var result = await service.CaptureAsync(() => false, CancellationToken.None);

        Assert.Null(result);
        Assert.False(readCalled);
    }
}
