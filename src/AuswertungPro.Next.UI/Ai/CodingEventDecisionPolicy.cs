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
        if (codingEvent.AiContext is not null)
        {
            codingEvent.AiContext.Decision = decision;
            return;
        }

        codingEvent.ReviewContext ??= new CodingEventReviewContext
        {
            Reason = createdContextReason
        };
        codingEvent.ReviewContext.Decision = decision;
    }
}
