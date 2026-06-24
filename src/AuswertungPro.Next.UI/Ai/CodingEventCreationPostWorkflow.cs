using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingEventCreationPostActions(
    Action RefreshEvents,
    Action<CodingEvent> SelectCreatedEvent,
    Action CancelSchema,
    Action ClearCurrentOverlay,
    Action ClearSelectedCode,
    Action RedrawCanvas,
    Action ClearSelectedCodeText,
    Action DisableCreateEvent,
    Action ClearOverlayInfo);

public sealed record CodingEventCreationPostOptions(
    bool SelectCreatedEvent,
    bool ClearSelectedCode);

public static class CodingEventCreationPostWorkflow
{
    public static bool Apply(
        CodingEvent? createdEvent,
        CodingEventCreationPostActions actions,
        CodingEventCreationPostOptions options)
    {
        if (createdEvent is null)
            return false;

        ArgumentNullException.ThrowIfNull(actions);

        actions.RefreshEvents();

        if (options.SelectCreatedEvent)
            actions.SelectCreatedEvent(createdEvent);

        actions.CancelSchema();
        actions.ClearCurrentOverlay();

        if (options.ClearSelectedCode)
            actions.ClearSelectedCode();

        actions.RedrawCanvas();
        actions.ClearSelectedCodeText();
        actions.DisableCreateEvent();
        actions.ClearOverlayInfo();
        return true;
    }
}
