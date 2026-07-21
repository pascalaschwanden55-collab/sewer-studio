using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FileContentComparerTests
{
    [Fact]
    public void FilesEqual_GleicherInhalt_LiefertTrue()
    {
        using var dir = new TempDir();
        var a = dir.Write("a.bin", "identischer inhalt 12345");
        var b = dir.Write("b.bin", "identischer inhalt 12345");

        Assert.True(FileContentComparer.FilesEqual(a, b));
    }

    [Fact]
    public void FilesEqual_UnterschiedlicheLaenge_LiefertFalse()
    {
        using var dir = new TempDir();
        var a = dir.Write("a.bin", "kurz");
        var b = dir.Write("b.bin", "deutlich laenger als kurz");

        Assert.False(FileContentComparer.FilesEqual(a, b));
    }

    [Fact]
    public void FilesEqual_GleicheLaengeAndererInhalt_LiefertFalse()
    {
        using var dir = new TempDir();
        var a = dir.Write("a.bin", "AAAAAAAA");
        var b = dir.Write("b.bin", "AAAAAAAB");

        Assert.False(FileContentComparer.FilesEqual(a, b));
    }

    private sealed class TempDir : IDisposable
    {
        private readonly string _path =
            Path.Combine(Path.GetTempPath(), "file-content-comparer-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(_path);

        public string Write(string name, string content)
        {
            var full = Path.Combine(_path, name);
            File.WriteAllText(full, content);
            return full;
        }

        public void Dispose()
        {
            try { Directory.Delete(_path, recursive: true); } catch { }
        }
    }
}
