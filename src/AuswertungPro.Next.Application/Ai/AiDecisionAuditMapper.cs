using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ueberfuehrt eine zentrale Entscheidung verlustfrei in die Domain-Persistenz.</summary>
public static class AiDecisionAuditMapper
{
    public static AiDecisionAudit Create(
        AiDecision decision,
        string? visionModel = null,
        string? textModel = null,
        string? qualityGateVersion = null,
        IReadOnlyDictionary<string, double>? qualityGateWeights = null,
        string? qualityGateExplanation = null)
    {
        var signals = decision.Signals ?? new AiDecisionSignals(0.0);
        var thresholds = decision.Thresholds ?? StandardAiDecisionPolicy.CurrentThresholds;

        return new AiDecisionAudit
        {
            Outcome = decision.Outcome.ToString(),
            ReasonCode = decision.ReasonCode.ToString(),
            Reason = decision.Reason,
            PolicyVersion = decision.PolicyVersion,
            Signals = new AiDecisionSignalAudit
            {
                Confidence = signals.Confidence,
                QualityGate = signals.QualityGate?.ToString(),
                KbAgreement = signals.KbAgreement,
                EpistemicUncertainty = signals.EpistemicUncertainty
            },
            Thresholds = new AiDecisionThresholdAudit
            {
                AutoAcceptConfidence = thresholds.AutoAcceptConfidence,
                RejectConfidence = thresholds.RejectConfidence,
                MaxEpistemicUncertainty = thresholds.MaxEpistemicUncertainty
            },
            VisionModel = NullIfBlank(visionModel),
            TextModel = NullIfBlank(textModel),
            QualityGateVersion = NullIfBlank(qualityGateVersion),
            QualityGateWeights = qualityGateWeights is null
                ? new Dictionary<string, double>(StringComparer.Ordinal)
                : new Dictionary<string, double>(qualityGateWeights, StringComparer.Ordinal),
            QualityGateExplanation = NullIfBlank(qualityGateExplanation),
            DecidedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public static AiDecisionAudit CaptureCodingEvent(
        CodingEvent codingEvent,
        string? visionModel = null,
        string? textModel = null,
        string? qualityGateVersion = null)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(codingEvent.AiContext);

        var context = codingEvent.AiContext;
        var decision = DefectStatusPolicy.GetCentralDecision(context);
        var audit = Create(
            decision,
            visionModel,
            textModel,
            qualityGateVersion,
            context.QualityGateWeights,
            context.QualityGateExplanation);

        context.CentralDecision = audit;
        ApplyToEntry(codingEvent.Entry, context, audit);
        return audit;
    }

    private static void ApplyToEntry(
        ProtocolEntry entry,
        CodingEventAiContext context,
        AiDecisionAudit audit)
    {
        entry.Ai ??= new ProtocolEntryAiMeta();
        entry.Ai.SuggestedCode = context.SuggestedCode;
        entry.Ai.Confidence = context.Confidence;
        entry.Ai.Reason = context.Reason;
        entry.Ai.Accepted = context.Decision is
            CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit;
        entry.Ai.FinalCode = entry.Code;
        entry.Ai.CentralDecision = AiDecisionAuditCloner.Clone(audit);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
