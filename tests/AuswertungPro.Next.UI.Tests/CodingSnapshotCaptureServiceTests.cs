using System.IO;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSnapshotCaptureServiceTests
{
    [Fact]
    public async Task CapturePngAsync_ReturnsBytesAndDeletesTempFile()
    {
        using var temp = new TempDir();
        var bytes = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        var service = new CodingSnapshotCaptureService(
            path =>
            {
                File.WriteAllBytes(path, bytes);
                return true;
            },
            temp.Path,
            TimeSpan.FromMilliseconds(1),
            maxAttempts: 2);

        var result = await service.CapturePngAsync();

        Assert.Equal(bytes, result);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.png"));
    }

    [Fact]
    public async Task CapturePngAsync_WhenCaptureFails_ReturnsNull()
    {
        using var temp = new TempDir();
        var service = new CodingSnapshotCaptureService(
            _ => false,
            temp.Path,
            TimeSpan.FromMilliseconds(1),
            maxAttempts: 2);

        var result = await service.CapturePngAsync();

        Assert.Null(result);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.png"));
    }

    [Fact]
    public async Task CapturePngAsync_WhenCancelledAfterCapture_DeletesTempFile()
    {
        using var temp = new TempDir();
        using var cts = new CancellationTokenSource();
        var service = new CodingSnapshotCaptureService(
            path =>
            {
                File.WriteAllBytes(path, [1, 2, 3]);
                cts.Cancel();
                return true;
            },
            temp.Path,
            TimeSpan.FromMilliseconds(1),
            maxAttempts: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CapturePngAsync(cts.Token));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.png"));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sewer-snapshot-capture-tests",
            Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
