using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerPlaybackCommandRunner
{
    public static void Play(
        Action ensurePlaying,
        Action<bool> setPause,
        Action updateRateLabel,
        Action clearDetectionOverlays)
    {
        ensurePlaying();
        setPause(false);
        updateRateLabel();
        clearDetectionOverlays();
    }

    public static void Pause(Action<bool> setPause, Action updateRateLabel)
    {
        setPause(true);
        updateRateLabel();
    }

    public static void Stop(Action stopPlayer, Action updateRateLabel)
    {
        stopPlayer();
        updateRateLabel();
    }
}
