using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HaltungSummaryFormatterTests
{
    [Fact]
    public void FormatSummary_joins_all_parts()
    {
        Assert.Equal(
            "DN 300 · 45.30 m · Mischabwasser",
            HaltungSummaryFormatter.FormatSummary("300", "45.30", "Mischabwasser"));
    }

    [Fact]
    public void FormatSummary_skips_empty_parts()
    {
        Assert.Equal("DN 300 · Mischabwasser",
            HaltungSummaryFormatter.FormatSummary("300", "", "Mischabwasser"));
        Assert.Equal("45.30 m",
            HaltungSummaryFormatter.FormatSummary("  ", "45.30", null));
    }

    [Fact]
    public void FormatSummary_returns_empty_when_nothing_present()
    {
        Assert.Equal(string.Empty, HaltungSummaryFormatter.FormatSummary(null, null, null));
    }
}
