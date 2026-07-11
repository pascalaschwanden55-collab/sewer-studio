using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingFindingFilterPolicyTests
{
    [Fact]
    public void FilterValid_discards_unresolved_findings_and_normalizes_code_hint()
    {
        var raw = new[]
        {
            Finding("unknown", "???"),
            Finding("crack", "bad")
        };

        var filtered = CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter: 4.2,
            codeResolver: (finding, _) => finding.Label == "crack" ? "BAB" : null,
            sessionEvents: null,
            viewEvents: null);

        var finding = Assert.Single(filtered);
        Assert.Equal("BAB", finding.VsaCodeHint);
    }

    [Fact]
    public void FilterValid_skips_one_time_code_when_already_present()
    {
        var raw = new[]
        {
            Finding("Rohranfang", null)
        };

        var filtered = CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter: 10,
            codeResolver: (_, _) => "BCD",
            sessionEvents: [Event("BCD")],
            viewEvents: null);

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterValid_deduplicates_same_code_and_position()
    {
        var raw = new[]
        {
            Finding("Riss A", "BAB", clock: "3"),
            Finding("Riss B", "BAB", clock: "3")
        };

        var filtered = CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter: 5,
            codeResolver: (_, _) => "BAB",
            sessionEvents: null,
            viewEvents: null);

        Assert.Single(filtered);
    }

    [Fact]
    public void FilterValid_keeps_distinct_bbox_positions()
    {
        var raw = new[]
        {
            Finding("Anschluss links", "BCA", x1: 0.1, y1: 0.1, x2: 0.2, y2: 0.2),
            Finding("Anschluss rechts", "BCA", x1: 0.8, y1: 0.1, x2: 0.9, y2: 0.2)
        };

        var filtered = CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter: 5,
            codeResolver: (_, _) => "BCA",
            sessionEvents: null,
            viewEvents: null);

        Assert.Equal(2, filtered.Count);
    }

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };

    private static LiveFrameFinding Finding(
        string label,
        string? code,
        string? clock = null,
        double? x1 = null,
        double? y1 = null,
        double? x2 = null,
        double? y2 = null)
        => new(
            Label: label,
            Severity: 2,
            PositionClock: clock,
            ExtentPercent: null,
            VsaCodeHint: code,
            BboxX1: x1,
            BboxY1: y1,
            BboxX2: x2,
            BboxY2: y2);
}
