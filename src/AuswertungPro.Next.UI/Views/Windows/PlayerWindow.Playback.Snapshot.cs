using System;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame als PNG.
    /// </summary>
    public static bool TryTakeSnapshot(out string snapshotPath)
    {
        snapshotPath = string.Empty;
        var playerWindow = _lastOpened;
        if (playerWindow is null || playerWindow._closing || playerWindow._playbackDisposed)
            return false;
        var currentTime = playerWindow._playerTimelineHost.CurrentTime;
        if (!playerWindow._playerPlaybackControlHost.IsPlaying && (!currentTime.HasValue || currentTime.Value <= TimeSpan.Zero))
            return false;

        try
        {
            var target = PlayerSnapshotPathPolicy.Create();
            return PlayerSnapshotFileCaptureServiceFactory.Create()
                .TryCapture(target, path => playerWindow.TakeSnapshotSafe(path), out snapshotPath);
        }
        catch
        {
            return false;
        }
    }

    private bool TakeSnapshotSafe(string filePath, uint width = 0, uint height = 0)
    {
        if (_closing || _playbackDisposed)
            return false;

        var wasPlaying = false;
        try
        {
            wasPlaying = PlayerSnapshotPauseStarter.PauseIfPlaying(
                _playerPlaybackControlHost.IsPlaying,
                _playerPlaybackControlHost.SetPause);
            if (_closing || _playbackDisposed)
                return false;

            _playerMarqueeOverlayHost.Disable();
            return _player.TakeSnapshot(0, filePath, width, height);
        }
        catch
        {
            return false;
        }
        finally
        {
            PlayerSnapshotPauseRestorer.ResumeIfNeeded(
                wasPlaying,
                _closing,
                _playbackDisposed,
                _playerPlaybackControlHost.SetPause);
        }
    }
}
