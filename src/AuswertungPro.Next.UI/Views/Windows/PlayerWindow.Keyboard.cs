using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var overlayOutcome = _shortcutOverlayController.HandleKey(e.Key);
        if (overlayOutcome == PlayerShortcutOverlayKeyOutcome.Handled)
            e.Handled = true;

        if (overlayOutcome != PlayerShortcutOverlayKeyOutcome.Continue)
            return;

        var keyboardActions = _keyboardActionControllerOwner.Ensure(
            new PlayerKeyboardActionControllerFactoryActions(
                CancelCodingOverlay: CancelCodingOverlayShortcut,
                TogglePlayPause: TogglePlayPause,
                StopPlayback: _playerPlaybackControlHost.Stop,
                SetPause: _playerPlaybackControlHost.SetPause,
                EnsurePlaying: EnsurePlaying,
                ChangeSpeed: _playerControlInputController.ChangeSpeed,
                JumpSeconds: JumpSeconds,
                ToggleDetection: ToggleDetectionShortcut,
                ToggleMarkTool: ToggleMarkToolShortcut));

        var action = PlayerKeyboardShortcutPolicy.Resolve(e.Key, _codingOverlayToolHost.HasOverlayService);
        PlayerKeyboardInputWorkflow.Execute(
            new PlayerKeyboardInputWorkflowRequest(action),
            new PlayerKeyboardInputWorkflowActions(
                ExecuteAction: keyboardActions.Execute,
                MarkHandled: () => { e.Handled = true; }));
    }

    private void ShowShortcutOverlay_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        _shortcutOverlayController.Show();
    }

    private void CloseShortcutOverlay_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        e.Handled = true;
        _shortcutOverlayController.Hide();
    }

    private void ShortcutOverlayCard_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => e.Handled = true;

    private void CancelCodingOverlayShortcut()
        => PlayerCancelCodingOverlayShortcutWorkflow.Execute(
            new PlayerCancelCodingOverlayShortcutWorkflowRequest(
                CodingOverlayInputControls.IsCanvasMouseCaptured(CodingOverlayCanvas),
                _codingSessionHost.HasViewModel,
                CodingOverlayInputControls.IsPopupOpen(CodingOverlayPopup)),
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
                PlayerToggleButtonControls.IsChecked(BtnCodingLiveAi),
                PlayerToggleButtonControls.IsChecked(LiveDetectionButton)),
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
                _liveDetectionMarkToolController.Deactivate,
                ToggleMarkToolPopup: () => _liveDetectionMarkToolController.ToggleManualMarkPopup(isCodingMode: false)));
    }
}
