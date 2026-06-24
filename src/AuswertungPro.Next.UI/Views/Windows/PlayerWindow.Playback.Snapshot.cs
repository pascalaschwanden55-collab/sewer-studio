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
        if (playerWindow._player is null || !playerWindow._player.IsPlaying && playerWindow._player.Time <= 0)
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
                _player.IsPlaying,
                _player.SetPause);
            if (_closing || _playbackDisposed)
                return false;

            PlayerMarqueeOverlayDisabler.Disable((option, value) => _player.SetMarqueeInt(option, value));
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
                _player.SetPause);
        }
    }
}
