using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Live;

public static class LiveDetectionMarkCatalogWorkflowServiceFactory
{
    public static LiveDetectionMarkCatalogWorkflowService Create(
        Func<bool> hasCodeCatalog,
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        Action<ProtocolEntry> onEntryCreated,
        Action<string> showOverlay,
        ICodeUsageTracker? codeUsage = null)
        => new(
            hasCodeCatalog,
            () => LiveDetectionDialogServiceFactory.Create().ShowCodeCatalogUnavailable(),
            createViewModel,
            (viewModel, videoPath, videoTime, owner) =>
                VsaCodeExplorerDialogServiceFactory.Create(codeUsage).Show(viewModel, videoPath, videoTime, owner),
            onEntryCreated,
            showOverlay);
}
