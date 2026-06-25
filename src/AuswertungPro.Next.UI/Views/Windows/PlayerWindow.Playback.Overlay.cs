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
    {
        if (_playbackDisposed)
            return;

        try
        {
            var marquee = PlayerMarqueeOverlayPolicy.BuildShow(text);
            _playerMarqueeOverlayHost.Show(marquee);

            var t = PlayerWindowTimerFactory.CreateOneShotTimer(duration, () =>
            {
                _playerMarqueeOverlayHost.Disable();
            });
            t.Start();
        }
        catch
        {
            // ignore overlay errors
        }
    }
}
