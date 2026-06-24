using System;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerSliderSeekController
{
    public static bool SeekToSlider(
        double sliderValue,
        double sliderMaximum,
        long durationMs,
        Action<long> setTimeMs,
        Action<float> setPosition,
        Action updateUi)
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(sliderValue, sliderMaximum, durationMs);
        if (!target.IsValid)
            return false;

        ApplySliderSeekTarget(target, setTimeMs, setPosition);
        updateUi();
        return true;
    }

    public static bool UpdateSeekPreview(
        double sliderValue,
        double sliderMaximum,
        long durationMs,
        bool isDragging,
        bool isScrubTimerEnabled,
        Action<double, long> applySeekPreview,
        Action startScrubTimer)
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(sliderValue, sliderMaximum, durationMs);
        if (!target.IsValid)
            return false;

        applySeekPreview(target.Ratio, durationMs);

        if (isDragging && !isScrubTimerEnabled)
            startScrubTimer();

        return true;
    }

    public static bool ScrubSeekToSlider(
        double sliderValue,
        double sliderMaximum,
        long durationMs,
        Action<long> setTimeMs,
        Action<float> setPosition,
        Action<double, long> applyScrubPreview)
    {
        var target = PlayerPlaybackState.ResolveSliderSeekTarget(sliderValue, sliderMaximum, durationMs);
        if (!target.IsValid)
            return false;

        ApplySliderSeekTarget(target, setTimeMs, setPosition);
        applyScrubPreview(target.Ratio, durationMs);
        return true;
    }

    private static void ApplySliderSeekTarget(
        PlayerSliderSeekTarget target,
        Action<long> setTimeMs,
        Action<float> setPosition)
    {
        if (target.TimeMs.HasValue)
            setTimeMs(target.TimeMs.Value);
        else if (target.Position.HasValue)
            setPosition(target.Position.Value);
    }
}
