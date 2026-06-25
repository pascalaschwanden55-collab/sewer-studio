using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    public static bool TryShowOverlayOnLast(string text, TimeSpan duration)
    {
        return PlayerLastOverlayDisplayWorkflow.Show(
            new PlayerLastOverlayDisplayWorkflowRequest(_lastOpened is not null),
            new PlayerLastOverlayDisplayWorkflowActions(
                ShowOverlay: () => _lastOpened!.ShowOverlay(text, duration)))
            .Handled;
    }

    private void ShowOverlay(string text, TimeSpan duration)
        => PlayerOverlayDisplayWorkflow.Show(
            new PlayerOverlayDisplayWorkflowRequest(
                _playbackDisposed,
                text,
                duration),
            new PlayerOverlayDisplayWorkflowActions(
                ShowMarquee: _playerMarqueeOverlayHost.Show,
                ScheduleDisable: (disableAfter, disable) =>
                {
                    var timer = PlayerWindowTimerFactory.CreateOneShotTimer(disableAfter, disable);
                    timer.Start();
                },
                DisableMarquee: _playerMarqueeOverlayHost.Disable));
}
