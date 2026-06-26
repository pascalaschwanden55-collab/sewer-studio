using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingCodeExplorerManualEntryWorkflowRequest(
    OverlayGeometry? Overlay,
    double? Meter,
    TimeSpan VideoTime,
    string? VideoPath,
    Window Owner);

public sealed record CodingCodeExplorerManualEntryWorkflowActions(
    Func<CodingCodeExplorerWorkflowService> CreateService,
    Func<Func<string?>> CreateLiveSnapshotProvider);

public static class CodingCodeExplorerManualEntryWorkflow
{
    public static ProtocolEntry? Execute(
        CodingCodeExplorerManualEntryWorkflowRequest request,
        CodingCodeExplorerManualEntryWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);
        ArgumentNullException.ThrowIfNull(actions.CreateLiveSnapshotProvider);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        return service.CreateManualEntry(
            request.Overlay,
            request.Meter,
            request.VideoTime,
            request.VideoPath,
            request.Owner,
            actions.CreateLiveSnapshotProvider());
    }
}
