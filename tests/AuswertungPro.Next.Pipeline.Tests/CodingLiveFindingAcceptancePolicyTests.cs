using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingLiveFindingAcceptancePolicyTests
{
    [Theory]
    [InlineData("BAB", true, true)]
    [InlineData("BAB", false, false)]
    [InlineData("BCD", true, false)]
    [InlineData("BCE", true, false)]
    public void ShouldSkipAsTooFarAhead_skips_only_non_terminal_codes(string code, bool isTooFarAhead, bool expected)
    {
        Assert.Equal(expected, CodingLiveFindingAcceptancePolicy.ShouldSkipAsTooFarAhead(code, isTooFarAhead));
    }

    [Theory]
    [InlineData(TrafficLight.Green, 3, false)]
    [InlineData(TrafficLight.Green, 4, true)]
    [InlineData(TrafficLight.Yellow, 2, true)]
    [InlineData(TrafficLight.Red, 1, true)]
    public void NeedsConfirmation_uses_quality_gate_and_critical_severity(
        TrafficLight trafficLight,
        int severity,
        bool expected)
    {
        var gate = new QualityGateResult(0.8, trafficLight, new Dictionary<string, double>(), "test");

        Assert.Equal(expected, CodingLiveFindingAcceptancePolicy.NeedsConfirmation(gate, Finding(severity)));
    }

    private static LiveFrameFinding Finding(int severity)
        => new(
            Label: "finding",
            Severity: severity,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: "BAB");
}
