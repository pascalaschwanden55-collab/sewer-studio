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
        var playerWindow = LastOpenedWindow.Current;
        var result = PlayerLastOpenedPlaybackWorkflow.TryGetCurrentTime(
            new PlayerLastOpenedCurrentTimeRequest(playerWindow is not null),
            new PlayerLastOpenedCurrentTimeActions(
                () =>
                {
                    var success = playerWindow!.TryGetCurrentTimeInternal(out var currentTime);
                    return new PlayerLastOpenedCurrentTimeActionResult(success, currentTime);
                }));

        time = result.Time;
        return result.Success;
    }

    public static bool TrySeekTo(TimeSpan time)
    {
        var playerWindow = LastOpenedWindow.Current;
        return PlayerLastOpenedPlaybackWorkflow.TrySeekTo(
                new PlayerLastOpenedSeekRequest(
                    playerWindow is not null,
                    time),
                new PlayerLastOpenedSeekActions(
                    requestedTime => playerWindow!.TrySeekToInternal(requestedTime)))
            .Success;
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
                _playerTimerController.StartUpdateTimer,
                UpdateRateLabel));

    private void UpdateUi()
        => PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                _positionSliderStateController.IsDragging,
                _codingModeState.IsCodingMode,
                _playerTimelineHost.TimeMilliseconds ?? 0,
                _playerTimelineHost.LengthMilliseconds ?? 0),
            new PlayerUiUpdateWorkflowActions(
                _positionControls.ApplyPlaybackState,
                UpdateRateLabel,
                UpdateCodingCurrentCode));

    // Quick-Scan
}
