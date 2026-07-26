using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingEventDeleteResult(
    bool RemovedFromList,
    bool ShouldClearSelectedDefect);

public static class CodingEventDeleteApplier
{
    public static CodingEventDeleteResult Apply(
        CodingEvent codingEvent,
        ICodingSessionService? codingSessionService,
        ICollection<CodingEvent>? codingEvents,
        CodingEvent? selectedDefect)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        codingSessionService?.RemoveEvent(codingEvent.EventId);
        var removedFromList = codingEvents?.Remove(codingEvent) == true;
        var shouldClearSelectedDefect = ReferenceEquals(selectedDefect, codingEvent);

        return new CodingEventDeleteResult(removedFromList, shouldClearSelectedDefect);
    }
}
