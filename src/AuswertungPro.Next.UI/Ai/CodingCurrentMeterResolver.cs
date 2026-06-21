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
}
