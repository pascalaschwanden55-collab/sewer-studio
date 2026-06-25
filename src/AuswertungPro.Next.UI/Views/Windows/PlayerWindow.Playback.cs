using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

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
        => PlayerPlaybackGateway.TryGetCurrentTime(
            () => _playerTimelineHost.TimeMilliseconds ?? 0,
            out time);

    private bool TrySeekToInternal(TimeSpan time)
        => PlayerPlaybackGateway.TrySeekTo(
            time,
            () => _playerTimelineHost.LengthMilliseconds ?? 0,
            _playerTimelineHost.SeekMilliseconds,
            EnsurePlaying,
            UpdateUi);

    private void TogglePlayPause()
        => PlayerPlaybackCommandRunner.TogglePlayPause(
            EnsurePlaying,
            () => _playerPlaybackControlHost.IsPlaying,
            _playerPlaybackControlHost.SetPause);

    private void EnsurePlaying()
        => PlayerPlaybackStartWorkflow.EnsurePlaying(
            new PlayerPlaybackEnsurePlayingRequest(
                _playerPlaybackControlHost.ShouldStartPlayback,
                _videoPath),
            new PlayerPlaybackEnsurePlayingActions(Play));

    private void ChangeSpeed(float delta)
    {
        SetSpeed(AuswertungPro.Next.UI.Player.PlayerPlaybackState.ApplyRateDelta(_playerPlaybackControlHost.Rate, delta));
    }

    private void JumpSeconds(int seconds)
        => PlayerPlaybackCommandRunner.JumpSeconds(
            _playerTimelineHost.TimeMilliseconds ?? 0,
            _playerTimelineHost.LengthMilliseconds ?? 0,
            seconds,
            _playerTimelineHost.SeekMilliseconds,
            ClearDetectionOverlays,
            UpdateUi);

    private void Play(string path)
        => PlayerPlaybackStartWorkflow.Play(
            new PlayerPlaybackStartRequest(path),
            new PlayerPlaybackStartActions(
                _playerPlaybackControlHost.PlayPath,
                _timer.Start,
                UpdateRateLabel));

    private void UpdateUi()
        => PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                _isDragging,
                _isCodingMode,
                _playerTimelineHost.TimeMilliseconds ?? 0,
                _playerTimelineHost.LengthMilliseconds ?? 0),
            new PlayerUiUpdateWorkflowActions(
                _positionControls.ApplyPlaybackState,
                UpdateRateLabel,
                UpdateCodingCurrentCode));

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
