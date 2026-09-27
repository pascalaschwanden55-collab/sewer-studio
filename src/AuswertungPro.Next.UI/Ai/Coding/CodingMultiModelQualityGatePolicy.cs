using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingMultiModelQualityGatePolicy
{
    public static EvidenceVector BuildEvidence(SegmentedFinding finding, double? yoloMaxConfidence, string? officialLabel)
        => finding.Origin is { } origin
            ? new EvidenceVector(YoloConf: origin.YoloConfidence, DinoConf: origin.DinoConfidence,
                // Ein Katalogtitel ist keine weitere Modellstimme.
                SamMaskStability: finding.Quant.Confidence)
            : BuildEvidence(yoloMaxConfidence, finding.Dino?.Confidence ?? finding.Quant.Confidence,
                finding.Quant.Confidence, officialLabel);

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
        EvidenceVector evidence,
        bool requiresReview = false)
    {
        // Ohne Gate gibt es keine echte Bewertung — ehrlich Rot statt DINO-Konfidenz als
        // Pseudo-Gelb (konsistent zu CodingLiveFindingQualityGatePolicy).
        var result = qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                0.0,
                TrafficLight.Red,
                new Dictionary<string, double>(),
                "QualityGate nicht verfuegbar");
        return requiresReview && result.TrafficLight == TrafficLight.Green
            ? result with { TrafficLight = TrafficLight.Yellow, Explanation = result.Explanation + " — Modellnachweis unvollstaendig; manuell pruefen." }
            : result;
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
