namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerSeekPreviewText(
    string CurrentTimeText,
    string DurationText);

public static class PlayerPlaybackState
{
    public const float MinRate = 0.25f;
    public const float MaxRate = 8.0f;

    public static float ClampRate(float rate)
        => Math.Clamp(rate, MinRate, MaxRate);

    public static float ApplyRateDelta(float currentRate, float delta)
    {
        var baseRate = currentRate <= 0f ? 1.0f : currentRate;
        return ClampRate(baseRate + delta);
    }

    public static long AddSeconds(long currentTimeMs, long durationMs, int deltaSeconds)
    {
        var next = currentTimeMs + deltaSeconds * 1000L;
        return Math.Clamp(next, 0, Math.Max(0, durationMs));
    }

    public static long ResolveSeekTargetMs(TimeSpan requestedTime, long durationMs)
    {
        var ms = (long)Math.Max(0, requestedTime.TotalMilliseconds);
        if (durationMs > 0 && ms > durationMs)
            return durationMs;

        return ms;
    }

    public static string FormatMilliseconds(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }

    public static bool TryResolveSliderRatio(double sliderValue, double sliderMaximum, out double ratio)
    {
        if (sliderMaximum <= 0)
        {
            ratio = 0;
            return false;
        }

        ratio = Math.Clamp(sliderValue / sliderMaximum, 0.0, 1.0);
        return true;
    }

    public static string FormatRateLabel(float rate)
    {
        var normalized = NormalizeRate(rate);
        return $"{normalized:0.##}x";
    }

    public static float NormalizeRate(float rate)
        => rate <= 0f ? 1.0f : rate;

    public static PlayerSeekPreviewText BuildSeekPreviewText(double ratio, long durationMs)
    {
        if (durationMs > 0)
        {
            var targetMs = (long)(ratio * durationMs);
            return new PlayerSeekPreviewText(
                FormatMilliseconds(targetMs),
                FormatMilliseconds(durationMs));
        }

        return new PlayerSeekPreviewText($"{ratio:P0}", "--:--");
    }
}
