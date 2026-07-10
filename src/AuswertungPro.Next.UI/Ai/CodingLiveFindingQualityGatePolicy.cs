using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingLiveFindingQualityGatePolicy
{
    public static EvidenceVector BuildEvidence(LiveFrameFinding finding)
        => new(
            QwenVisionConf: finding.Severity / 5.0,
            PlausibilityScore: 0.6);

    public static QualityGateResult Evaluate(QualityGateService? qualityGate, LiveFrameFinding finding)
    {
        var evidence = BuildEvidence(finding);
        return qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                finding.Severity / 5.0,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "QualityGate nicht verfuegbar");
    }
}
