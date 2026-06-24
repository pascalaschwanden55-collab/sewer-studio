using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerPositionSliderDragPlayback
{
    public static bool Start(bool isPlaying, Action<bool> setPause)
    {
        if (!isPlaying)
            return false;

        setPause(true);
        return true;
    }

    public static void Complete(bool wasPlayingBeforeDrag, Action<bool> setPause)
    {
        if (wasPlayingBeforeDrag)
            setPause(false);
    }
}
