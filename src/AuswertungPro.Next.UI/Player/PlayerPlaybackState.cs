namespace AuswertungPro.Next.UI.Player;

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
}
