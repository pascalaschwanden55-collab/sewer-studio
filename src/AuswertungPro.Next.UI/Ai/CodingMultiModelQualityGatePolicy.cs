using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingMultiModelQualityGatePolicy
{
    public static QualityGateResult Evaluate(
        QualityGateService? qualityGate,
        double? yoloMaxConfidence,
        double dinoConfidence,
        double samMaskConfidence,
        string? officialLabel)
    {
        var evidence = new EvidenceVector(
            YoloConf: yoloMaxConfidence,
            DinoConf: dinoConfidence,
            SamMaskStability: samMaskConfidence,
            PlausibilityScore: officialLabel != null ? 0.8 : 0.4);

        return qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                dinoConfidence,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "Multi-Model");
    }
}
