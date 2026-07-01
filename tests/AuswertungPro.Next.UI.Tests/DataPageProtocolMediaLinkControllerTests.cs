using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolMediaLinkControllerTests
{
    [Fact]
    public void ResolveEntry_prefers_tag_over_datacontext()
    {
        var tagEntry = new ProtocolEntry { Code = "TAG" };
        var dataEntry = new ProtocolEntry { Code = "DATA" };

        var result = DataPageProtocolMediaLinkController.ResolveEntry(tagEntry, dataEntry);

        Assert.Same(tagEntry, result);
    }

    [Fact]
    public void ResolveTargetTime_uses_explicit_time_before_mpeg_text()
    {
        var entry = new ProtocolEntry
        {
            Zeit = TimeSpan.FromSeconds(12),
            Mpeg = "00:01:23"
        };

        var result = DataPageProtocolMediaLinkController.ResolveTargetTime(entry);

        Assert.Equal(TimeSpan.FromSeconds(12), result);
    }

    [Theory]
    [InlineData("01:23", 83)]
    [InlineData("00:01:23", 83)]
    [InlineData("01:23.500", 83.5)]
    public void ResolveTargetTime_parses_mpeg_text(string mpeg, double expectedSeconds)
    {
        var entry = new ProtocolEntry { Mpeg = mpeg };

        var result = DataPageProtocolMediaLinkController.ResolveTargetTime(entry);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Fact]
    public void BuildOverlayText_combines_code_description_and_stretch_meter_range()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAJ",
            Beschreibung = "Riss",
            MeterStart = 1.2,
            MeterEnd = 2.4,
            IsStreckenschaden = true
        };

        var result = DataPageProtocolMediaLinkController.BuildOverlayText(entry);

        Assert.Equal("BAJ | Riss | Strecke 1.20 - 2.40 m", result);
    }
}
