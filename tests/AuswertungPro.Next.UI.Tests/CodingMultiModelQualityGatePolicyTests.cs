using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelQualityGatePolicyTests
{
    [Fact]
    public void Evaluate_without_service_uses_existing_yellow_fallback()
    {
        var result = CodingMultiModelQualityGatePolicy.Evaluate(
            qualityGate: null,
            yoloMaxConfidence: null,
            dinoConfidence: 0.72,
            samMaskConfidence: 0.61,
            officialLabel: "Riss");

        Assert.Equal(0.72, result.CompositeConfidence);
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
        Assert.Equal("Multi-Model", result.Explanation);
        Assert.Empty(result.WeightsUsed);
    }

    [Theory]
    [InlineData("Riss", 0.8)]
    [InlineData(null, 0.4)]
    public void Evaluate_with_service_builds_expected_evidence(string? officialLabel, double expectedPlausibility)
    {
        var result = CodingMultiModelQualityGatePolicy.Evaluate(
            new QualityGateService(),
            yoloMaxConfidence: 0.93,
            dinoConfidence: 0.72,
            samMaskConfidence: 0.61,
            officialLabel);

        Assert.NotEqual("Multi-Model", result.Explanation);
        Assert.Contains(nameof(EvidenceVector.YoloConf), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.DinoConf), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.SamMaskStability), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.PlausibilityScore), result.WeightsUsed.Keys);
        Assert.Contains($"{nameof(EvidenceVector.PlausibilityScore)}={expectedPlausibility:F2}", result.Explanation);
    }
}
