using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Integration")]
public class CrashLogServiceTests : IDisposable
{
    private readonly string _tempDir;

    public CrashLogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"crashlog_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void List_NoLogsDir_ReturnsEmpty()
    {
        var svc = new CrashLogService(() => Path.Combine(_tempDir, "no-such-dir"));
        Assert.Empty(svc.List());
    }

    [Fact]
    public void List_OrdersByLastWriteDescending()
    {
        var f1 = Path.Combine(_tempDir, "crash-20260101_120000.log");
        var f2 = Path.Combine(_tempDir, "crash-20260201_120000.log");
        var f3 = Path.Combine(_tempDir, "crash-20260301_120000.log");
        File.WriteAllText(f1, "old");
        File.WriteAllText(f2, "mid");
        File.WriteAllText(f3, "new");
        File.SetLastWriteTimeUtc(f1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(f2, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(f3, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var svc = new CrashLogService(() => _tempDir);
        var entries = svc.List();

        Assert.Equal(3, entries.Count);
        Assert.Equal("crash-20260301_120000.log", entries[0].Name);
        Assert.Equal("crash-20260201_120000.log", entries[1].Name);
        Assert.Equal("crash-20260101_120000.log", entries[2].Name);
    }

    [Fact]
    public void List_IgnoresNonCrashFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "crash-1.log"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "app-2026.log"), "y");
        File.WriteAllText(Path.Combine(_tempDir, "maintenance-2026.log"), "z");

        var svc = new CrashLogService(() => _tempDir);
        var entries = svc.List();

        Assert.Single(entries);
        Assert.Equal("crash-1.log", entries[0].Name);
    }

    [Fact]
    public void Prune_KeepsYoungestRegardlessOfAge()
    {
        // 5 Crash-Logs, alle 60 Tage alt
        for (int i = 0; i < 5; i++)
        {
            var p = Path.Combine(_tempDir, $"crash-{i:D2}.log");
            File.WriteAllText(p, "x");
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddDays(-60).AddSeconds(i));
        }

        var svc = new CrashLogService(() => _tempDir);
        var result = svc.Prune(keepCount: 3, keepDays: 30);

        // Trotz 60 Tagen alt: 3 juengste behalten
        Assert.Equal(2, result.Deleted);
        Assert.Equal(3, Directory.GetFiles(_tempDir, "crash-*.log").Length);
    }

    [Fact]
    public void Prune_KeepsAllWhenYoung()
    {
        for (int i = 0; i < 5; i++)
        {
            var p = Path.Combine(_tempDir, $"crash-{i:D2}.log");
            File.WriteAllText(p, "x");
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddDays(-1)); // alle 1 Tag alt
        }

        var svc = new CrashLogService(() => _tempDir);
        var result = svc.Prune(keepCount: 10, keepDays: 30);

        Assert.Equal(0, result.Deleted);
        Assert.Equal(5, Directory.GetFiles(_tempDir, "crash-*.log").Length);
    }
}
