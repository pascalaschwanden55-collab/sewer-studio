using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai.Coding;

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

    public static double ParseDisplayedMeterOrZero(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var normalized = text.Replace("m", "", StringComparison.OrdinalIgnoreCase).Trim();
        return FachzahlParser.TryParseMeasurement(normalized, out var meter)
            ? (double)meter
            : 0;
    }
}
