using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ergebnis der zentralen Freigabe-Regel.</summary>
public enum AiDecisionOutcome
{
    AutoAccept, // Gruen: mehrere Belege erfuellen die noch unkalibrierten KI-Kriterien
    Review,     // Gelb: pruefen
    Reject      // Rot: unbedingt pruefen
}

/// <summary>Stabiler, auswertbarer Grund einer zentralen KI-Entscheidung.</summary>
public enum AiDecisionReasonCode
{
    Unspecified,
    InvalidConfidence,
    InvalidUncertainty,
    QualityGateRed,
    ConfidenceBelowReject,
    ConfidenceBelowAutoAccept,
    QualityGateNotGreen,
    KbDisagreement,
    KbMissing,
    UncertaintyTooHigh,
    EvidenceConfirmed
}

/// <summary>Schwellen, die fuer eine konkrete Entscheidung verwendet wurden.</summary>
public sealed record AiDecisionThresholds(
    double AutoAcceptConfidence,
    double RejectConfidence,
    double MaxEpistemicUncertainty);

/// <summary>
/// Freigabe-Entscheidung mit maschinenlesbarem Grund und vollstaendigem
/// Schnappschuss der zentralen Regel. Die Defaultwerte halten alte Aufrufer lesbar.
/// </summary>
public sealed record AiDecision(
    AiDecisionOutcome Outcome,
    string Reason,
    AiDecisionReasonCode ReasonCode = AiDecisionReasonCode.Unspecified,
    string PolicyVersion = "legacy",
    AiDecisionSignals? Signals = null,
    AiDecisionThresholds? Thresholds = null);

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
    public const string PolicyVersion = "central-ai-release-v2";
    public const double AutoAcceptConfidence = 0.92;
    public const double RejectConfidence = 0.60;
    public const double MaxEpistemicUncertainty = 0.15;

    public static AiDecisionThresholds CurrentThresholds { get; } = new(
        AutoAcceptConfidence,
        RejectConfidence,
        MaxEpistemicUncertainty);

    public static StandardAiDecisionPolicy Default { get; } = new();

    public AiDecision Decide(AiDecisionSignals s)
    {
        if (double.IsNaN(s.Confidence) || double.IsInfinity(s.Confidence)
            || s.Confidence is < 0.0 or > 1.0)
        {
            return Create(
                AiDecisionOutcome.Reject,
                AiDecisionReasonCode.InvalidConfidence,
                $"Ungueltige Sicherheit ({s.Confidence}) - Datenfehler, kein Wert in 0..1.",
                s);
        }

        if (s.EpistemicUncertainty is { } eu
            && (double.IsNaN(eu) || double.IsInfinity(eu) || eu is < 0.0 or > 1.0))
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.InvalidUncertainty,
                "Unsicherheitswert unbrauchbar (kein Wert in 0..1).",
                s);
        }

        if (s.QualityGate == TrafficLight.Red)
        {
            return Create(
                AiDecisionOutcome.Reject,
                AiDecisionReasonCode.QualityGateRed,
                "QualityGate steht auf Rot.",
                s);
        }

        if (s.Confidence < RejectConfidence)
        {
            return Create(
                AiDecisionOutcome.Reject,
                AiDecisionReasonCode.ConfidenceBelowReject,
                $"Sicherheit zu niedrig ({s.Confidence:P0}).",
                s);
        }

        if (s.Confidence < AutoAcceptConfidence)
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.ConfidenceBelowAutoAccept,
                $"Sicherheit unter {AutoAcceptConfidence:P0}.",
                s);
        }

        if (s.QualityGate != TrafficLight.Green)
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.QualityGateNotGreen,
                "QualityGate nicht auf Gruen.",
                s);
        }

        if (s.KbAgreement == false)
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.KbDisagreement,
                "Datenbank-Abgleich widerspricht.",
                s);
        }

        if (s.KbAgreement != true)
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.KbMissing,
                "Unabhaengiger Datenbank-Abgleich fehlt.",
                s);
        }

        if (s.EpistemicUncertainty is { } u && u >= MaxEpistemicUncertainty)
        {
            return Create(
                AiDecisionOutcome.Review,
                AiDecisionReasonCode.UncertaintyTooHigh,
                $"Unsicherheit zu hoch ({u:F2}).",
                s);
        }

        return Create(
            AiDecisionOutcome.AutoAccept,
            AiDecisionReasonCode.EvidenceConfirmed,
            "Alle vorhandenen Belege bestaetigt.",
            s);
    }

    private static AiDecision Create(
        AiDecisionOutcome outcome,
        AiDecisionReasonCode reasonCode,
        string reason,
        AiDecisionSignals signals)
        => new(
            outcome,
            reason,
            reasonCode,
            PolicyVersion,
            signals,
            CurrentThresholds);
}
