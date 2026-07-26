using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using UiServiceProvider = AuswertungPro.Next.UI.ServiceProvider;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingProtocolPreviewDisplayWorkflowActions(
    Func<CodingProtocolPreviewWorkflowService> CreateService);

public static class CodingProtocolPreviewDisplayWorkflow
{
    public static bool TryShow(
        Window? owner,
        HaltungRecord record,
        ProtocolDocument document,
        UiServiceProvider serviceProvider,
        string? videoPath,
        string? lastProjectPath,
        Action markDirty)
        => TryShow(
            owner,
            record,
            document,
            serviceProvider,
            videoPath,
            lastProjectPath,
            markDirty,
            new CodingProtocolPreviewDisplayWorkflowActions(
                CreateService: CodingProtocolPreviewWorkflowServiceFactory.Create));

    public static bool TryShow(
        Window? owner,
        HaltungRecord record,
        ProtocolDocument document,
        UiServiceProvider serviceProvider,
        string? videoPath,
        string? lastProjectPath,
        Action markDirty,
        CodingProtocolPreviewDisplayWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(markDirty);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        return service.TryShow(owner, record, document, serviceProvider, videoPath, lastProjectPath, markDirty);
    }
}
