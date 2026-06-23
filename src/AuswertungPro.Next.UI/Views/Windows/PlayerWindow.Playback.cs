using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
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

    public static bool TryGetCurrentTime(out TimeSpan time)
    {
        time = default;
        if (_lastOpened is null)
            return false;

        return _lastOpened.TryGetCurrentTimeInternal(out time);
    }

    public static bool TrySeekTo(TimeSpan time)
    {
        if (_lastOpened is null)
            return false;

        return _lastOpened.TrySeekToInternal(time);
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

    private bool TryGetCurrentTimeInternal(out TimeSpan time)
    {
        time = default;
        try
        {
            var ms = Math.Max(0, _player.Time);
            time = TimeSpan.FromMilliseconds(ms);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySeekToInternal(TimeSpan time)
    {
        try
        {
            EnsurePlaying();
            _player.Time = PlayerPlaybackState.ResolveSeekTargetMs(time, _player.Length);
            UpdateUi();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TogglePlayPause()
    {
        EnsurePlaying();
        _player.SetPause(_player.IsPlaying);
    }

    private void EnsurePlaying()
    {
        var state = _player.State;
        if (state == VLCState.Stopped || state == VLCState.Ended)
            Play(_videoPath);
    }

    private void ChangeSpeed(float delta)
    {
        SetSpeed(AuswertungPro.Next.UI.Player.PlayerPlaybackState.ApplyRateDelta(_player.Rate, delta));
    }

    private void JumpSeconds(int seconds)
    {
        if (_player.Length <= 0)
            return;

        _player.Time = AuswertungPro.Next.UI.Player.PlayerPlaybackState.AddSeconds(_player.Time, _player.Length, seconds);
        ClearDetectionOverlays(); // Alte Overlays bei Navigation entfernen
        UpdateUi();
    }

    private void Play(string path)
    {
        using var media = new Media(_libVlc, path, FromType.FromPath);
        _player.Play(media);
        _timer.Start();
        UpdateRateLabel();
    }

    private void UpdateUi()
    {
        if (_isDragging)
            return;

        var state = PlayerPlaybackState.BuildUiState(
            _player.Time,
            _player.Length,
            PositionSlider.Maximum);

        if (state.SliderValue.HasValue)
            PositionSlider.Value = state.SliderValue.Value;
        CurrentTimeText.Text = state.CurrentTimeText;
        DurationText.Text = state.DurationText;

        UpdateRateLabel();

        // Im Codier-Modus: Echtzeit-Code am Zeitstempel aktualisieren
        if (_isCodingMode)
            UpdateCodingCurrentCode();
    }

    private void EnsureVisibleOnScreen()
    {
        var bounds = PlayerWindowBoundsPolicy.ClampToWorkArea(
            new Rect(Left, Top, Width, Height),
            SystemParameters.WorkArea);

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    // Quick-Scan
}
