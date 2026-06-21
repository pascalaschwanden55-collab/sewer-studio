using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

public static class CodingSchemaOverlayBuilder
{
    public static SchemaOverlayBase? Create(
        SchemaType? schemaType,
        bool pipeBendSnapEnabled,
        LevelMode activeLevelMode)
    {
        return schemaType switch
        {
            SchemaType.PipeBend => new PipeBendSchema
            {
                SnapEnabled = pipeBendSnapEnabled
            },
            SchemaType.FillLevel => new FillLevelSchema
            {
                Mode = activeLevelMode
            },
            SchemaType.Intrusion => new IntrusionSchema(),
            _ => null
        };
    }

    public static string GetDefaultHandleId(SchemaType? schemaType)
        => schemaType switch
        {
            SchemaType.PipeBend => "vertex",
            SchemaType.FillLevel => "level",
            SchemaType.Intrusion => "depth",
            _ => "vertex"
        };

    public static OverlayGeometry? BuildGeometry(SchemaOverlayBase? active)
    {
        return active switch
        {
            PipeBendSchema bend => BuildPipeBendGeometry(bend),
            FillLevelSchema fill => BuildFillLevelGeometry(fill),
            IntrusionSchema intrusion => BuildIntrusionGeometry(intrusion),
            _ => null
        };
    }

    private static OverlayGeometry BuildPipeBendGeometry(PipeBendSchema bend)
    {
        var (arm1, arm2) = bend.GetArmEndpoints();
        var angle = bend.SnapEnabled
            ? new[] { 15d, 30d, 45d, 90d }
                .OrderBy(candidate => Math.Abs(candidate - bend.AngleDeg))
                .First()
            : Math.Round(bend.AngleDeg, 1);

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { arm1, bend.Center, arm2 },
            ArcDegrees = Math.Round(angle, 1)
        };
    }

    private static OverlayGeometry BuildFillLevelGeometry(FillLevelSchema fill)
    {
        var levelY = fill.GetLevelLineY();
        var dy = levelY - fill.PipeCenter.Y;
        var halfChord = Math.Sqrt(Math.Max(0, fill.PipeRadius * fill.PipeRadius - dy * dy));
        var percent = OverlayToolService.CircleSegmentPercent(fill.FillRatio);

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint>
            {
                new(fill.PipeCenter.X - halfChord, levelY),
                new(fill.PipeCenter.X + halfChord, levelY)
            },
            FillPercent = Math.Round(percent, 1),
            LevelSubMode = fill.Mode
        };
    }

    private static OverlayGeometry BuildIntrusionGeometry(IntrusionSchema intrusion)
    {
        var edge = intrusion.GetEdgePoint();
        var tip = intrusion.GetIntrusionTip();
        var (left, right) = intrusion.GetSpreadEdges();

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint> { edge, tip, intrusion.PipeCenter, left, right },
            FillPercent = Math.Round(intrusion.DepthRatio * 100.0, 1),
            LevelSubMode = LevelMode.Obstacle,
            ClockFrom = Math.Round(intrusion.ClockHour, 1)
        };
    }
}
