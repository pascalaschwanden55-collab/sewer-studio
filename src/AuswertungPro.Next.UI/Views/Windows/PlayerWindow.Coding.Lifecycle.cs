using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        CodingModeCommandWorkflow.Execute(
            new CodingModeCommandRequest(_haltungRecord is not null),
            new CodingModeCommandActions(
                ShowMissingHaltung: CodingModeDialogWorkflow.ShowMissingHaltung,
                EnterCodingMode: EnterCodingMode));
    }

    private void EnterCodingMode()
    {
        CodingModeEnterWorkflow.Execute(
            new CodingModeEnterWorkflowRequest(
                _codingModeState.IsCodingMode,
                _haltungRecord is not null),
            new CodingModeEnterWorkflowActions(
                SetCodingMode: _codingModeState.Set,
                ResetFrameReadiness: ResetFrameReadiness,
                PrepareCodingModePlayback: PrepareCodingModePlayback,
                CreateCodingSessionState: CreateCodingSessionState,
                ApplyCodingDnCalibration: ApplyCodingDnCalibration,
                EnsureHaltungslaenge: () => EnsureHaltungslaenge(_haltungRecord!),
                TryStartCodingSession: TryStartCodingSession,
                InitializeCodingImportReferences: InitializeCodingImportReferences,
                ActivateDefaultCodingTool: ActivateDefaultCodingTool,
                ShowCodingModeUi: ShowCodingModeUi,
                InitializeCodingTimeline: InitializeCodingTimeline,
                StartCodingModeBackgroundServices: StartCodingModeBackgroundServices,
                LoadExistingProtocolEventsAsImport: LoadExistingProtocolEventsAsImport,
                SetCodingNavigationPending: _codingNavigationPendingState.Set,
                SyncVideoToCodingMeter: SyncVideoToCodingMeter));
    }

}
