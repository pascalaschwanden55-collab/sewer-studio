using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VideoStartErrorLogFileWriterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "VideoStartErrorLogFileWriterTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryWrite_schreibt_ueber_die_injizierte_Instanz()
    {
        var now = new DateTime(2026, 7, 14, 16, 30, 45, DateTimeKind.Local);
        var writer = new VideoStartErrorLogFileWriter(_tempDirectory, () => now);

        var logPath = writer.TryWrite(
            new InvalidOperationException("Player fehlt"),
            @"D:\Videos\haltung-42.mp4");

        Assert.Equal(
            Path.Combine(_tempDirectory, "logs", "video_start_error_20260714_163045_haltung-42.txt"),
            logPath);
        var content = File.ReadAllText(logPath!);
        Assert.Contains($"Time: {now:O}", content);
        Assert.Contains(@"VideoPath: D:\Videos\haltung-42.mp4", content);
        Assert.Contains("Player fehlt", content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
