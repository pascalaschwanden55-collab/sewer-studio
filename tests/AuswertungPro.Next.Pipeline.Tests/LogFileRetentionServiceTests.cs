using System;
using System.IO;
using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class LogFileRetentionServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sewerstudio-log-retention-" + Guid.NewGuid().ToString("N"));

    public LogFileRetentionServiceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Test cleanup best effort.
        }
    }

    [Fact]
    public void Apply_deletes_only_old_app_logs()
    {
        var now = new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
        var oldAppLog = CreateLog("app-20260401.log", now.AddDays(-45));
        var freshAppLog = CreateLog("app-20260524.log", now.AddDays(-1));
        var oldNonAppLog = CreateLog("selftraining_errors.log", now.AddDays(-45));
        var oldBackup = CreateLog("app-20260401.log.bak", now.AddDays(-45));

        var result = LogFileRetentionService.Apply(_dir, TimeSpan.FromDays(30), now);

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Deleted);
        Assert.Equal(0, result.Failed);
        Assert.False(File.Exists(oldAppLog));
        Assert.True(File.Exists(freshAppLog));
        Assert.True(File.Exists(oldNonAppLog));
        Assert.True(File.Exists(oldBackup));
    }

    [Fact]
    public void Apply_missing_directory_is_noop()
    {
        var missing = Path.Combine(_dir, "missing");

        var result = LogFileRetentionService.Apply(missing, TimeSpan.FromDays(30), DateTimeOffset.UtcNow);

        Assert.Equal(0, result.Scanned);
        Assert.Equal(0, result.Deleted);
        Assert.Equal(0, result.Failed);
    }

    private string CreateLog(string name, DateTimeOffset lastWriteUtc)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "test");
        File.SetLastWriteTimeUtc(path, lastWriteUtc.UtcDateTime);
        return path;
    }
}
