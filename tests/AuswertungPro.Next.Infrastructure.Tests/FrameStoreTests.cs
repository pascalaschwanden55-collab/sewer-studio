using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FrameStoreTests
{
    private static byte[] ValidPng()
        => Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task FileStore_verwendet_injizierte_Extraktion_und_schreibt_Png()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        var calls = 0;
        var png = ValidPng();
        var store = new TrainingFrameFileStore((ffmpeg, video, at, ct) =>
        {
            calls++;
            Assert.Equal("ffmpeg-test", ffmpeg);
            Assert.Equal("video-test.mp4", video);
            Assert.Equal(TimeSpan.FromSeconds(3.5), at);
            Assert.False(ct.IsCancellationRequested);
            return Task.FromResult<byte[]?>(png);
        });

        try
        {
            var result = await store.ExtractAndStoreAsync(
                "ffmpeg-test",
                "video-test.mp4",
                3.5,
                "sample/23",
                directory);

            Assert.Equal(Path.Combine(directory, "sample_23.png"), result);
            Assert.Equal(png, await File.ReadAllBytesAsync(result!));
            Assert.Equal(1, calls);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void GetFramesDir_creates_and_returns_custom_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(directory, FrameStore.GetFramesDir(directory));
            Assert.True(Directory.Exists(directory));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ExtractAndStoreAsync_reuses_existing_frame_without_ffmpeg()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var expected = Path.Combine(directory, "sample_1.png");
        var png = ValidPng();
        await File.WriteAllBytesAsync(expected, png);

        try
        {
            var result = await FrameStore.ExtractAndStoreAsync(
                "ffmpeg-nicht-vorhanden",
                "video-nicht-vorhanden.mp4",
                2.0,
                "sample/1",
                directory);

            Assert.Equal(expected, result);
            Assert.Equal(png, await File.ReadAllBytesAsync(expected));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task ExtractAndStoreAsync_ersetzt_unvollstaendigen_Altframe_atomar()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var expected = Path.Combine(directory, "sample_2.png");
        await File.WriteAllBytesAsync(expected, [1, 2, 3]);
        var png = ValidPng();
        var calls = 0;
        var store = new TrainingFrameFileStore((_, _, _, _) =>
        {
            calls++;
            return Task.FromResult<byte[]?>(png);
        });

        try
        {
            var result = await store.ExtractAndStoreAsync(
                "ffmpeg-test",
                "video-test.mp4",
                2.0,
                "sample/2",
                directory);

            Assert.Equal(expected, result);
            Assert.Equal(png, await File.ReadAllBytesAsync(expected));
            Assert.Equal(1, calls);
            Assert.DoesNotContain(
                Directory.GetFiles(directory),
                path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task StoreExistingAsync_kopiert_Original_unveraendert_in_den_Goldordner()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        var sourceDir = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
        var goldDir = Path.Combine(root, "gold_frames");
        var source = Path.Combine(sourceDir, "riss.jpg");
        var bytes = new byte[] { 1, 3, 3, 7 };
        await File.WriteAllBytesAsync(source, bytes);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var store = new TrainingFrameFileStore();

        try
        {
            var first = await store.StoreExistingAsync(source, goldDir);
            var second = await store.StoreExistingAsync(source, goldDir);

            Assert.Equal(Path.Combine(goldDir, $"gold_{hash}.jpg"), first);
            Assert.Equal(first, second);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(source));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(first!));
            Assert.Single(Directory.GetFiles(goldDir));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task StoreBytesAsync_speichert_Png_inhaltsadressiert_und_wiederholbar()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        var goldDir = Path.Combine(root, "gold_frames");
        var bytes = new byte[] { 9, 8, 7, 6 };
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var store = new TrainingFrameFileStore();

        try
        {
            var first = await store.StoreBytesAsync(bytes, "PNG", goldDir);
            var second = await store.StoreBytesAsync(bytes, ".png", goldDir);

            Assert.Equal(Path.Combine(goldDir, $"gold_{hash}.png"), first);
            Assert.Equal(first, second);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(first!));
            Assert.Single(Directory.GetFiles(goldDir));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Theory]
    // Verschachtelte CaseId mit '/' (und '.') wird zu einem flachen, sicheren Datei-Stamm.
    [InlineData("st_06.24341-35625/20250602_x_000", "st_06_24341-35625_20250602_x_000")]
    [InlineData("a\\b", "a_b")]
    [InlineData("a:b*c?", "a_b_c_")]
    [InlineData("mit Leerzeichen / und \\ und : Zeichen", "mit_Leerzeichen___und___und___Zeichen")]
    [InlineData("normal-id_001", "normal-id_001")] // bereits sicher -> unveraendert
    public void SanitizeFileStem_macht_pfadsichere_Dateinamen(string raw, string expected)
    {
        Assert.Equal(expected, FrameStore.SanitizeFileStem(raw));
    }

    [Fact]
    public void SanitizeFileStem_leerer_Input_ergibt_Fallback()
    {
        Assert.Equal("frame", FrameStore.SanitizeFileStem(""));
    }
}
