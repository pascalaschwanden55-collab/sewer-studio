using AuswertungPro.Next.UI.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class QualityGateServiceTests
{
    [Fact]
    public void FullEvidence_HighSignals_ReturnsGreen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            YoloConf: 0.95,
            DinoConf: 0.88,
            SamMaskStability: 0.90,
            QwenVisionConf: 0.85,
            LlmCodeConf: 0.92,
            KbSimilarity: 0.80,
            KbCodeAgreement: true,
            PlausibilityScore: 0.95);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Green, result.TrafficLight);
        Assert.True(result.CompositeConfidence >= QualityGateService.GreenThreshold);
    }

    [Fact]
    public void MixedEvidence_ReturnsYellow()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            YoloConf: 0.80,
            DinoConf: 0.55,
            LlmCodeConf: 0.50,
            KbCodeAgreement: false,
            PlausibilityScore: 0.60);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
    }

    [Fact]
    public void LowEvidence_ReturnsRed()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            LlmCodeConf: 0.20,
            KbCodeAgreement: false,
            PlausibilityScore: 0.15);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Red, result.TrafficLight);
        Assert.True(result.CompositeConfidence < QualityGateService.YellowThreshold);
    }

    [Fact]
    public void NullSignals_AreSkipped_WeightsRenormalized()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.90, PlausibilityScore: 0.85);

        var result = svc.Evaluate(ev);

        Assert.Equal(2, result.WeightsUsed.Count);
        Assert.True(result.CompositeConfidence > 0.80);
    }

    [Fact]
    public void EmptyEvidence_ReturnsRed()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector();

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Red, result.TrafficLight);
        Assert.Equal(0.0, result.CompositeConfidence);
    }

    [Fact]
    public void CustomCategoryWeights_AreUsed()
    {
        var svc = new QualityGateService();
        var weights = new CategoryWeights
        {
            Category = "BAB",
            WLlm = 0.80,
            WPlausibility = 0.20,
            WYolo = 0, WDino = 0, WSam = 0, WQwen = 0, WKb = 0, WKbAgreement = 0
        };
        svc.SetWeights(weights);

        var ev = new EvidenceVector(LlmCodeConf: 0.95, PlausibilityScore: 0.50, DamageCategory: "BAB");
        var result = svc.Evaluate(ev);

        // With 80% weight on LLM (0.95) and 20% on Plausibility (0.50):
        // Composite ≈ (0.80*0.95 + 0.20*0.50) / 1.0 = 0.86
        Assert.True(result.CompositeConfidence > 0.80);
        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    [Fact]
    public void ExplanationContainsSignalInfo()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.70, DinoConf: 0.60);

        var result = svc.Evaluate(ev);

        Assert.Contains("LlmCodeConf", result.Explanation);
        Assert.Contains("DinoConf", result.Explanation);
    }
}
