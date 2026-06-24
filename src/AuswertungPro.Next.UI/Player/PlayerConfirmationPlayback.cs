using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerConfirmationPlayback
{
    public static void PauseCodingConfirmation(Action<bool> setPause)
        => setPause(true);

    public static void ResumeCodingLiveAi(bool isLiveAiEnabled, Action<bool> setPause)
    {
        if (isLiveAiEnabled)
            setPause(false);
    }

    public static void PauseLiveDetectionConfirmation(bool isPlaying, Action<bool> setPause)
    {
        if (isPlaying)
            setPause(true);
    }
}
