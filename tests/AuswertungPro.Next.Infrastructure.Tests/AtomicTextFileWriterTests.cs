using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AtomicTextFileWriterTests
{
    [Fact]
    public void WriteAllText_ReplacesFileAndKeepsBackup()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "data.json");

        AtomicTextFileWriter.WriteAllText(path, "old");
        AtomicTextFileWriter.WriteAllText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal("old", File.ReadAllText(path + ".bak"));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task WriteAllTextAsync_WhenCancelled_DoesNotLeaveTempFile()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "data.json");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AtomicTextFileWriter.WriteAllTextAsync(path, "new", cts.Token));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "atomic-text-writer-tests",
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
