using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingToolBadgeTextPolicyTests
{
    [Theory]
    [InlineData(OverlayToolType.Line, "Linie")]
    [InlineData(OverlayToolType.Arc, "Bogen")]
    [InlineData(OverlayToolType.Rectangle, "Flaeche")]
    [InlineData(OverlayToolType.Point, "Punkt")]
    [InlineData(OverlayToolType.Stretch, "Strecke")]
    [InlineData(OverlayToolType.PipeBend, "Bogen")]
    [InlineData(OverlayToolType.LateralCircle, "Anschluss")]
    [InlineData(OverlayToolType.Ruler, "Lineal")]
    public void BuildText_keeps_existing_labels(OverlayToolType tool, string expected)
    {
        Assert.Equal(
            expected,
            CodingToolBadgeTextPolicy.BuildText(tool, SchemaType.PipeBend, LevelMode.Deposit));
    }

    [Theory]
    [InlineData(SchemaType.FillLevel, LevelMode.Water, "Wasser %")]
    [InlineData(SchemaType.FillLevel, LevelMode.Deposit, "Sediment %")]
    [InlineData(SchemaType.FillLevel, LevelMode.Obstacle, "Sediment %")]
    [InlineData(SchemaType.Intrusion, LevelMode.Deposit, "Einragung %")]
    [InlineData(SchemaType.PipeBend, LevelMode.Deposit, "Level")]
    public void BuildText_maps_level_tool_by_schema_and_level_mode(
        SchemaType schemaType,
        LevelMode levelMode,
        string expected)
    {
        Assert.Equal(
            expected,
            CodingToolBadgeTextPolicy.BuildText(OverlayToolType.Level, schemaType, levelMode));
    }

    [Fact]
    public void BuildText_returns_level_when_level_tool_has_no_schema()
    {
        Assert.Equal(
            "Level",
            CodingToolBadgeTextPolicy.BuildText(OverlayToolType.Level, schemaType: null, LevelMode.Deposit));
    }

    [Theory]
    [InlineData(OverlayToolType.None)]
    [InlineData(OverlayToolType.Ellipse)]
    [InlineData(OverlayToolType.Freehand)]
    [InlineData(OverlayToolType.CrossSection)]
    public void BuildText_returns_null_for_tools_without_badge(OverlayToolType tool)
    {
        Assert.Null(CodingToolBadgeTextPolicy.BuildText(tool, SchemaType.PipeBend, LevelMode.Deposit));
    }
}
