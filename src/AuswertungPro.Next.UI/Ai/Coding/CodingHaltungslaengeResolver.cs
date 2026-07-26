using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingHaltungslaengeResolver
{
    public static bool TryEnsureFromKnownSources(HaltungRecord record, double? overlayPipeLengthMeters)
    {
        if (HasValidLength(record, "Haltungslaenge_m"))
            return true;

        if (HasValidLength(record, "Laenge_m"))
        {
            record.SetFieldValue(
                "Haltungslaenge_m",
                record.GetFieldValue("Laenge_m"),
                FieldSource.Legacy,
                userEdited: false);
            return true;
        }

        if (overlayPipeLengthMeters is > 0)
        {
            record.SetFieldValue(
                "Haltungslaenge_m",
                overlayPipeLengthMeters.Value.ToString("F2", CultureInfo.InvariantCulture),
                FieldSource.Legacy,
                userEdited: false);
            return true;
        }

        var maxProtocolMeter = record.Protocol?.Current?.Entries
            .Where(e => e.MeterStart.HasValue && e.MeterStart.Value > 0)
            .Select(e => e.MeterStart!.Value)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        if (maxProtocolMeter > 0)
        {
            record.SetFieldValue(
                "Haltungslaenge_m",
                maxProtocolMeter.ToString("F2", CultureInfo.InvariantCulture),
                FieldSource.Legacy,
                userEdited: false);
            return true;
        }

        return false;
    }

    public static bool HasValidLength(HaltungRecord record, string fieldName)
    {
        var raw = record.GetFieldValue(fieldName);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalized = raw.Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            && value > 0;
    }
}
