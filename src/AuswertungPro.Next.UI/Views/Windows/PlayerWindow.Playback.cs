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

    private bool TryGetCurrentTimeInternal(out TimeSpan time)
        => PlayerPlaybackGateway.TryGetCurrentTime(() => _player.Time, out time);

    private bool TrySeekToInternal(TimeSpan time)
        => PlayerPlaybackGateway.TrySeekTo(
            time,
            () => _player.Length,
            targetMs => _player.Time = targetMs,
            EnsurePlaying,
            UpdateUi);

    private void TogglePlayPause()
        => PlayerPlaybackCommandRunner.TogglePlayPause(
            EnsurePlaying,
            () => _player.IsPlaying,
            pause => _player.SetPause(pause));

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
        => PlayerPlaybackCommandRunner.JumpSeconds(
            _player.Time,
            _player.Length,
            seconds,
            targetMs => _player.Time = targetMs,
            ClearDetectionOverlays,
            UpdateUi);

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

        _positionControls.ApplyPlaybackState(_player.Time, _player.Length);
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
