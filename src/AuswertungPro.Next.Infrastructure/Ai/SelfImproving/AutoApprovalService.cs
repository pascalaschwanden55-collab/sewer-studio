using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Infrastruktur-Adapter auf die zentrale <see cref="AiDecisionPolicy"/>.
/// Dadurch gelten in Pipeline, Codiermodus und Fast-Mode dieselben Freigabekriterien.
/// </summary>
public sealed class AutoApprovalService
{
    public double MinConfidence => AiDecisionPolicy.AutoApprovalThreshold;
    public double MaxEpistemicUncertainty => AiDecisionPolicy.MaxEpistemicUncertainty;

    public AutoApprovalResult Evaluate(MappedProtocolEntry entry)
    {
        var policy = AiDecisionPolicy.Evaluate(new AiDecisionEvidence(
            entry.Confidence,
            entry.QualityGateResult?.TrafficLight.ToString(),
            entry.Detection.Evidence?.KbCodeAgreement,
            entry.Uncertainty?.EpistemicUncertainty));

        return policy.IsAutoApproved
            ? AutoApprovalResult.Approved(policy.Reason, policy.PolicyVersion)
            : AutoApprovalResult.Rejected(policy.Reason, policy.PolicyVersion);
    }
}

public sealed record AutoApprovalResult(bool IsApproved, string Reason, string PolicyVersion)
{
    public static AutoApprovalResult Approved(string reason, string policyVersion) =>
        new(true, reason, policyVersion);

    public static AutoApprovalResult Rejected(string reason, string policyVersion) =>
        new(false, reason, policyVersion);
}
