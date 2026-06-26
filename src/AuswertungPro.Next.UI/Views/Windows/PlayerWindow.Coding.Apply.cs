using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => ApplyCodingChanges(showOverlay: true);

    private bool ApplyCodingChanges(bool showOverlay)
    {
        var result = CodingApplyChangesWorkflow.Execute(
            new CodingApplyChangesWorkflowRequest(
                _codingSessionHost.HasViewModel,
                _haltungRecord,
                _codingSessionHost.EventCollection,
                showOverlay),
            new CodingApplyChangesWorkflowActions(
                ConfirmEmptyProtocol: CodingApplyEmptyProtocolDialogWorkflow.Execute,
                AssignProtocol: document => _haltungRecord!.Protocol = document,
                MarkProjectDirty: MarkProjectDirtyForCoding,
                SyncCodingToPrimaryDamages: SyncCodingToPrimaryDamages,
                PersistCodingEventsAsTrainingSamples: PersistCodingEventsAsTrainingSamples,
                SetBaselineSignature: _codingBaselineSignatureState.Set,
                SaveProjectAfterCoding: SaveProjectAfterCoding,
                ShowOverlay: ShowOverlay));

        return result.Applied;
    }

    private bool ConfirmUnappliedCodingChangesOnClose()
    {
        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                IsCodingMode: _codingModeState.IsCodingMode,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Events: _codingSessionHost.Events,
                BaselineSignature: _codingBaselineSignatureState.BaselineSignature),
            new CodingUnappliedChangesCloseWorkflowActions(
                BuildSignature: CodingEventsSignatureBuilder.Build,
                ConfirmWithSuspendedOverlay: () => CodingUnappliedChangesCloseDialogWorkflow.Execute(
                    runWithSuspendedOverlay: callback => RunWithSuspendedCodingOverlayInput(callback),
                    applyChanges: () => ApplyCodingChanges(showOverlay: false))));

        return result.ShouldClose;
    }

    private void MarkProjectDirtyForCoding()
    {
        CodingProjectPersistenceWorkflow.MarkProjectDirty(_haltungRecord);
    }

    private void SaveProjectAfterCoding()
    {
        // Nur speichern, wenn das Projekt bereits einen Pfad hat. Sonst wuerde TrySaveProject
        // mitten im Codieren oder beim Fensterschliessen einen Speichern-unter-Dialog oeffnen.
        CodingProjectPersistenceWorkflow.TrySaveProjectIfReady();
    }
}
