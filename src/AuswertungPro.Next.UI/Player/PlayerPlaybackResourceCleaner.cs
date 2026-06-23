using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerPlaybackResourceCleaner
{
    public static void DetachVideoView(Action detachVideoView)
        => AuswertungPro.Next.Application.Common.BestEffort.Try(
            detachVideoView,
            "VLC: VideoView trennen");

    public static void StopPlayer(Action stopPlayer)
        => AuswertungPro.Next.Application.Common.BestEffort.Try(
            stopPlayer,
            "VLC: Player stoppen");

    public static void DisposeMediaPlayer(IDisposable mediaPlayer, Action<string> trace)
        => DisposeResource(mediaPlayer.Dispose, trace, "MediaPlayer");

    public static void DisposeLibVlc(IDisposable libVlc, Action<string> trace)
        => DisposeResource(libVlc.Dispose, trace, "LibVLC");

    private static void DisposeResource(Action dispose, Action<string> trace, string resourceName)
    {
        try
        {
            dispose();
        }
        catch (Exception ex)
        {
            trace($"[PlayerWindow] {resourceName} Dispose error: {ex.Message}");
        }
    }
}
