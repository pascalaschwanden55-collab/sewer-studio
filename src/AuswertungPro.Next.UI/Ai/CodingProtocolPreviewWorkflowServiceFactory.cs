using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolPreviewWorkflowServiceFactory
{
    public static CodingProtocolPreviewWorkflowService Create()
        => new(
            observationCount => CodingProtocolDialogServiceFactory.Create().ConfirmProtocolPreview(observationCount),
            () => PlayerShellProjectServiceFactory.Create().GetCurrentProject(),
            CodingProjectFolderResolver.ResolveNullable,
            (owner, record, project, serviceProvider, videoPath, projectFolder, markDirty) =>
                CodingProtocolPreviewWindowServiceFactory.Create().Show(
                    owner!,
                    record,
                    project,
                    serviceProvider,
                    videoPath,
                    projectFolder,
                    markDirty));
}
