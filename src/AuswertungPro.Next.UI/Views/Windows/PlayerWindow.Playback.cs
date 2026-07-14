using System;
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
        => _playerPlaybackController.TryGetCurrentTime(out time);

    private bool TrySeekToInternal(TimeSpan time)
        => _playerPlaybackController.TrySeekTo(time);

    private void TogglePlayPause()
        => _playerPlaybackController.TogglePlayPause();

    private void EnsurePlaying()
        => _playerPlaybackController.EnsurePlaying();

    private void JumpSeconds(int seconds)
        => _playerPlaybackController.JumpSeconds(seconds);

    private void Play(string path)
        => _playerPlaybackController.Play(path);

    private void UpdateUi()
        => _playerPlaybackController.UpdateUi();

    // Quick-Scan
}
