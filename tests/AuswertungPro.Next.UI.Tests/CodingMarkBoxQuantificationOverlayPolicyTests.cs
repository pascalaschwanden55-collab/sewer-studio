using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMarkBoxQuantificationOverlayPolicyTests
{
    [Fact]
    public void Apply_writes_height_width_and_clock_to_overlay()
    {
        var overlay = new OverlayGeometry();
        var quant = Quant(heightMm: 12, widthMm: 4, clock: "3.5");

        CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, quant);

        Assert.Equal(12, overlay.Q1Mm);
        Assert.Equal(4, overlay.Q2Mm);
        Assert.Equal(3.5, overlay.ClockFrom);
    }

    [Fact]
    public void Apply_prefers_cross_section_percent_over_extent_percent()
    {
        var overlay = new OverlayGeometry();
        var quant = Quant(extentPercent: 25, crossSectionPercent: 40);

        CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, quant);

        Assert.Equal(40, overlay.FillPercent);
    }

    [Fact]
    public void Apply_uses_extent_percent_when_cross_section_is_missing()
    {
        var overlay = new OverlayGeometry();
        var quant = Quant(extentPercent: 25);

        CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, quant);

        Assert.Equal(25, overlay.FillPercent);
    }

    [Fact]
    public void Apply_ignores_unparseable_clock_position()
    {
        var overlay = new OverlayGeometry { ClockFrom = 6 };
        var quant = Quant(clock: "3:00");

        CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, quant);

        Assert.Equal(6, overlay.ClockFrom);
    }

    private static MaskQuantificationService.QuantifiedMask Quant(
        int? heightMm = null,
        int? widthMm = null,
        int? extentPercent = null,
        int? crossSectionPercent = null,
        string? clock = null)
        => new(
            Label: "mark",
            Confidence: 0.9,
            HeightMm: heightMm,
            WidthMm: widthMm,
            ExtentPercent: extentPercent,
            CrossSectionReductionPercent: crossSectionPercent,
            IntrusionPercent: null,
            ClockPosition: clock);
}
