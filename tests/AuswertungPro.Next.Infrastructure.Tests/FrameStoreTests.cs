using System.IO;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FrameStoreTests
{
    [Fact]
    public async Task FileStore_verwendet_injizierte_Extraktion_und_schreibt_Png()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sewer-frame-store-tests", Guid.NewGuid().ToString("N"));
        var calls = 0;
        var store = new TrainingFrameFileStore((ffmpeg, video, at, ct) =>
        {
            calls++;
            Assert.Equal("ffmpeg-test", ffmpeg);
            Assert.Equal("video-test.mp4", video);
            Assert.Equal(TimeSpan.FromSeconds(3.5), at);
            Assert.False(ct.IsCancellationRequested);
            return Task.FromResult<byte[]?>([4, 5, 6]);
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
            Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(result!));
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
        await File.WriteAllBytesAsync(expected, [1, 2, 3]);

        try
        {
            var result = await FrameStore.ExtractAndStoreAsync(
                "ffmpeg-nicht-vorhanden",
                "video-nicht-vorhanden.mp4",
                2.0,
                "sample/1",
                directory);

            Assert.Equal(expected, result);
            Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(expected));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
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
