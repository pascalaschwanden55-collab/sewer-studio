using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingCodeExplorerEditWorkflowRequest(
    CodingEvent CodingEvent,
    string? VideoPath,
    TimeSpan? CurrentVideoTime,
    Window Owner);

public sealed record CodingCodeExplorerEditWorkflowActions(
    Func<CodingCodeExplorerWorkflowService> CreateService,
    Func<Func<string?>> CreateLiveSnapshotProvider,
    Func<Func<bool>, bool> RunWithSuspendedOverlayInput);

public static class CodingCodeExplorerEditWorkflow
{
    public static bool Execute(
        CodingCodeExplorerEditWorkflowRequest request,
        CodingCodeExplorerEditWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.CodingEvent);
        ArgumentNullException.ThrowIfNull(request.CodingEvent.Entry);
        ArgumentNullException.ThrowIfNull(actions.CreateService);
        ArgumentNullException.ThrowIfNull(actions.CreateLiveSnapshotProvider);
        ArgumentNullException.ThrowIfNull(actions.RunWithSuspendedOverlayInput);

        return actions.RunWithSuspendedOverlayInput(() =>
        {
            var entry = request.CodingEvent.Entry;
            var service = actions.CreateService();
            ArgumentNullException.ThrowIfNull(service);

            return service.TryEdit(
                entry,
                entry.MeterStart,
                entry.Zeit,
                request.VideoPath,
                request.CurrentVideoTime,
                request.Owner,
                actions.CreateLiveSnapshotProvider());
        });
    }
}
