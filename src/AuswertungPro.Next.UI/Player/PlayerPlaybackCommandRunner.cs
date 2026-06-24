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

    public static void SetSpeed(
        float rate,
        Func<float, int> setRate,
        Action<float> showUnsupportedRate,
        Action updateRateLabel)
    {
        var clamped = PlayerPlaybackState.ClampRate(rate);
        if (setRate(clamped) != 0)
            showUnsupportedRate(clamped);

        updateRateLabel();
    }

    public static void TogglePlayPause(Action ensurePlaying, Func<bool> isPlaying, Action<bool> setPause)
    {
        ensurePlaying();
        setPause(isPlaying());
    }

    public static bool JumpSeconds(
        long currentTimeMs,
        long durationMs,
        int seconds,
        Action<long> setTimeMs,
        Action clearDetectionOverlays,
        Action updateUi)
    {
        if (durationMs <= 0)
            return false;

        setTimeMs(PlayerPlaybackState.AddSeconds(currentTimeMs, durationMs, seconds));
        clearDetectionOverlays();
        updateUi();
        return true;
    }
}
