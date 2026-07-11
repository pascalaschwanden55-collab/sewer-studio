using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Haeufig genutzte VSA-Codes fuer die Favoriten-Chips im Code-Explorer.</summary>
public sealed class CodeUsageTrackerTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(
        Path.GetTempPath(), $"code-usage-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void TopCodes_ranks_by_frequency()
    {
        var tracker = new CodeUsageTracker(_tempFile);
        tracker.Erfasse("BAB");
        tracker.Erfasse("BCA");
        tracker.Erfasse("BAB");
        tracker.Erfasse("BAB");
        tracker.Erfasse("BCA");
        tracker.Erfasse("BBC");

        var top = tracker.TopCodes(2);
        Assert.Equal(new[] { "BAB", "BCA" }, top.Select(t => t.Code).ToArray());
        Assert.Equal(3, top[0].Anzahl);
    }

    [Fact]
    public void Erfasse_ignores_empty_codes()
    {
        var tracker = new CodeUsageTracker(_tempFile);
        tracker.Erfasse("");
        tracker.Erfasse("   ");
        tracker.Erfasse(null);
        Assert.Empty(tracker.TopCodes(5));
    }

    [Fact]
    public void Usage_survives_reload_from_disk()
    {
        var tracker = new CodeUsageTracker(_tempFile);
        tracker.Erfasse("BAJ");
        tracker.Erfasse("BAJ");
        tracker.Erfasse("BDD");

        var neuGeladen = new CodeUsageTracker(_tempFile);
        var top = neuGeladen.TopCodes(5);
        Assert.Equal("BAJ", top[0].Code);
        Assert.Equal(2, top[0].Anzahl);
        Assert.Equal(2, top.Count);
    }

    [Fact]
    public void Zuletzt_returns_most_recent_distinct_codes_first()
    {
        var tracker = new CodeUsageTracker(_tempFile);
        tracker.Erfasse("BAB");
        tracker.Erfasse("BCA");
        tracker.Erfasse("BBC");
        tracker.Erfasse("BCA"); // erneut -> rueckt nach vorn

        Assert.Equal(new[] { "BCA", "BBC", "BAB" }, tracker.Zuletzt(3).ToArray());
    }

    [Fact]
    public void Corrupt_file_is_tolerated()
    {
        File.WriteAllText(_tempFile, "kein json {{{");
        var tracker = new CodeUsageTracker(_tempFile);
        Assert.Empty(tracker.TopCodes(3));
        tracker.Erfasse("BAB"); // und ab jetzt normal weiter
        Assert.Single(tracker.TopCodes(3));
    }
}
