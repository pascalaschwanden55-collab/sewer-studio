using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerCodingPlayback
{
    public static void PauseForCodingInteraction(Action<bool> setPause)
        => setPause(true);
}
