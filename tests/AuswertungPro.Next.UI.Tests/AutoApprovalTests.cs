using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

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
        Assert.Contains("Sicherheit", result.Reason);
    }

    [Fact]
    public void YellowLight_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Yellow, kbAgrees: true, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("Gruen", result.Reason);
    }

    [Fact]
    public void KbDisagrees_IsRejected()
    {
        var svc = new AutoApprovalService();
        var entry = CreateEntry(0.95, TrafficLight.Green, kbAgrees: false, epistemic: 0.05);

        var result = svc.Evaluate(entry);

        Assert.False(result.IsApproved);
        Assert.Contains("Datenbank", result.Reason);
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

    [Theory] // Review 11.07., Empfehlung 2: Urteil + Grund als sichtbarer Hinweis fuer die Vollanalyse.
    [InlineData(true, "Zentrale Freigabe: verlaesslich —")]
    [InlineData(false, "Zentrale Freigabe: pruefen —")]
    public void AlsHinweis_FormatiertUrteilUndGrund(bool approved, string erwarteterPrefix)
    {
        var result = approved
            ? AutoApprovalResult.Approved("Alle Belege bestaetigt.")
            : AutoApprovalResult.Rejected("Datenbank-Abgleich fehlt.");

        var hinweis = AutoApprovalService.AlsHinweis(result);

        Assert.StartsWith(erwarteterPrefix, hinweis);
        Assert.Contains(result.Reason, hinweis);
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
