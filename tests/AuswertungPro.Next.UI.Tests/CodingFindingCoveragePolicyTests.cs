using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFindingCoveragePolicyTests
{
    [Fact]
    public void IsCovered_treats_one_time_code_as_covered_independent_of_meter()
    {
        var existing = Event("BCD", meter: 0);
        var finding = Finding("BCD");

        Assert.True(CodingFindingCoveragePolicy.IsCovered(existing, newMeter: 20, finding));
    }

    [Fact]
    public void IsCovered_treats_open_stretch_damage_as_covering_until_end()
    {
        var existing = Event("BAB", meter: 4);
        existing.Entry.IsStreckenschaden = true;
        existing.Entry.MeterStart = 4;
        existing.Entry.MeterEnd = null;

        Assert.True(CodingFindingCoveragePolicy.IsCovered(existing, 12, Finding("BAB")));
    }

    [Fact]
    public void IsCovered_accepted_event_covers_same_meter_window()
    {
        var existing = Event("BAB", meter: 5);
        existing.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted };

        Assert.True(CodingFindingCoveragePolicy.IsCovered(existing, 5.9, Finding("BAB")));
        Assert.False(CodingFindingCoveragePolicy.IsCovered(existing, 6.1, Finding("BAB")));
    }

    [Fact]
    public void IsCovered_distinguishes_bca_by_bbox_position()
    {
        var existing = Event("BCA", meter: 5);
        existing.Overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points =
            [
                new NormalizedPoint(0.1, 0.1),
                new NormalizedPoint(0.2, 0.1),
                new NormalizedPoint(0.2, 0.2),
                new NormalizedPoint(0.1, 0.2)
            ]
        };

        Assert.True(CodingFindingCoveragePolicy.IsCovered(
            existing,
            5.2,
            Finding("BCA", x1: 0.11, y1: 0.11, x2: 0.21, y2: 0.21)));

        Assert.False(CodingFindingCoveragePolicy.IsCovered(
            existing,
            5.2,
            Finding("BCA", x1: 0.7, y1: 0.7, x2: 0.8, y2: 0.8)));
    }

    [Fact]
    public void IsSamePosition_uses_clock_when_bbox_is_missing()
    {
        var existing = Event("BCA", meter: 5);
        existing.Entry.CodeMeta = new ProtocolEntryCodeMeta();
        existing.Entry.CodeMeta.Parameters["vsa.uhr.von"] = "3";

        Assert.True(CodingFindingCoveragePolicy.IsSamePosition(existing, Finding("BCA", clock: "3")));
        Assert.False(CodingFindingCoveragePolicy.IsSamePosition(existing, Finding("BCA", clock: "9")));
    }

    [Fact]
    public void FindCoveringEvent_returns_matching_covered_event()
    {
        var first = Event("BCA", meter: 2);
        var second = Event("BAB", meter: 5);
        second.AiContext = new CodingEventAiContext { Decision = CodingUserDecision.Accepted };

        var covering = CodingFindingCoveragePolicy.FindCoveringEvent(
            [first, second],
            "BAB",
            meter: 5.5,
            Finding("BAB"));

        Assert.Same(second, covering);
    }

    [Fact]
    public void MarkCoveredAgain_extends_stretch_damage_capture_meter_only_forward()
    {
        var existing = Event("BAB", meter: 4);
        existing.Entry.IsStreckenschaden = true;

        CodingFindingCoveragePolicy.MarkCoveredAgain(existing, 7);
        Assert.Equal(7, existing.MeterAtCapture);

        CodingFindingCoveragePolicy.MarkCoveredAgain(existing, 5);
        Assert.Equal(7, existing.MeterAtCapture);
    }

    [Fact]
    public void MarkCoveredAgain_does_not_update_point_damage_meter()
    {
        var existing = Event("BAB", meter: 4);

        CodingFindingCoveragePolicy.MarkCoveredAgain(existing, 7);

        Assert.Equal(4, existing.MeterAtCapture);
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };

    private static LiveFrameFinding Finding(
        string code,
        string? clock = null,
        double? x1 = null,
        double? y1 = null,
        double? x2 = null,
        double? y2 = null)
        => new(
            Label: code,
            Severity: 2,
            PositionClock: clock,
            ExtentPercent: null,
            VsaCodeHint: code,
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
