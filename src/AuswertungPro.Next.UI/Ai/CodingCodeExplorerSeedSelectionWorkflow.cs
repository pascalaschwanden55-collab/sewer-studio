using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingCodeExplorerSeedSelectionWorkflowRequest(
    OverlayGeometry? Overlay,
    double? PresetMeter,
    TimeSpan VideoTime,
    string? VideoPath,
    Window Owner);

public sealed record CodingCodeExplorerSeedSelectionWorkflowActions(
    Func<CodingCodeExplorerWorkflowService> CreateService);

public static class CodingCodeExplorerSeedSelectionWorkflow
{
    public static ProtocolEntry? Execute(
        CodingCodeExplorerSeedSelectionWorkflowRequest request,
        CodingCodeExplorerSeedSelectionWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        return service.SelectSeed(
            request.Overlay,
            request.PresetMeter,
            request.VideoTime,
            request.VideoPath,
            request.Owner);
    }
}
