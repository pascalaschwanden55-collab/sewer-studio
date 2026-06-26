using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingPhotoViewerDisplayWorkflowActions(
    Func<CodingPhotoViewerWorkflowService> CreateService);

public static class CodingPhotoViewerDisplayWorkflow
{
    public static void Show(
        Window owner,
        CodingEvent codingEvent,
        string? lastProjectPath,
        CodingPhotoViewerDisplayWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService();
        ArgumentNullException.ThrowIfNull(service);

        service.Show(owner, codingEvent, lastProjectPath);
    }
}
