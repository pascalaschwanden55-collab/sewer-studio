using System.IO;
using LibVLCSharp.Shared;
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
            Directory.CreateDirectory(target.DirectoryPath);
            snapshotPath = target.FilePath;

            return playerWindow.TakeSnapshotSafe(snapshotPath);
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
            wasPlaying = _player.IsPlaying;
            if (wasPlaying)
            {
                _player.SetPause(true);
                System.Threading.Thread.Sleep(60);
            }
            if (_closing || _playbackDisposed)
                return false;

            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => _player.SetMarqueeInt(VideoMarqueeOption.Enable, PlayerMarqueeOverlayPolicy.DisabledEnable),
                "VLC: Marquee deaktivieren");
            return _player.TakeSnapshot(0, filePath, width, height);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (wasPlaying && !_closing && !_playbackDisposed)
                AuswertungPro.Next.Application.Common.BestEffort.Try(
                    () => _player.SetPause(false),
                    "VLC: Pause aufheben");
        }
    }
}
