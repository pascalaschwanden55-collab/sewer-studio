using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingLiveFindingQualityGatePolicy
{
    public static EvidenceVector BuildEvidence(LiveFrameFinding finding)
        => new(
            // Nur ECHTE Modell-Sicherheit als Signal — der Schadensgrad ist fachlich etwas
            // anderes und darf das Gate nicht speisen (Fehlerpruefung 11.07., Kritisch 3).
            QwenVisionConf: finding.ModelConfidence,
            PlausibilityScore: 0.6);

    public static QualityGateResult Evaluate(QualityGateService? qualityGate, LiveFrameFinding finding)
    {
        var evidence = BuildEvidence(finding);
        // Ohne Gate gibt es keine Bewertung — ehrlich Rot statt Severity als Pseudo-Composite.
        return qualityGate?.Evaluate(evidence)
            ?? new QualityGateResult(
                0.0,
                TrafficLight.Red,
                new Dictionary<string, double>(),
                "QualityGate nicht verfuegbar");
    }
}
