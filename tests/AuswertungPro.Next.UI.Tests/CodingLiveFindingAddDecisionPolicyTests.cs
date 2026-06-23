using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingAddDecisionPolicyTests
{
    [Fact]
    public void Decide_allows_regular_finding_when_no_skip_condition_matches()
    {
        var decision = CodingLiveFindingAddDecisionPolicy.Decide(
            code: "BAB",
            finding: Finding("BAB"),
            meter: 5,
            isTooFarAhead: false,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingLiveFindingAddDecisionKind.Add, decision.Kind);
    }

    [Fact]
    public void Decide_skips_regular_finding_that_is_too_far_ahead()
    {
        var decision = CodingLiveFindingAddDecisionPolicy.Decide(
            code: "BAB",
            finding: Finding("BAB"),
            meter: 5.25,
            isTooFarAhead: true,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingLiveFindingAddDecisionKind.SkipTooFarAhead, decision.Kind);
        Assert.Contains("BAB", decision.TraceMessage);
        Assert.Contains("5.25m", decision.TraceMessage);
    }

    [Theory]
    [InlineData("BCD")]
    [InlineData("BCE")]
    public void Decide_does_not_skip_terminal_codes_as_too_far_ahead(string code)
    {
        var decision = CodingLiveFindingAddDecisionPolicy.Decide(
            code,
            Finding(code),
            meter: 5,
            isTooFarAhead: true,
            sessionEvents: null,
            viewEvents: []);

        Assert.Equal(CodingLiveFindingAddDecisionKind.Add, decision.Kind);
    }

    [Fact]
    public void Decide_skips_one_time_code_duplicate()
    {
        var decision = CodingLiveFindingAddDecisionPolicy.Decide(
            code: "BCD",
            finding: Finding("BCD"),
            meter: 5,
            isTooFarAhead: false,
            sessionEvents: [Event("BCD", 2)],
            viewEvents: []);

        Assert.Equal(CodingLiveFindingAddDecisionKind.SkipOneTimeDuplicate, decision.Kind);
        Assert.Contains("BCD", decision.TraceMessage);
    }

    [Fact]
    public void Decide_returns_covering_existing_event()
    {
        var existing = Event("BAB", 5);

        var decision = CodingLiveFindingAddDecisionPolicy.Decide(
            code: "BAB",
            finding: Finding("BAB"),
            meter: 5.4,
            isTooFarAhead: false,
            sessionEvents: null,
            viewEvents: [existing]);

        Assert.Equal(CodingLiveFindingAddDecisionKind.CoveredExisting, decision.Kind);
        Assert.Same(existing, decision.CoveringEvent);
    }

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
