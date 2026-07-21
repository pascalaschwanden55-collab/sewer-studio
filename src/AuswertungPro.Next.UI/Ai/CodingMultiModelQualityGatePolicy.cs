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
        // Ohne Gate gibt es keine echte Bewertung — ehrlich Rot statt DINO-Konfidenz als
        // Pseudo-Gelb (konsistent zu CodingLiveFindingQualityGatePolicy).
        return qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                0.0,
                TrafficLight.Red,
                new Dictionary<string, double>(),
                "QualityGate nicht verfuegbar");
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
