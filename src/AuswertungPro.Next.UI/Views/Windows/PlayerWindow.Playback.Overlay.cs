using System;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Player;
using LibVLCSharp.Shared;

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
        if (_player is null)
            return;

        try
        {
            var marquee = PlayerMarqueeOverlayPolicy.BuildShow(text);
            _player.SetMarqueeInt(VideoMarqueeOption.Enable, marquee.Enable);
            _player.SetMarqueeInt(VideoMarqueeOption.X, marquee.X);
            _player.SetMarqueeInt(VideoMarqueeOption.Y, marquee.Y);
            _player.SetMarqueeInt(VideoMarqueeOption.Size, marquee.Size);
            _player.SetMarqueeInt(VideoMarqueeOption.Color, marquee.Color);
            _player.SetMarqueeInt(VideoMarqueeOption.Opacity, marquee.Opacity);
            _player.SetMarqueeString(VideoMarqueeOption.Text, marquee.Text);

            var t = new DispatcherTimer { Interval = duration };
            t.Tick += (_, __) =>
            {
                t.Stop();
                AuswertungPro.Next.Application.Common.BestEffort.Try(
                    () => _player.SetMarqueeInt(VideoMarqueeOption.Enable, PlayerMarqueeOverlayPolicy.DisabledEnable),
                    "VLC: Marquee deaktivieren");
            };
            t.Start();
        }
        catch
        {
            // ignore overlay errors
        }
    }
}
