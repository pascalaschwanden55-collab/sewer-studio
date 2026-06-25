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
                ConfirmEmptyProtocol: guard => CodingApplyDialogServiceFactory.Create().ConfirmEmptyProtocol(guard),
                AssignProtocol: document => _haltungRecord!.Protocol = document,
                MarkProjectDirty: MarkProjectDirtyForCoding,
                SyncCodingToPrimaryDamages: SyncCodingToPrimaryDamages,
                PersistCodingEventsAsTrainingSamples: PersistCodingEventsAsTrainingSamples,
                SetBaselineSignature: signature => _codingBaselineSignature = signature,
                SaveProjectAfterCoding: SaveProjectAfterCoding,
                ShowOverlay: ShowOverlay));

        return result.Applied;
    }

    private bool ConfirmUnappliedCodingChangesOnClose()
    {
        var result = CodingUnappliedChangesCloseWorkflow.Execute(
            new CodingUnappliedChangesCloseWorkflowRequest(
                IsCodingMode: _isCodingMode,
                HasCodingViewModel: _codingSessionHost.HasViewModel,
                Events: _codingSessionHost.Events,
                BaselineSignature: _codingBaselineSignature),
            new CodingUnappliedChangesCloseWorkflowActions(
                BuildSignature: CodingEventsSignatureBuilder.Build,
                ConfirmWithSuspendedOverlay: () => RunWithSuspendedCodingOverlayInput(() =>
                    CodingApplyDialogServiceFactory.Create()
                        .ConfirmUnappliedChangesOnClose(() => ApplyCodingChanges(showOverlay: false)))));

        return result.ShouldClose;
    }

    private void MarkProjectDirtyForCoding()
    {
        CodingProjectPersistenceServiceFactory.Create().MarkProjectDirty(_haltungRecord);
    }

    private void SaveProjectAfterCoding()
    {
        // Nur speichern, wenn das Projekt bereits einen Pfad hat. Sonst wuerde TrySaveProject
        // mitten im Codieren oder beim Fensterschliessen einen Speichern-unter-Dialog oeffnen.
        CodingProjectPersistenceServiceFactory.Create().TrySaveProjectIfReady();
    }
}
