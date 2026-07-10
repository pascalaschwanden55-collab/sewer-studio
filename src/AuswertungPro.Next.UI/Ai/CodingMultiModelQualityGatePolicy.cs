using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingMultiModelQualityGatePolicy
{
    public static EvidenceVector BuildEvidence(
        double? yoloMaxConfidence,
        double dinoConfidence,
        double samMaskConfidence,
        string? officialLabel)
        => new(
            YoloConf: yoloMaxConfidence,
            DinoConf: dinoConfidence,
            SamMaskStability: samMaskConfidence,
            PlausibilityScore: officialLabel != null ? 0.8 : 0.4);

    public static QualityGateResult Evaluate(
        QualityGateService? qualityGate,
        EvidenceVector evidence)
    {
        return qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                evidence.DinoConf ?? 0,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "Multi-Model");
    }

    public static QualityGateResult Evaluate(
        QualityGateService? qualityGate,
        double? yoloMaxConfidence,
        double dinoConfidence,
        double samMaskConfidence,
        string? officialLabel)
        => Evaluate(
            qualityGate,
            BuildEvidence(
                yoloMaxConfidence,
                dinoConfidence,
                samMaskConfidence,
                officialLabel));
}
