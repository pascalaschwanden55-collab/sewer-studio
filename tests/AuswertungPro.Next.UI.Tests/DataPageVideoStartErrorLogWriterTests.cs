using System;
using System.IO;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoStartErrorLogWriterTests
{
    [Fact]
    public void TryWrite_creates_log_file_with_stable_name_and_content()
    {
        using var temp = new TempDir();
        var now = new DateTime(2026, 6, 21, 12, 34, 56, DateTimeKind.Local);
        var exception = new InvalidOperationException("native side missing");
        var videoPath = Path.Combine(temp.Path, "haltung-01.mp4");

        var logPath = DataPageVideoStartErrorLogWriter.TryWrite(exception, videoPath, temp.Path, now);

        Assert.NotNull(logPath);
        Assert.Equal(
            Path.Combine(temp.Path, "logs", "video_start_error_20260621_123456_haltung-01.txt"),
            logPath);
        var content = File.ReadAllText(logPath!);
        Assert.Contains($"Time: {now:O}", content);
        Assert.Contains($"VideoPath: {videoPath}", content);
        Assert.Contains("native side missing", content);
    }

    [Fact]
    public void TryWrite_uses_video_fallback_name_when_path_has_no_filename()
    {
        using var temp = new TempDir();
        var now = new DateTime(2026, 6, 21, 12, 34, 56);

        var logPath = DataPageVideoStartErrorLogWriter.TryWrite(
            new Exception("boom"),
            videoPath: "",
            baseDirectory: temp.Path,
            now: now);

        Assert.Equal(
            Path.Combine(temp.Path, "logs", "video_start_error_20260621_123456_video.txt"),
            logPath);
    }

    [Fact]
    public void TryWrite_returns_null_when_log_directory_cannot_be_created()
    {
        using var temp = new TempDir();
        var blockedBasePath = Path.Combine(temp.Path, "keine_dateiablage");
        File.WriteAllText(blockedBasePath, "blockiert");

        var logPath = DataPageVideoStartErrorLogWriter.TryWrite(
            new Exception("boom"),
            videoPath: "haltung-01.mp4",
            baseDirectory: blockedBasePath,
            now: new DateTime(2026, 6, 21, 12, 34, 56));

        Assert.Null(logPath);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssv_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
