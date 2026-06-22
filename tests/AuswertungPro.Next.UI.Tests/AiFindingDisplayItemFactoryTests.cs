using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiFindingDisplayItemFactoryTests
{
    [Fact]
    public void ForPossibleBoundary_builds_single_possible_boundary_item()
    {
        var items = AiFindingDisplayItemFactory.ForPossibleBoundary("BCE", "Rohrende");

        var item = Assert.Single(items);
        Assert.StartsWith("M", item.Label, StringComparison.Ordinal);
        Assert.EndsWith("Rohrende", item.Label, StringComparison.Ordinal);
        Assert.Equal("BCE", item.VsaCode);
        Assert.Equal(3, item.Severity);
    }

    [Fact]
    public void ForBoundary_builds_single_boundary_item()
    {
        var items = AiFindingDisplayItemFactory.ForBoundary("BCD", "Rohranfang");

        var item = Assert.Single(items);
        Assert.Equal("Rohranfang", item.Label);
        Assert.Equal("BCD", item.VsaCode);
        Assert.Equal(4, item.Severity);
    }

    [Fact]
    public void ForResolvedFinding_overwrites_vsa_hint_for_display()
    {
        var finding = new LiveFrameFinding(
            Label: "Rohrende",
            Severity: 4,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: "BCA");

        var items = AiFindingDisplayItemFactory.ForResolvedFinding(finding, "BCE");

        var item = Assert.Single(items);
        Assert.Equal("Rohrende", item.Label);
        Assert.Equal("BCE", item.VsaCode);
        Assert.Equal(4, item.Severity);
    }
}
