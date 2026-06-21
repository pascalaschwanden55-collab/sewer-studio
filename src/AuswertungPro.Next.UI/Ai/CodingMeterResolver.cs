namespace AuswertungPro.Next.UI.Ai;

public readonly record struct CodingMeterResolution(double Meter, bool IsOsd);

public static class CodingMeterResolver
{
    public const double RecentOsdMeterMaxAgeSeconds = 1.5;
    public const double OsdSeekResetGapSeconds = 6.0;
    private const double MaxPlausibleOsdMeter = 500.0;

    public static CodingMeterResolution Resolve(
        double? frameTimestampSeconds,
        double? sameFrameOsdMeter,
        double? cachedOsdMeter,
        double? cachedOsdTimestampSeconds,
        double? currentPlayerTimestampSeconds,
        double? videoDurationSeconds,
        double endMeter,
        double currentMeter)
    {
        if (sameFrameOsdMeter is >= 0 and <= MaxPlausibleOsdMeter)
            return new CodingMeterResolution(Math.Round(sameFrameOsdMeter.GetValueOrDefault(), 2), IsOsd: true);

        var recentOsdMeter = ResolveRecentOsdMeter(frameTimestampSeconds, cachedOsdMeter, cachedOsdTimestampSeconds);
        if (recentOsdMeter.HasValue)
            return new CodingMeterResolution(recentOsdMeter.Value, IsOsd: true);

        var videoMeter = EstimateFromVideo(frameTimestampSeconds, videoDurationSeconds, endMeter)
            ?? EstimateFromVideo(currentPlayerTimestampSeconds, videoDurationSeconds, endMeter);
        if (videoMeter.HasValue)
            return new CodingMeterResolution(videoMeter.Value, IsOsd: false);

        return new CodingMeterResolution(Math.Round(Math.Max(0, currentMeter), 2), IsOsd: false);
    }

    public static bool ShouldResetRecentMeterForSeek(double? frameTimestampSeconds, double? cachedOsdTimestampSeconds)
        => frameTimestampSeconds.HasValue
           && cachedOsdTimestampSeconds.HasValue
           && Math.Abs(frameTimestampSeconds.Value - cachedOsdTimestampSeconds.Value) > OsdSeekResetGapSeconds;

    private static double? ResolveRecentOsdMeter(
        double? frameTimestampSeconds,
        double? cachedOsdMeter,
        double? cachedOsdTimestampSeconds)
    {
        if (!IsPlausibleOsdMeter(cachedOsdMeter))
            return null;
        if (!frameTimestampSeconds.HasValue || !cachedOsdTimestampSeconds.HasValue)
            return null;

        var ageSeconds = Math.Abs(frameTimestampSeconds.Value - cachedOsdTimestampSeconds.Value);
        return ageSeconds <= RecentOsdMeterMaxAgeSeconds
            ? Math.Round(cachedOsdMeter.GetValueOrDefault(), 2)
            : null;
    }

    public static double? EstimateFromVideo(double? timestampSeconds, double? videoDurationSeconds, double endMeter)
    {
        if (!timestampSeconds.HasValue || !videoDurationSeconds.HasValue)
            return null;
        if (videoDurationSeconds.Value <= 0 || endMeter <= 0)
            return null;

        var fraction = Math.Clamp(timestampSeconds.Value / videoDurationSeconds.Value, 0.0, 1.0);
        return Math.Round(fraction * endMeter, 2);
    }

    private static bool IsPlausibleOsdMeter(double? meter)
        => meter is >= 0 and <= MaxPlausibleOsdMeter;
}
