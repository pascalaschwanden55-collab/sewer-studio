namespace AuswertungPro.Next.UI.Player;

public static class PlayerPlaybackGateway
{
    public static bool TryGetCurrentTime(Func<long> getCurrentTimeMs, out TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(getCurrentTimeMs);

        time = default;
        try
        {
            time = TimeSpan.FromMilliseconds(Math.Max(0, getCurrentTimeMs()));
            return true;
        }
        catch
        {
            time = default;
            return false;
        }
    }

    public static bool TrySeekTo(
        TimeSpan requestedTime,
        Func<long> getDurationMs,
        Action<long> setTimeMs,
        Action ensurePlaying,
        Action updateUi)
    {
        ArgumentNullException.ThrowIfNull(getDurationMs);
        ArgumentNullException.ThrowIfNull(setTimeMs);
        ArgumentNullException.ThrowIfNull(ensurePlaying);
        ArgumentNullException.ThrowIfNull(updateUi);

        try
        {
            ensurePlaying();
            setTimeMs(PlayerPlaybackState.ResolveSeekTargetMs(requestedTime, getDurationMs()));
            updateUi();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
