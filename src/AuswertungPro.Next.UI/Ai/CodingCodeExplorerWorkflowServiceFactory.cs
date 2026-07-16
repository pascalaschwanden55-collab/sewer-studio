using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingCodeExplorerWorkflowServiceFactory
{
    public static CodingCodeExplorerWorkflowService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        ICodeUsageTracker? codeUsage = null)
        => new(
            createViewModel,
            (viewModel, videoPath, currentVideoTime, owner, liveSnapshotProvider) =>
                VsaCodeExplorerDialogServiceFactory.Create(codeUsage).Show(
                    viewModel,
                    videoPath,
                    currentVideoTime,
                    owner,
                    liveSnapshotProvider));
}
