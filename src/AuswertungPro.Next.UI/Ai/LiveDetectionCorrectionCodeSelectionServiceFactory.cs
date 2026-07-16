using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionCorrectionCodeSelectionServiceFactory
{
    public static LiveDetectionCorrectionCodeSelectionService Create(
        Func<ProtocolEntry, double?, TimeSpan?, VsaCodeExplorerViewModel> createViewModel,
        ICodeUsageTracker? codeUsage = null)
        => new(
            createViewModel,
            (viewModel, videoPath, videoTime, owner) =>
                VsaCodeExplorerDialogServiceFactory.Create(codeUsage).Show(viewModel, videoPath, videoTime, owner));
}
