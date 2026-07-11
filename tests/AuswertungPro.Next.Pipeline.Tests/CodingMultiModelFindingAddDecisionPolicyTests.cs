using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingMultiModelFindingAddDecisionPolicyTests
{
    [Fact]
    public void Decide_skips_when_code_is_missing()
    {
        var decision = CodingMultiModelFindingAddDecisionPolicy.Decide(
            code: null,
            sourceLabel: "crack",
            proximity: Proximity(MetrierungProximity.Codierbar),
            finding: Finding("BAB"),
            meter: 4,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingMultiModelFindingAddDecisionKind.MissingCode, decision.Kind);
        Assert.Contains("crack", decision.TraceMessage);
    }

    [Fact]
    public void Decide_defers_spatial_code_until_closer()
    {
        var decision = CodingMultiModelFindingAddDecisionPolicy.Decide(
            code: "BCC",
            sourceLabel: "bend",
            proximity: Proximity(MetrierungProximity.Voraus),
            finding: Finding("BCC"),
            meter: 6.25,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingMultiModelFindingAddDecisionKind.DeferSpatial, decision.Kind);
        Assert.Contains("BCC", decision.TraceMessage);
        Assert.Contains("6.25m", decision.TraceMessage);
    }

    [Fact]
    public void Decide_skips_one_time_code_duplicate()
    {
        var decision = CodingMultiModelFindingAddDecisionPolicy.Decide(
            code: "BCE",
            sourceLabel: "end",
            proximity: Proximity(MetrierungProximity.Codierbar),
            finding: Finding("BCE"),
            meter: 12,
            sessionEvents: [Event("BCE", 10)],
            viewEvents: []);

        Assert.Equal(CodingMultiModelFindingAddDecisionKind.SkipOneTimeDuplicate, decision.Kind);
    }

    [Fact]
    public void Decide_returns_covering_existing_event()
    {
        var existing = Event("BAB", 5);

        var decision = CodingMultiModelFindingAddDecisionPolicy.Decide(
            code: "BAB",
            sourceLabel: "crack",
            proximity: Proximity(MetrierungProximity.Codierbar),
            finding: Finding("BAB"),
            meter: 5.4,
            sessionEvents: null,
            viewEvents: [existing]);

        Assert.Equal(CodingMultiModelFindingAddDecisionKind.CoveredExisting, decision.Kind);
        Assert.Same(existing, decision.CoveringEvent);
    }

    [Fact]
    public void Decide_allows_regular_finding_when_no_skip_condition_matches()
    {
        var decision = CodingMultiModelFindingAddDecisionPolicy.Decide(
            code: "BAB",
            sourceLabel: "crack",
            proximity: Proximity(MetrierungProximity.Codierbar),
            finding: Finding("BAB"),
            meter: 4,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingMultiModelFindingAddDecisionKind.Add, decision.Kind);
        Assert.Equal("BAB", decision.Code);
    }

    private static MetrierungProximityResult Proximity(MetrierungProximity decision)
        => new(decision, "test", FillRatio: 0, DistToVanish: 0, OuterRadius: 0, WandNaehe: false, EnthaeltCenter: false);

    private static LiveFrameFinding Finding(string code)
        => new(
            Label: code,
            Severity: 2,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry { Code = code, MeterStart = meter }
        };
}
