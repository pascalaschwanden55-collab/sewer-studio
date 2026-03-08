using System;
using AuswertungPro.Next.UI.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Auto-approve detections when all confidence criteria are met:
/// - Confidence >= 0.92
/// - KbCodeAgreement = true
/// - TrafficLight = Green
/// - EpistemicUncertainty < 0.15
/// </summary>
public sealed class AutoApprovalService
{
    public double MinConfidence { get; set; } = 0.92;
    public double MaxEpistemicUncertainty { get; set; } = 0.15;

    /// <summary>Determines if a mapped entry can be auto-approved.</summary>
    public AutoApprovalResult Evaluate(MappedProtocolEntry entry)
    {
        if (entry.QualityGateResult is null)
            return AutoApprovalResult.Rejected("Kein QualityGate-Ergebnis vorhanden.");

        if (!entry.QualityGateResult.IsGreen)
            return AutoApprovalResult.Rejected($"TrafficLight ist {entry.QualityGateResult.TrafficLight}, nicht Green.");

        if (entry.Confidence < MinConfidence)
            return AutoApprovalResult.Rejected($"Confidence {entry.Confidence:F2} < {MinConfidence:F2}.");

        var evidence = entry.Detection.Evidence;
        if (evidence?.KbCodeAgreement != true)
            return AutoApprovalResult.Rejected("KB-Code stimmt nicht überein.");

        if (entry.Uncertainty is { EpistemicUncertainty: var ep } && ep >= MaxEpistemicUncertainty)
            return AutoApprovalResult.Rejected($"Epistemische Unsicherheit {ep:F2} >= {MaxEpistemicUncertainty:F2}.");

        return AutoApprovalResult.Approved(
            $"Auto-Approved: Conf={entry.Confidence:F2}, Green, KB-Agree, Epistemic={entry.Uncertainty?.EpistemicUncertainty:F2}");
    }
}

public sealed record AutoApprovalResult(bool IsApproved, string Reason)
{
    public static AutoApprovalResult Approved(string reason) => new(true, reason);
    public static AutoApprovalResult Rejected(string reason) => new(false, reason);
}
