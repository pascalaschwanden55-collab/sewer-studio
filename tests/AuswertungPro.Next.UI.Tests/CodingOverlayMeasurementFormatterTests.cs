using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayMeasurementFormatterTests
{
    [Fact]
    public void BuildOverlayMeasurementText_formats_tool_specific_measurements()
    {
        Assert.Equal("Winkel: 33.5\u00B0", CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            ArcDegrees = 33.45
        }));

        Assert.Equal("Wasser: 12.3%", CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            LevelSubMode = LevelMode.Water,
            FillPercent = 12.34
        }));

        Assert.Equal("DN 160 (53% v. Haupt-DN)", CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(new OverlayGeometry
        {
            ToolType = OverlayToolType.LateralCircle,
            Q1Mm = 160,
            DnRatioPercent = 53
        }));
    }

    [Fact]
    public void BuildOverlayMeasurementText_keeps_standard_quantification_format()
    {
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Q1Mm = 12.3,
            Q2Mm = 4.8,
            ClockFrom = 2,
            ClockTo = 4,
            ArcDegrees = 45
        };

        Assert.Equal("Q1:12mm  Q2:5mm  Uhr:2.0->4.0  Bogen:45deg",
            CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(overlay));
    }

    [Fact]
    public void BuildOverlayMeasurementText_does_not_require_fill_percent_for_ellipse()
    {
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Ellipse };

        Assert.Equal("", CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(overlay));
    }

    [Fact]
    public void BuildPanelMeasurementText_formats_level_and_lateral_measurements()
    {
        var level = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            FillPercent = 40,
            ClockFrom = 6,
            Points =
            {
                new NormalizedPoint(0.1, 0.2),
                new NormalizedPoint(0.4, 0.2),
                new NormalizedPoint(0.4, 0.6)
            }
        };
        var lateral = new OverlayGeometry
        {
            ToolType = OverlayToolType.LateralCircle,
            Q1Mm = 110,
            DnRatioPercent = 37,
            ClockFrom = 3
        };

        Assert.Equal("Einragung:40.0%  |  Uhr:6.0", CodingOverlayMeasurementFormatter.BuildPanelMeasurementText(level));
        Assert.Equal("DN:110mm  |  37%  |  Uhr:3.0", CodingOverlayMeasurementFormatter.BuildPanelMeasurementText(lateral));
    }

    [Fact]
    public void BuildPanelState_hides_measurement_panel_without_overlay()
    {
        var state = CodingOverlayMeasurementFormatter.BuildPanelState(null);

        Assert.False(state.IsVisible);
        Assert.Equal("Q1: -", state.Q1Text);
        Assert.Equal("Q2: -", state.Q2Text);
        Assert.Equal("Uhr: -", state.ClockText);
        Assert.Equal("Bogen: -", state.ArcText);
        Assert.Equal("", state.MeasurementText);
    }

    [Fact]
    public void BuildPanelState_formats_standard_measurement_fields()
    {
        var state = CodingOverlayMeasurementFormatter.BuildPanelState(new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Q1Mm = 12.34,
            Q2Mm = 4.56,
            ClockFrom = 2,
            ClockTo = 5,
            ArcDegrees = 44.9
        });

        Assert.True(state.IsVisible);
        Assert.Equal("Q1: 12.3 mm", state.Q1Text);
        Assert.Equal("Q2: 4.6 mm", state.Q2Text);
        Assert.Equal("Uhr: 2.0 -> 5.0", state.ClockText);
        Assert.Equal("Bogen: 45 deg", state.ArcText);
        Assert.Equal("Q1:12.3mm  |  Uhr:2.0  |  45deg", state.MeasurementText);
    }

    [Fact]
    public void BuildPanelState_formats_level_fill_and_pipe_bend_angle()
    {
        var level = CodingOverlayMeasurementFormatter.BuildPanelState(new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            FillPercent = 37.8
        });
        var bend = CodingOverlayMeasurementFormatter.BuildPanelState(new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            ArcDegrees = 22.26
        });

        Assert.Equal("Fuellung: 37.8%", level.ArcText);
        Assert.Equal("Winkel: 22.3\u00B0", bend.ArcText);
    }
}
