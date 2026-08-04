using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingCodeExplorerWorkflowServiceFactory
{
    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        ICodeUsageTracker? codeUsage = null)
        => Create(createViewModel, codeUsage, services: null);

    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        ICodeUsageTracker? codeUsage,
        ServiceProvider? services)
        => new(
            createViewModel,
            (viewModel, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
                VsaCodeExplorerDialogServiceFactory.Create(codeUsage, services).Show(
                    viewModel,
                    videoPath,
                    currentVideoTime,
                    owner,
                    liveSnapshotProvider));
}
