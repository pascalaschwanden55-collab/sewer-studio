using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerKeyboardPlaybackCommandRunner
{
    public static void Stop(Action stopPlayer)
        => stopPlayer();

    public static void Pause(Action<bool> setPause)
        => setPause(true);

    public static void Resume(Action ensurePlaying, Action<bool> setPause)
    {
        ensurePlaying();
        setPause(false);
    }
}
