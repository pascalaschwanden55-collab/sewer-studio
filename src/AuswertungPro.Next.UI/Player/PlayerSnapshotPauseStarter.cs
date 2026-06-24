using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerSnapshotPauseStarter
{
    public static bool PauseIfPlaying(bool isPlaying, Action pausePlayback, Action waitAfterPause)
    {
        if (!isPlaying)
            return false;

        pausePlayback();
        waitAfterPause();
        return true;
    }

    public static bool PauseIfPlaying(bool isPlaying, Action<bool> setPause)
        => PauseIfPlaying(isPlaying, () => setPause(true), () => PlayerSnapshotPauseDelay.WaitAfterPause());
}
