using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingPhotoViewerWorkflowServiceFactory
{
    public static CodingPhotoViewerWorkflowService Create()
        => new(
            CodingProjectFolderResolver.ResolveOrEmpty,
            (owner, codingEvent, projectFolder) =>
                CodingPhotoViewerWindowServiceFactory.Create().Show(owner, codingEvent, projectFolder));

    public static CodingPhotoViewerWorkflowService Create(
        ICodingDefectPreviewRenderer previewRenderer)
    {
        ArgumentNullException.ThrowIfNull(previewRenderer);

        return new CodingPhotoViewerWorkflowService(
            CodingProjectFolderResolver.ResolveOrEmpty,
            (owner, codingEvent, projectFolder) =>
                CodingPhotoViewerWindowServiceFactory.Create(previewRenderer)
                    .Show(owner, codingEvent, projectFolder));
    }
}
