using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingInlineDefectRejectResult(
    bool Rejected,
    CodingEvent? Event,
    bool ShouldClearSelectedDefect);

public static class CodingInlineDefectDecisionWorkflow
{
    public static CodingEvent? Accept(
        Func<CodingEvent?> selectedDefectProvider,
        Action executeAcceptCommand,
        Action<CodingEvent> persistTrainingSample)
    {
        ArgumentNullException.ThrowIfNull(selectedDefectProvider);
        ArgumentNullException.ThrowIfNull(executeAcceptCommand);
        ArgumentNullException.ThrowIfNull(persistTrainingSample);

        executeAcceptCommand();

        var selectedDefect = selectedDefectProvider();
        if (selectedDefect is null)
            return null;

        persistTrainingSample(selectedDefect);
        return selectedDefect;
    }

    public static bool CompleteEdit(
        CodingEvent? codingEvent,
        ICodingSessionService? codingSessionService,
        Action executeEditCommand,
        Action<CodingEvent> persistTrainingSample)
    {
        if (codingEvent is null)
            return false;

        ArgumentNullException.ThrowIfNull(executeEditCommand);
        ArgumentNullException.ThrowIfNull(persistTrainingSample);

        CodingEventEditApplier.Apply(codingEvent, codingSessionService);

        if (codingEvent.AiContext != null)
            executeEditCommand();

        persistTrainingSample(codingEvent);
        return true;
    }

    public static CodingInlineDefectRejectResult Reject(
        CodingEvent? selectedDefect,
        CodingEvent? selectedListEvent,
        ICodingSessionService? codingSessionService,
        ICollection<CodingEvent>? codingEvents)
    {
        var codingEvent = selectedDefect ?? selectedListEvent;
        if (codingEvent is null || codingEvents is null)
            return new CodingInlineDefectRejectResult(false, null, ShouldClearSelectedDefect: false);

        var deleteResult = CodingEventDeleteApplier.Apply(
            codingEvent,
            codingSessionService,
            codingEvents,
            selectedDefect);

        return new CodingInlineDefectRejectResult(
            Rejected: true,
            codingEvent,
            deleteResult.ShouldClearSelectedDefect);
    }
}
