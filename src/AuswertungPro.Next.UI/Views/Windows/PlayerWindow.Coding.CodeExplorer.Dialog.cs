using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private Func<string?> CreateVsaCodeExplorerLiveSnapshotProvider()
        => () =>
        {
            var snapPath = CodingLiveSnapshotPathPolicy.CreateTempPath();
            return TakeSnapshotSafe(snapPath) ? snapPath : null;
        };

    private CodingCodeExplorerEditWorkflowActions CreateCodingCodeExplorerEditActions()
        => new(
            CreateService: () => CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel),
            CreateLiveSnapshotProvider: CreateVsaCodeExplorerLiveSnapshotProvider,
            RunWithSuspendedOverlayInput: callback => RunWithSuspendedCodingOverlayInput(callback));
}
