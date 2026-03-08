using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AutoApprovalTests
{
    private static MappedProtocolEntry CreateEntry(
        double confidence,
        TrafficLight light,
        bool? kbAgrees = null,
        double epistemic = 0.05)
    {
        var evidence = new EvidenceVector(
            LlmCodeConf: confidence,
            KbCodeAgreement: kbAgrees);
        var detection = new RawVideoDetection("Test", 1.0, 2.0, "high", Evidence: evidence);
        return new MappedProtocolEntry(
            Detection: detection,
            SuggestedCode: "BAB",
            Confidence: confidence,
            Reason: "test",
            Warnings: System.Array.Empty<string>(),
            QualityGateResult: new QualityGateResult(confidence, light,
                new System.Collections.Generic.Dictionary<string, double>(), "test"),
            Uncertainty: new UncertaintyEstimate(confidence, epistemic, 0.05, confidence, UncertaintySource.SinglePass));
    }

    [Fact]
    public void AllCriteriaMet_IsApproved()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Green, kbAgrees: true, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.True(result.IsApproved);
    }

    [Fact]
    public void LowConfidence_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.80, TrafficLight.Green, kbAgrees: true, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("Confidence", result.Reason);
    }

    [Fact]
    public void YellowLight_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Yellow, kbAgrees: true, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("Yellow", result.Reason);
    }

    [Fact]
    public void KbDisagrees_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Green, kbAgrees: false, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("KB", result.Reason);
    }

    [Fact]
    public void HighEpistemic_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Green, kbAgrees: true, epistemic: 0.30);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("Unsicherheit", result.Reason);
    }

    [Fact]
    public void NoQualityGateResult_IsRejected()
    {
        var detection = new RawVideoDetection("Test", 1.0, 2.0, "high");
        var entry = new MappedProtocolEntry(detection, "BAB", 0.95, "test",
            System.Array.Empty<string>());

        var svc = new AutoApprovalService();
        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
    }
}
