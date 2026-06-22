namespace AuswertungPro.Next.UI.Ai;

public static class CodingCurrentMeterResolver
{
    public static double Resolve(
        double? osdMeter,
        long playerTimeMs,
        long playerLengthMs,
        double endMeter,
        double sessionCurrentMeter)
    {
        if (osdMeter.HasValue)
            return osdMeter.Value;

        if (playerLengthMs > 0 && endMeter > 0)
            return (playerTimeMs / (double)playerLengthMs) * endMeter;

        return sessionCurrentMeter;
    }

    public static double ResolveManualEntry(
        double? osdMeter,
        double? cachedOsdMeter,
        long playerTimeMs,
        long playerLengthMs,
        double endMeter,
        double sessionCurrentMeter)
    {
        var timelineMeter = sessionCurrentMeter;
        if (playerLengthMs > 0 && endMeter > 0)
            timelineMeter = Math.Round((playerTimeMs / (double)playerLengthMs) * endMeter, 2);

        return Math.Round(Math.Max(0, osdMeter ?? cachedOsdMeter ?? timelineMeter), 2);
    }
}
