using AuswertungPro.Next.Application.Ai;
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

    /// <summary>
    /// Meter fuer einen Handeintrag: frische OSD-Lesung, sonst der gemerkte OSD-Meter, sonst die
    /// Videoposition, sonst der Sitzungswert. Der gemerkte Wert gilt nur, wenn er hoechstens
    /// <see cref="CodingMeterResolver.RecentOsdMeterMaxAgeSeconds"/> alt ist — dieselbe Regel wie
    /// bei der Anzeige (Entscheid Pascal 02.10.2026, E2). Ohne Zeitstempel gilt er nicht.
    /// </summary>
    public static double ResolveManualEntry(
        double? osdMeter,
        double? cachedOsdMeter,
        long playerTimeMs,
        long playerLengthMs,
        double endMeter,
        double sessionCurrentMeter,
        double? cachedOsdTimestampSeconds = null)
    {
        var timelineMeter = sessionCurrentMeter;
        if (playerLengthMs > 0 && endMeter > 0)
            timelineMeter = Math.Round((playerTimeMs / (double)playerLengthMs) * endMeter, 2);

        var recentOsdMeter = CodingMeterResolver.ResolveRecentOsdMeter(
            playerTimeMs / 1000.0,
            cachedOsdMeter,
            cachedOsdTimestampSeconds);

        return Math.Round(Math.Max(0, osdMeter ?? recentOsdMeter ?? timelineMeter), 2);
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
