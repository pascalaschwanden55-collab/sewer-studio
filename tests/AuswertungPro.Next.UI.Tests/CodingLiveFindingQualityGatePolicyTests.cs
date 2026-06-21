using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingQualityGatePolicyTests
{
    [Fact]
    public void Evaluate_without_service_uses_existing_yellow_fallback_for_low_severity()
    {
        var result = CodingLiveFindingQualityGatePolicy.Evaluate(null, Finding(severity: 3));

        Assert.Equal(0.6, result.CompositeConfidence);
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
        Assert.Equal("Fallback", result.Explanation);
        Assert.Empty(result.WeightsUsed);
    }

    [Fact]
    public void Evaluate_without_service_uses_existing_green_fallback_for_high_severity()
    {
        var result = CodingLiveFindingQualityGatePolicy.Evaluate(null, Finding(severity: 4));

        Assert.Equal(0.8, result.CompositeConfidence);
        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    [Fact]
    public void Evaluate_with_service_delegates_to_quality_gate()
    {
        var service = new QualityGateService();

        var result = CodingLiveFindingQualityGatePolicy.Evaluate(service, Finding(severity: 5));

        Assert.NotEqual("Fallback", result.Explanation);
        Assert.Contains(nameof(EvidenceVector.QwenVisionConf), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.PlausibilityScore), result.WeightsUsed.Keys);
    }

    private static LiveFrameFinding Finding(int severity)
        => new(
            Label: "finding",
            Severity: severity,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: null);
}
