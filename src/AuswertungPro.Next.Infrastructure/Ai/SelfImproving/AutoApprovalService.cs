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

    public AutoApprovalResult Evaluate(MappedProtocolEntry entry)
    {
        var signals = new AiDecisionSignals(
            Confidence: entry.Confidence,
            QualityGate: entry.QualityGateResult?.TrafficLight,
            KbAgreement: entry.Detection.Evidence?.KbCodeAgreement,
            EpistemicUncertainty: entry.Uncertainty?.EpistemicUncertainty);

        var decision = _policy.Decide(signals);
        return decision.Outcome == AiDecisionOutcome.AutoAccept
            ? AutoApprovalResult.Approved(decision.Reason)
            : AutoApprovalResult.Rejected(decision.Reason);
    }
}

public sealed record AutoApprovalResult(bool IsApproved, string Reason)
{
    public static AutoApprovalResult Approved(string reason) => new(true, reason);
    public static AutoApprovalResult Rejected(string reason) => new(false, reason);
}
