using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
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
            Stop = () => _player.Stop(),
            Pause = () => _player.SetPause(true),
            Resume = () =>
            {
                EnsurePlaying();
                _player.SetPause(false);
            },
            ChangeSpeed = ChangeSpeed,
            JumpSeconds = JumpSeconds,
            ToggleDetection = ToggleDetectionShortcut,
            ToggleMarkTool = ToggleMarkToolShortcut
        });

        var action = PlayerKeyboardShortcutPolicy.Resolve(e.Key, _codingOverlayService != null);
        if (_keyboardActions.Execute(action))
            e.Handled = true;
    }

    private void CancelCodingOverlayShortcut()
    {
        _codingOverlayService?.CancelDraw();
        _codingSchemaManager.Cancel();
        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        if (_codingVm != null)
        {
            _codingVm.CurrentOverlay = null;
            BtnCodingCreateEvent.IsEnabled = false;
            UpdateCodingOverlayInfo(null);
        }
        if (CodingOverlayPopup.IsOpen)
            RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void ToggleDetectionShortcut()
    {
        if (_isCodingMode)
        {
            BtnCodingLiveAi.IsChecked = !(BtnCodingLiveAi.IsChecked == true);
            CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
        }
        else
        {
            LiveDetectionButton.IsChecked = !(LiveDetectionButton.IsChecked == true);
            LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs());
        }
    }

    private void ToggleMarkToolShortcut()
    {
        if (_markToolType != OverlayToolType.None)
            DeactivateMarkTool();
        else
            MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }
}
