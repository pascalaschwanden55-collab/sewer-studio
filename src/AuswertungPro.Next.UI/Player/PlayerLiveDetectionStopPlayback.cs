using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerLiveDetectionStopPlayback
{
    public static void PauseIfRunning(
        bool hasPlayer,
        bool isPlaybackDisposed,
        bool isPlaying,
        Action<bool> setPause)
    {
        if (hasPlayer && !isPlaybackDisposed && isPlaying)
            setPause(true);
    }
}
