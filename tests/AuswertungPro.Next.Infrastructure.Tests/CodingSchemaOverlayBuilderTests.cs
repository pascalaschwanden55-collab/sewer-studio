using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodingSchemaOverlayBuilderTests
{
    [Fact]
    public void Create_builds_schema_with_current_tool_options()
    {
        var bend = Assert.IsType<PipeBendSchema>(
            CodingSchemaOverlayBuilder.Create(SchemaType.PipeBend, pipeBendSnapEnabled: true, LevelMode.Deposit));
        Assert.True(bend.SnapEnabled);

        var fill = Assert.IsType<FillLevelSchema>(
            CodingSchemaOverlayBuilder.Create(SchemaType.FillLevel, pipeBendSnapEnabled: false, LevelMode.Water));
        Assert.Equal(LevelMode.Water, fill.Mode);

        Assert.IsType<IntrusionSchema>(
            CodingSchemaOverlayBuilder.Create(SchemaType.Intrusion, pipeBendSnapEnabled: false, LevelMode.Deposit));
        Assert.Null(CodingSchemaOverlayBuilder.Create(null, pipeBendSnapEnabled: false, LevelMode.Deposit));
    }

    [Theory]
    [InlineData(SchemaType.PipeBend, "vertex")]
    [InlineData(SchemaType.FillLevel, "level")]
    [InlineData(SchemaType.Intrusion, "depth")]
    public void GetDefaultHandleId_returns_schema_handle(SchemaType schemaType, string expected)
    {
        Assert.Equal(expected, CodingSchemaOverlayBuilder.GetDefaultHandleId(schemaType));
    }

    [Fact]
    public void BuildGeometry_keeps_pipe_bend_point_order_expected_by_renderer()
    {
        var schema = new PipeBendSchema
        {
            Center = new NormalizedPoint(0.5, 0.5),
            AngleDeg = 44,
            RotationDeg = -90,
            ArmLength = 0.1,
            SnapEnabled = true
        };

        var geometry = CodingSchemaOverlayBuilder.BuildGeometry(schema);
        Assert.NotNull(geometry);

        Assert.Equal(OverlayToolType.PipeBend, geometry!.ToolType);
        Assert.Equal(3, geometry.Points.Count);
        Assert.Equal(0.5, geometry.Points[1].X, 6);
        Assert.Equal(0.5, geometry.Points[1].Y, 6);
        Assert.Equal(45, geometry.ArcDegrees);
    }

    [Fact]
    public void BuildGeometry_uses_shared_fill_percent_formula()
    {
        var schema = new FillLevelSchema
        {
            PipeCenter = new NormalizedPoint(0.5, 0.5),
            PipeRadius = 0.25,
            FillRatio = 0.25,
            Mode = LevelMode.Water
        };

        var geometry = CodingSchemaOverlayBuilder.BuildGeometry(schema);
        Assert.NotNull(geometry);

        Assert.Equal(OverlayToolType.Level, geometry!.ToolType);
        Assert.Equal(2, geometry.Points.Count);
        Assert.Equal(LevelMode.Water, geometry.LevelSubMode);
        Assert.Equal(Math.Round(OverlayToolService.CircleSegmentPercent(0.25), 1), geometry.FillPercent);
    }

    [Fact]
    public void BuildGeometry_expands_intrusion_to_renderable_polygon_points()
    {
        var schema = new IntrusionSchema
        {
            PipeCenter = new NormalizedPoint(0.5, 0.5),
            PipeRadius = 0.3,
            DepthRatio = 0.2,
            ClockHour = 3,
            SpreadDeg = 30
        };

        var geometry = CodingSchemaOverlayBuilder.BuildGeometry(schema);
        Assert.NotNull(geometry);

        Assert.Equal(OverlayToolType.Level, geometry!.ToolType);
        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(LevelMode.Obstacle, geometry.LevelSubMode);
        Assert.Equal(20.0, geometry.FillPercent);
        Assert.Equal(3.0, geometry.ClockFrom);
    }
}
