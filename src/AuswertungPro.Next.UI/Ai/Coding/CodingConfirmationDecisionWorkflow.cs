using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingConfirmationDecisionWorkflow
{
    public static bool Accept(
        CodingEvent? pendingEvent,
        QualityGateResult? gateResult,
        Action<CodingEvent> persistTrainingSample)
    {
        if (!CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
                pendingEvent,
                CodingUserDecision.Accepted,
                gateResult))
            return false;

        ArgumentNullException.ThrowIfNull(persistTrainingSample);
        persistTrainingSample(pendingEvent!);
        return true;
    }

    public static CodingEvent? Edit(
        CodingEvent? pendingEvent,
        QualityGateResult? gateResult)
    {
        if (pendingEvent is null)
            return null;

        CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
            pendingEvent,
            CodingUserDecision.AcceptedWithEdit,
            gateResult);

        return pendingEvent;
    }

    public static bool Reject(
        CodingEvent? pendingEvent,
        QualityGateResult? gateResult,
        ICodingSessionService? codingSessionService,
        ICollection<CodingEvent>? codingEvents,
        Action<CodingEvent> persistTrainingSample,
        Action refreshEvents)
    {
        if (pendingEvent is null)
            return false;

        ArgumentNullException.ThrowIfNull(persistTrainingSample);
        ArgumentNullException.ThrowIfNull(refreshEvents);

        CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
            pendingEvent,
            CodingUserDecision.Rejected,
            gateResult);

        persistTrainingSample(pendingEvent);
        CodingEventDeleteApplier.Apply(pendingEvent, codingSessionService, codingEvents, selectedDefect: null);
        refreshEvents();
        return true;
    }
}
