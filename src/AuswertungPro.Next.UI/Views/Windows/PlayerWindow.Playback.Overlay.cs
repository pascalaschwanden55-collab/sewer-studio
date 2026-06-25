using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    public static bool TryShowOverlayOnLast(string text, TimeSpan duration)
    {
        if (_lastOpened is null)
            return false;
        _lastOpened.ShowOverlay(text, duration);
        return true;
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
