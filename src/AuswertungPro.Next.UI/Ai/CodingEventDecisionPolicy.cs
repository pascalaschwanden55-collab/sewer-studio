using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventDecisionPolicy
{
    public static bool ApplyAiConfirmationDecision(
        CodingEvent? codingEvent,
        CodingUserDecision decision,
        QualityGateResult? gateResult)
    {
        if (codingEvent?.AiContext is null)
            return false;

        codingEvent.AiContext.Decision = decision;
        if (gateResult is not null)
            codingEvent.AiContext.QualityGateLevel = gateResult.TrafficLight.ToString();

        return true;
    }

    public static void ApplyManualReviewDecision(
        CodingEvent codingEvent,
        CodingUserDecision decision,
        string createdContextReason)
    {
        codingEvent.AiContext ??= new CodingEventAiContext
        {
            SuggestedCode = codingEvent.Entry.Code,
            Confidence = 1.0,
            Reason = createdContextReason
        };

        codingEvent.AiContext.Decision = decision;
    }
}
