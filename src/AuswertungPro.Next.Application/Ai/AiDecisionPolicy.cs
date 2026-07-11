using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis der zentralen Freigabe-Regel.</summary>
public enum AiDecisionOutcome
{
    AutoAccept, // Gruen: mehrere Belege passen, verlaesslich
    Review,     // Gelb: pruefen
    Reject      // Rot: unbedingt pruefen
}

/// <summary>Freigabe-Entscheidung mit Begruendung fuer Anzeige/Statistik.</summary>
public sealed record AiDecision(AiDecisionOutcome Outcome, string Reason);

/// <summary>
/// Belege eines KI-Befunds. Null = Beleg ist in diesem Kontext nicht vorhanden
/// (z.B. beim Live-Codieren gibt es weder Datenbank-Abgleich noch Unsicherheit).
/// </summary>
public sealed record AiDecisionSignals(
    double Confidence,
    TrafficLight? QualityGate = null,
    bool? KbAgreement = null,
    double? EpistemicUncertainty = null);

/// <summary>Zentrale KI-Freigabe-Regel (Audit Fix 3).</summary>
public interface IAiDecisionPolicy
{
    AiDecision Decide(AiDecisionSignals signals);
}

/// <summary>
/// Strenge Freigabe: Hohe Sicherheit und gruene Ampel sind Pflicht, gelten aber nicht
/// als unabhaengige Belege, weil beide aus derselben Gate-Berechnung stammen koennen.
/// Fuer AutoAccept muss der Datenbank-Abgleich den Code zusaetzlich bestaetigen.
/// </summary>
public sealed class StandardAiDecisionPolicy : IAiDecisionPolicy
{
    public const double AutoAcceptConfidence = 0.92;
    public const double RejectConfidence = 0.60;
    public const double MaxEpistemicUncertainty = 0.15;

    public static StandardAiDecisionPolicy Default { get; } = new();

    public AiDecision Decide(AiDecisionSignals s)
    {
        // Ungueltige Zahlen sind Datenfehler, keine Meinung (Review 11.07., Befund 3):
        // NaN laesst jeden Vergleich fehlschlagen und wuerde sonst bis AutoAccept
        // durchrutschen. Confidence muss ein echter Wert in [0..1] sein.
        if (double.IsNaN(s.Confidence) || double.IsInfinity(s.Confidence)
            || s.Confidence is < 0.0 or > 1.0)
            return new AiDecision(AiDecisionOutcome.Reject,
                $"Ungueltige Sicherheit ({s.Confidence}) — Datenfehler, kein Wert in 0..1.");

        // Unsicherheit: vorhanden, aber unbrauchbar (NaN/Inf) darf nie als "niedrig" gelten.
        if (s.EpistemicUncertainty is { } eu && (double.IsNaN(eu) || double.IsInfinity(eu)))
            return new AiDecision(AiDecisionOutcome.Review,
                "Unsicherheitswert unbrauchbar (NaN/unendlich).");

        // Rote Ampel oder sehr niedrige Sicherheit -> sofort ablehnen.
        if (s.QualityGate == TrafficLight.Red)
            return new AiDecision(AiDecisionOutcome.Reject, "QualityGate steht auf Rot.");
        if (s.Confidence < RejectConfidence)
            return new AiDecision(AiDecisionOutcome.Reject, $"Sicherheit zu niedrig ({s.Confidence:P0}).");

        // Pflichtwerte des Gates. Sie sind noch kein unabhaengiger Zweitbeleg.
        if (s.Confidence < AutoAcceptConfidence)
            return new AiDecision(AiDecisionOutcome.Review, $"Sicherheit unter {AutoAcceptConfidence:P0}.");
        if (s.QualityGate != TrafficLight.Green)
            return new AiDecision(AiDecisionOutcome.Review, "QualityGate nicht auf Gruen.");

        // Der KB-Abgleich ist der explizite, unabhaengige Zweitbeleg.
        if (s.KbAgreement == false)
            return new AiDecision(AiDecisionOutcome.Review, "Datenbank-Abgleich widerspricht.");
        if (s.KbAgreement != true)
            return new AiDecision(AiDecisionOutcome.Review, "Unabhaengiger Datenbank-Abgleich fehlt.");
        if (s.EpistemicUncertainty is { } u && u >= MaxEpistemicUncertainty)
            return new AiDecision(AiDecisionOutcome.Review, $"Unsicherheit zu hoch ({u:F2}).");

        return new AiDecision(AiDecisionOutcome.AutoAccept, "Alle vorhandenen Belege bestaetigt.");
    }
}
