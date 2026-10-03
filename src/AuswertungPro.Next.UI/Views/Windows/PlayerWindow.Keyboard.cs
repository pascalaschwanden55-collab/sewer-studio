using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        => _keyboardPresenter.HandleKey(e);

    private void ShowShortcutOverlay_Click(object sender, System.Windows.RoutedEventArgs e)
        => _keyboardPresenter.Show(e);

    private void CloseShortcutOverlay_Click(object sender, System.Windows.RoutedEventArgs e)
        => _keyboardPresenter.Hide(e);

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    // StaysOpen="False" schliesst den Aufklapper schon beim Klick auf diesen Knopf; ohne die
    // Zeitregel oeffnete ihn derselbe Klick sofort wieder (PopupToggle).
    private PopupToggle? _weitereToggle;

    private void WeitereDropdown_Click(object sender, System.Windows.RoutedEventArgs e)
        => (_weitereToggle ??= new PopupToggle(WeiterePopup)).Umschalten();

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
