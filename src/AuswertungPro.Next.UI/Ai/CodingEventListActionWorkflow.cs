using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingEventListDeleteResult(
    bool Deleted,
    bool ShouldClearSelectedDefect);

public sealed record CodingEventCloseStretchActionResult(
    bool Applied,
    bool RequiresLaterMeterPrompt,
    bool ShouldRefreshEvents,
    string StatusText);

public static class CodingEventListActionWorkflow
{
    public static bool CompleteEdit(
        CodingEvent? codingEvent,
        ICodingSessionService? codingSessionService,
        Action refreshEvents)
    {
        if (codingEvent is null)
            return false;

        ArgumentNullException.ThrowIfNull(refreshEvents);

        CodingEventEditApplier.Apply(codingEvent, codingSessionService);
        refreshEvents();
        return true;
    }

    public static CodingEventListDeleteResult Delete(
        CodingEvent? codingEvent,
        ICodingSessionService? codingSessionService,
        ICollection<CodingEvent>? codingEvents,
        CodingEvent? selectedDefect)
    {
        if (codingEvent is null)
            return new CodingEventListDeleteResult(Deleted: false, ShouldClearSelectedDefect: false);

        var deleteResult = CodingEventDeleteApplier.Apply(
            codingEvent,
            codingSessionService,
            codingEvents,
            selectedDefect);

        return new CodingEventListDeleteResult(
            Deleted: true,
            deleteResult.ShouldClearSelectedDefect);
    }

    public static CodingEventCloseStretchActionResult CloseStretch(
        CodingEvent? startEvent,
        ICodingSessionService? codingSessionService,
        double currentMeter,
        TimeSpan currentVideoTime)
    {
        if (startEvent is null || codingSessionService is null)
            return new CodingEventCloseStretchActionResult(
                Applied: false,
                RequiresLaterMeterPrompt: false,
                ShouldRefreshEvents: false,
                StatusText: "");

        var closeResult = CodingStretchDamageManualCloseApplier.Apply(
            startEvent,
            currentMeter,
            currentVideoTime,
            codingSessionService);

        if (closeResult.Kind == CodingStretchDamageManualCloseResultKind.RequiresLaterMeter)
            return new CodingEventCloseStretchActionResult(
                Applied: true,
                RequiresLaterMeterPrompt: true,
                ShouldRefreshEvents: false,
                StatusText: "");

        return new CodingEventCloseStretchActionResult(
            Applied: true,
            RequiresLaterMeterPrompt: false,
            ShouldRefreshEvents: true,
            closeResult.StatusText ?? "");
    }
}
