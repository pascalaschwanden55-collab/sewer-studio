using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private PlayerKeyboardActionController? _keyboardActions;

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _keyboardActions ??= new PlayerKeyboardActionController(new PlayerKeyboardActionBindings
        {
            CancelCodingOverlay = CancelCodingOverlayShortcut,
            TogglePlayPause = TogglePlayPause,
            Stop = () => PlayerKeyboardPlaybackCommandRunner.Stop(_playerPlaybackControlHost.Stop),
            Pause = () => PlayerKeyboardPlaybackCommandRunner.Pause(_playerPlaybackControlHost.SetPause),
            Resume = () => PlayerKeyboardPlaybackCommandRunner.Resume(EnsurePlaying, _playerPlaybackControlHost.SetPause),
            ChangeSpeed = ChangeSpeed,
            JumpSeconds = JumpSeconds,
            ToggleDetection = ToggleDetectionShortcut,
            ToggleMarkTool = ToggleMarkToolShortcut
        });

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
                CodingOverlayCanvas.IsMouseCaptured,
                _codingSessionHost.HasViewModel,
                CodingOverlayPopup.IsOpen),
            new PlayerCancelCodingOverlayShortcutWorkflowActions(
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                CancelSchema: _codingSchemaManager.Cancel,
                ReleaseMouseCapture: CodingOverlayCanvas.ReleaseMouseCapture,
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                DisableCreateEvent: () => CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false),
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null),
                RedrawCodingCanvasWithoutManualOverlay: () => RedrawCodingCanvas(includeManualOverlay: false)));

    private void ToggleDetectionShortcut()
    {
        PlayerDetectionShortcutWorkflow.Execute(
            new PlayerDetectionShortcutWorkflowRequest(
                _isCodingMode,
                BtnCodingLiveAi.IsChecked == true,
                LiveDetectionButton.IsChecked == true),
            new PlayerDetectionShortcutWorkflowActions(
                SetCodingLiveAiChecked: isChecked => BtnCodingLiveAi.IsChecked = isChecked,
                InvokeCodingLiveAi: () => CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs()),
                SetLiveDetectionChecked: isChecked => LiveDetectionButton.IsChecked = isChecked,
                InvokeLiveDetection: () => LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs())));
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
