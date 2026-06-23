namespace AuswertungPro.Next.UI.Ai;

public static class CodingPhotoViewerWorkflowServiceFactory
{
    public static CodingPhotoViewerWorkflowService Create()
        => new(
            CodingProjectFolderResolver.ResolveOrEmpty,
            (owner, codingEvent, projectFolder) =>
                CodingPhotoViewerWindowServiceFactory.Create().Show(owner, codingEvent, projectFolder));
}
