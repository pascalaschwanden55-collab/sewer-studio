using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingKnownFindingPolicyTests
{
    [Fact]
    public void IsKnown_returns_false_without_code_hint()
    {
        var finding = Finding(null);

        Assert.False(CodingKnownFindingPolicy.IsKnown(
            finding,
            meter: 5,
            sessionEvents: [Event("BAB", 5)],
            viewEvents: null));
    }

    [Fact]
    public void IsKnown_finds_covered_event_in_session_events()
    {
        var finding = Finding("BAB");

        Assert.True(CodingKnownFindingPolicy.IsKnown(
            finding,
            meter: 5.2,
            sessionEvents: [Event("BAB", 5)],
            viewEvents: null));
    }

    [Fact]
    public void IsKnown_finds_covered_event_in_view_events()
    {
        var finding = Finding("BCAEB", x1: 0.7, y1: 0.4, x2: 0.9, y2: 0.6);
        var existing = Event("BCA", 5);
        existing.Overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points =
            [
                new NormalizedPoint(0.7, 0.4),
                new NormalizedPoint(0.9, 0.4),
                new NormalizedPoint(0.9, 0.6),
                new NormalizedPoint(0.7, 0.6)
            ]
        };

        Assert.True(CodingKnownFindingPolicy.IsKnown(
            finding,
            meter: 5.2,
            sessionEvents: null,
            viewEvents: [existing]));
    }

    [Fact]
    public void IsKnown_returns_false_when_code_or_coverage_does_not_match()
    {
        var finding = Finding("BAB");

        Assert.False(CodingKnownFindingPolicy.IsKnown(
            finding,
            meter: 8,
            sessionEvents: [Event("BCA", 5), Event("BAB", 5)],
            viewEvents: null));
    }

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };

    private static LiveFrameFinding Finding(
        string? code,
        double? x1 = null,
        double? y1 = null,
        double? x2 = null,
        double? y2 = null)
        => new(
            Label: code ?? "unknown",
            Severity: 2,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code,
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
