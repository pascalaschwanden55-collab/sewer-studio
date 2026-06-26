using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private PlayerKeyboardActionController? _keyboardActions;

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _keyboardActions ??= PlayerKeyboardActionControllerFactory.Create(
            new PlayerKeyboardActionControllerFactoryActions(
                CancelCodingOverlay: CancelCodingOverlayShortcut,
                TogglePlayPause: TogglePlayPause,
                StopPlayback: _playerPlaybackControlHost.Stop,
                SetPause: _playerPlaybackControlHost.SetPause,
                EnsurePlaying: EnsurePlaying,
                ChangeSpeed: ChangeSpeed,
                JumpSeconds: JumpSeconds,
                ToggleDetection: ToggleDetectionShortcut,
                ToggleMarkTool: ToggleMarkToolShortcut));

        var action = PlayerKeyboardShortcutPolicy.Resolve(e.Key, _codingOverlayToolHost.HasOverlayService);
        PlayerKeyboardInputWorkflow.Execute(
            new PlayerKeyboardInputWorkflowRequest(action),
            new PlayerKeyboardInputWorkflowActions(
                ExecuteAction: _keyboardActions.Execute,
                MarkHandled: () => { e.Handled = true; }));
    }

    private void CancelCodingOverlayShortcut()
        => PlayerCancelCodingOverlayShortcutWorkflow.Execute(
            new PlayerCancelCodingOverlayShortcutWorkflowRequest(
                CodingOverlayInputControls.IsCanvasMouseCaptured(CodingOverlayCanvas),
                _codingSessionHost.HasViewModel,
                CodingOverlayPopup.IsOpen),
            new PlayerCancelCodingOverlayShortcutWorkflowActions(
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                CancelSchema: _codingSchemaManager.Cancel,
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(CodingOverlayCanvas),
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                DisableCreateEvent: () => CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false),
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null),
                RedrawCodingCanvasWithoutManualOverlay: () => RedrawCodingCanvas(includeManualOverlay: false)));

    private void ToggleDetectionShortcut()
    {
        PlayerDetectionShortcutWorkflow.Execute(
            new PlayerDetectionShortcutWorkflowRequest(
                _codingModeState.IsCodingMode,
                BtnCodingLiveAi.IsChecked == true,
                LiveDetectionButton.IsChecked == true),
            PlayerDetectionShortcutControls.CreateActions(
                BtnCodingLiveAi,
                LiveDetectionButton,
                CodingLiveAi_Click,
                LiveDetection_Click));
    }

    private void ToggleMarkToolShortcut()
    {
        PlayerMarkToolShortcutWorkflow.Execute(
            new PlayerMarkToolShortcutWorkflowRequest(_liveDetectionController.MarkToolType),
            new PlayerMarkToolShortcutWorkflowActions(
                DeactivateMarkTool,
                ToggleMarkToolPopup: () => _markToolControls.ToggleManualMarkPopup(isCodingMode: false)));
    }
}
