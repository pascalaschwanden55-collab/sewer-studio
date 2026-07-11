using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fehlerpruefung 11.07., Kritisch 3: Der Schadensgrad ist KEINE Modell-Sicherheit.
/// Das Gate bekommt nur echte ModelConfidence-Werte; ohne Gate gibt es ehrlich Rot
/// statt Severity/5 als Pseudo-Composite.
/// </summary>
public sealed class CodingLiveFindingQualityGatePolicyTests
{
    [Fact]
    public void Evaluate_ohne_Gate_liefert_Rot_ohne_Severity_Ersatzwert()
    {
        var result = CodingLiveFindingQualityGatePolicy.Evaluate(null, Finding(severity: 4));

        Assert.Equal(0.0, result.CompositeConfidence); // NICHT 0.8 (= Severity*0.2)
        Assert.Equal(TrafficLight.Red, result.TrafficLight);
        Assert.Equal("QualityGate nicht verfuegbar", result.Explanation);
        Assert.Empty(result.WeightsUsed);
    }

    [Fact]
    public void BuildEvidence_ohne_ModelConfidence_liefert_kein_QwenSignal()
    {
        var evidence = CodingLiveFindingQualityGatePolicy.BuildEvidence(Finding(severity: 5));

        Assert.Null(evidence.QwenVisionConf); // Severity 5 darf NICHT als 1.0 erscheinen
        Assert.Equal(0.6, evidence.PlausibilityScore);
    }

    [Fact]
    public void Evaluate_mit_Gate_und_echter_ModelConfidence_nutzt_beide_Signale()
    {
        var service = new QualityGateService();

        var result = CodingLiveFindingQualityGatePolicy.Evaluate(
            service, Finding(severity: 5, modelConfidence: 0.9));

        Assert.NotEqual("QualityGate nicht verfuegbar", result.Explanation);
        Assert.Contains(nameof(EvidenceVector.QwenVisionConf), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.PlausibilityScore), result.WeightsUsed.Keys);
    }

    [Fact]
    public void Evaluate_mit_Gate_ohne_ModelConfidence_bewertet_nur_Plausibilitaet()
    {
        var service = new QualityGateService();

        var result = CodingLiveFindingQualityGatePolicy.Evaluate(service, Finding(severity: 5));

        Assert.DoesNotContain(nameof(EvidenceVector.QwenVisionConf), result.WeightsUsed.Keys);
        Assert.Contains(nameof(EvidenceVector.PlausibilityScore), result.WeightsUsed.Keys);
    }

    private static LiveFrameFinding Finding(int severity, double? modelConfidence = null)
        => new(
            Label: "finding",
            Severity: severity,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: null,
            ModelConfidence: modelConfidence);
}
