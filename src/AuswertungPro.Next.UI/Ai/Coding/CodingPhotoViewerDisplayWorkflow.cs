using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingPhotoViewerDisplayWorkflowActions(
    Func<CodingPhotoViewerWorkflowService> CreateService);

public static class CodingPhotoViewerDisplayWorkflow
{
    public static void Show(
        Window owner,
        CodingEvent codingEvent,
        string? lastProjectPath)
        => Show(
            owner,
            codingEvent,
            lastProjectPath,
            new CodingPhotoViewerDisplayWorkflowActions(
                CreateService: CodingPhotoViewerWorkflowServiceFactory.Create));

    public static void Show(
        Window owner,
        CodingEvent codingEvent,
        string? lastProjectPath,
        ICodingDefectPreviewRenderer previewRenderer)
    {
        ArgumentNullException.ThrowIfNull(previewRenderer);

        Show(
            owner,
            codingEvent,
            lastProjectPath,
            new CodingPhotoViewerDisplayWorkflowActions(
                CreateService: () =>
                    CodingPhotoViewerWorkflowServiceFactory.Create(previewRenderer)));
    }

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
