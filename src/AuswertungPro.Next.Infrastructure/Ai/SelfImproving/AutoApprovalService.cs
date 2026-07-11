using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Duenner Adapter: baut die Belege eines KI-Befunds aus dem MappedProtocolEntry und
/// laesst die zentrale IAiDecisionPolicy entscheiden (Audit Fix 3). Keine eigene Schwellen-Logik mehr.
/// </summary>
public sealed class AutoApprovalService
{
    private readonly IAiDecisionPolicy _policy;

    public AutoApprovalService(IAiDecisionPolicy? policy = null)
        => _policy = policy ?? StandardAiDecisionPolicy.Default;

    /// <summary>
    /// Volles 3-stufiges Urteil (AutoAccept/Review/Reject) — Fehlerpruefung 11.07.,
    /// Wichtig 1: die bool-Reduktion verlor die Unterscheidung Grenzfall vs. Datenfehler.
    /// </summary>
    public AiDecision Decide(MappedProtocolEntry entry)
    {
        var signals = new AiDecisionSignals(
            Confidence: entry.Confidence,
            QualityGate: entry.QualityGateResult?.TrafficLight,
            KbAgreement: entry.Detection.Evidence?.KbCodeAgreement,
            EpistemicUncertainty: entry.Uncertainty?.EpistemicUncertainty);

        return _policy.Decide(signals);
    }

    public AutoApprovalResult Evaluate(MappedProtocolEntry entry)
    {
        var decision = Decide(entry);
        return decision.Outcome == AiDecisionOutcome.AutoAccept
            ? AutoApprovalResult.Approved(decision.Reason)
            : AutoApprovalResult.Rejected(decision.Reason);
    }

    /// <summary>Sichtbarer Hinweis mit allen DREI Ausgaengen.</summary>
    public static string AlsHinweis(AiDecision decision)
        => decision.Outcome switch
        {
            AiDecisionOutcome.AutoAccept => $"Zentrale Freigabe: verlaesslich — {decision.Reason}",
            AiDecisionOutcome.Reject => $"Zentrale Freigabe: ablehnen — {decision.Reason}",
            _ => $"Zentrale Freigabe: pruefen — {decision.Reason}"
        };

    /// <summary>
    /// Formatiert das Freigabe-Urteil als sichtbaren Hinweis fuer Warnings/Ai.Flags
    /// (Review 11.07., Empfehlung 2: die Vollanalyse zeigt Ergebnis UND Grund an —
    /// automatisch uebernommen wird weiterhin nichts).
    /// </summary>
    public static string AlsHinweis(AutoApprovalResult result)
        => result.IsApproved
            ? $"Zentrale Freigabe: verlaesslich — {result.Reason}"
            : $"Zentrale Freigabe: pruefen — {result.Reason}";
}

public sealed record AutoApprovalResult(bool IsApproved, string Reason)
{
    public static AutoApprovalResult Approved(string reason) => new(true, reason);
    public static AutoApprovalResult Rejected(string reason) => new(false, reason);
}
