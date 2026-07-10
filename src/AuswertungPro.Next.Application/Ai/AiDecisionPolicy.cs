using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Einheitlicher, schichtenunabhaengiger Vertrag fuer automatische KI-Freigaben.
/// Confidence-Zonen duerfen fuer die Darstellung verwendet werden, eine automatische
/// Freigabe benoetigt jedoch immer den vollstaendigen Sicherheitsnachweis.
/// </summary>
public sealed record AiDecisionEvidence(
    double Confidence,
    string? QualityGateLevel,
    bool? KbCodeAgreement,
    double? EpistemicUncertainty);

public sealed record AiDecisionPolicyResult(
    bool IsAutoApproved,
    string Reason,
    string PolicyVersion);

public static class AiDecisionPolicy
{
    public const string CurrentVersion = "ai-decision-policy-v1";
    public const double AutoApprovalThreshold = 0.92;
    public const double MaxEpistemicUncertainty = 0.15;

    public static AiDecisionPolicyResult Evaluate(AiDecisionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.Confidence < AutoApprovalThreshold)
        {
            return Reject(
                $"Confidence {evidence.Confidence:P0} liegt unter {AutoApprovalThreshold:P0}.");
        }

        if (!string.Equals(evidence.QualityGateLevel, "Green", StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                $"Quality Gate ist {evidence.QualityGateLevel ?? "nicht vorhanden"}, erwartet wird Green.");
        }

        if (evidence.KbCodeAgreement != true)
        {
            return Reject(evidence.KbCodeAgreement == false
                ? "Wissensbasis widerspricht dem vorgeschlagenen Code."
                : "Keine bestaetigte Uebereinstimmung mit der Wissensbasis vorhanden.");
        }

        if (!evidence.EpistemicUncertainty.HasValue)
            return Reject("Epistemische Unsicherheit ist nicht bestimmt.");

        if (evidence.EpistemicUncertainty.Value > MaxEpistemicUncertainty)
        {
            return Reject(
                $"Epistemische Unsicherheit {evidence.EpistemicUncertainty.Value:P0} ist zu hoch.");
        }

        return new AiDecisionPolicyResult(
            true,
            "Alle Auto-Freigabekriterien erfuellt.",
            CurrentVersion);
    }

    private static AiDecisionPolicyResult Reject(string reason)
        => new(false, reason, CurrentVersion);
}
