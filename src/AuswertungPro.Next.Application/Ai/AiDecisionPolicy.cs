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
/// Kontextabhaengig streng: Pflicht sind hohe Sicherheit (>= 0.92) UND gruene Ampel
/// (zwei unabhaengige Belege). Jeder zusaetzlich vorhandene Beleg (Datenbank-Abgleich,
/// Unsicherheit) darf nicht widersprechen. Fehlende Belege werden nicht gefordert.
/// </summary>
public sealed class StandardAiDecisionPolicy : IAiDecisionPolicy
{
    public const double AutoAcceptConfidence = 0.92;
    public const double RejectConfidence = 0.60;
    public const double MaxEpistemicUncertainty = 0.15;

    public static StandardAiDecisionPolicy Default { get; } = new();

    public AiDecision Decide(AiDecisionSignals s)
    {
        // Rote Ampel oder sehr niedrige Sicherheit -> sofort ablehnen.
        if (s.QualityGate == TrafficLight.Red)
            return new AiDecision(AiDecisionOutcome.Reject, "QualityGate steht auf Rot.");
        if (s.Confidence < RejectConfidence)
            return new AiDecision(AiDecisionOutcome.Reject, $"Sicherheit zu niedrig ({s.Confidence:P0}).");

        // Zwei Pflicht-Belege fuer Gruen: hohe Sicherheit UND gruene Ampel.
        if (s.Confidence < AutoAcceptConfidence)
            return new AiDecision(AiDecisionOutcome.Review, $"Sicherheit unter {AutoAcceptConfidence:P0}.");
        if (s.QualityGate != TrafficLight.Green)
            return new AiDecision(AiDecisionOutcome.Review, "QualityGate nicht auf Gruen.");

        // Jeder zusaetzlich vorhandene Beleg darf nicht widersprechen.
        if (s.KbAgreement == false)
            return new AiDecision(AiDecisionOutcome.Review, "Datenbank-Abgleich widerspricht.");
        if (s.EpistemicUncertainty is { } u && u >= MaxEpistemicUncertainty)
            return new AiDecision(AiDecisionOutcome.Review, $"Unsicherheit zu hoch ({u:F2}).");

        return new AiDecision(AiDecisionOutcome.AutoAccept, "Alle vorhandenen Belege bestaetigt.");
    }
}
