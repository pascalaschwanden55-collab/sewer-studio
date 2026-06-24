using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerManualMarkPlayback
{
    public static void PauseForManualMarking(Action<bool> setPause)
        => setPause(true);
}
