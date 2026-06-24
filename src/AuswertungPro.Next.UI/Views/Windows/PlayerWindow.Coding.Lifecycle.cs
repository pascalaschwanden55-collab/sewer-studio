using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            CodingModeDialogServiceFactory.Create().ShowMissingHaltung();
            return;
        }

        EnterCodingMode();
    }

    private void EnterCodingMode()
    {
        CodingModeEnterWorkflow.Execute(
            new CodingModeEnterWorkflowRequest(
                _isCodingMode,
                _haltungRecord is not null),
            new CodingModeEnterWorkflowActions(
                SetCodingMode: value => _isCodingMode = value,
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
                SetCodingNavigationPending: value => _codingNavPending = value,
                SyncVideoToCodingMeter: SyncVideoToCodingMeter));
    }

}
