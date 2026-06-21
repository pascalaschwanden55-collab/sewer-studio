using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayQuantificationWriterTests
{
    [Fact]
    public void ApplyToEntry_writes_clock_and_q_values()
    {
        var entry = new ProtocolEntry();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            ClockFrom = 2.25,
            ClockTo = 5.75,
            Q1Mm = 12.34,
            Q2Mm = 5.55
        };

        CodingOverlayQuantificationWriter.ApplyToEntry(entry, overlay);

        Assert.NotNull(entry.CodeMeta);
        Assert.Equal("2.2", entry.CodeMeta.Parameters["vsa.uhr.von"]);
        Assert.Equal("5.8", entry.CodeMeta.Parameters["vsa.uhr.bis"]);
        Assert.Equal("12.3", entry.CodeMeta.Parameters["vsa.q1"]);
        Assert.Equal("5.5", entry.CodeMeta.Parameters["vsa.q2"]);
    }

    [Fact]
    public void ApplyToEntry_writes_bend_angle_only_for_pipe_bend()
    {
        var pipeBend = new ProtocolEntry();
        CodingOverlayQuantificationWriter.ApplyToEntry(pipeBend, new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            ArcDegrees = 42.42
        });

        var arc = new ProtocolEntry();
        CodingOverlayQuantificationWriter.ApplyToEntry(arc, new OverlayGeometry
        {
            ToolType = OverlayToolType.Arc,
            ArcDegrees = 42.42
        });

        Assert.Equal("42.4", pipeBend.CodeMeta!.Parameters["vsa.winkel"]);
        Assert.DoesNotContain("vsa.winkel", arc.CodeMeta!.Parameters.Keys);
    }

    [Fact]
    public void ApplyToEntry_writes_fill_percent_as_cross_section_for_level_with_three_points()
    {
        var entry = new ProtocolEntry();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            FillPercent = 33.33,
            Points =
            [
                new NormalizedPoint(0.1, 0.2),
                new NormalizedPoint(0.5, 0.2),
                new NormalizedPoint(0.5, 0.8)
            ]
        };

        CodingOverlayQuantificationWriter.ApplyToEntry(entry, overlay);

        Assert.Equal("33.3", entry.CodeMeta!.Parameters["vsa.querschnitt.prozent"]);
        Assert.DoesNotContain("vsa.fuellgrad.prozent", entry.CodeMeta.Parameters.Keys);
    }

    [Fact]
    public void ApplyToEntry_writes_fill_percent_as_fill_level_for_non_level_overlay()
    {
        var entry = new ProtocolEntry();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            FillPercent = 66.66
        };

        CodingOverlayQuantificationWriter.ApplyToEntry(entry, overlay);

        Assert.Equal("66.7", entry.CodeMeta!.Parameters["vsa.fuellgrad.prozent"]);
        Assert.DoesNotContain("vsa.querschnitt.prozent", entry.CodeMeta.Parameters.Keys);
    }
}
