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

    private CodingCodeExplorerManualEntryWorkflowActions CreateCodingCodeExplorerManualEntryActions()
        => new(
            CreateService: () => CodingCodeExplorerServiceCreationWorkflow.Create(CreateVsaCodeExplorerViewModel, _protocolContext.CodeUsage),
            CreateLiveSnapshotProvider: CreateVsaCodeExplorerLiveSnapshotProvider);

    private CodingCodeExplorerSeedSelectionWorkflowActions CreateCodingCodeExplorerSeedSelectionActions()
        => new(
            CreateService: () => CodingCodeExplorerServiceCreationWorkflow.Create(CreateVsaCodeExplorerViewModel, _protocolContext.CodeUsage));

    private CodingCodeExplorerEditWorkflowActions CreateCodingCodeExplorerEditActions()
        => new(
            CreateService: () => CodingCodeExplorerServiceCreationWorkflow.Create(CreateVsaCodeExplorerViewModel, _protocolContext.CodeUsage),
            CreateLiveSnapshotProvider: CreateVsaCodeExplorerLiveSnapshotProvider,
            RunWithSuspendedOverlayInput: callback => _codingOverlayInputVisibilityController.Run(callback));
}
