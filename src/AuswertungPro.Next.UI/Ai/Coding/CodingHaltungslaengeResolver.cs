using System.Globalization;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingHaltungslaengeResolver
{
    public static bool TryEnsureFromKnownSources(HaltungRecord record, double? overlayPipeLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (HasValidLength(record, "Haltungslaenge_m"))
            return true;

        if (HasValidLength(record, "Laenge_m"))
        {
            var source = record.FieldMeta.TryGetValue("Laenge_m", out var metadata)
                ? metadata.Source
                : FieldSource.Legacy;
            var userEdited = metadata?.UserEdited == true;

            record.SetFieldValue(
                "Haltungslaenge_m",
                record.GetFieldValue("Laenge_m"),
                source,
                userEdited);
            return true;
        }

        // Das Overlay wird im produktiven Weg aus Haltungslaenge_m aufgebaut und
        // ist daher keine unabhängige Quelle. Eine beliebige Schadensposition darf
        // ebenfalls nie als echte Haltungslänge in Bewertung und Kosten gelangen.
        _ = overlayPipeLengthMeters;

        var pipeEndMeter = TryReadUniqueActivePipeEndMeter(record);
        if (pipeEndMeter is > 0)
        {
            record.SetFieldValue(
                "Haltungslaenge_m",
                pipeEndMeter.Value.ToString("F2", CultureInfo.InvariantCulture),
                FieldSource.Protocol,
                userEdited: false);
            return true;
        }

        return false;
    }

    private static double? TryReadUniqueActivePipeEndMeter(HaltungRecord record)
    {
        var activeEntries = record.Protocol?.Current?.Entries
            .Where(entry => !entry.IsDeleted)
            .ToList();

        if (activeEntries is null
            || activeEntries.Any(entry =>
                entry.Code?.StartsWith(
                    ProtocolBoundaryService.AbortPrefix,
                    StringComparison.OrdinalIgnoreCase) == true))
        {
            return null;
        }

        var pipeEndMeters = activeEntries
            .Where(entry =>
                string.Equals(
                    entry.Code,
                    ProtocolBoundaryService.CodeRohrende,
                    StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.MeterStart ?? entry.MeterEnd)
            .Where(meter => meter is > 0)
            .Select(meter => Math.Round(meter!.Value, 2))
            .Distinct()
            .ToList();

        return pipeEndMeters is { Count: 1 } ? pipeEndMeters[0] : null;
    }

    /// <summary>
    /// Liest die bekannte Haltungslaenge in Metern. Null, wenn kein brauchbarer Wert
    /// vorliegt - dann darf kein Rohrende vorgeschlagen werden.
    /// </summary>
    public static double? TryReadHaltungslaenge(HaltungRecord? record)
    {
        if (record is null)
            return null;

        foreach (var fieldName in new[] { "Haltungslaenge_m", "Laenge_m" })
        {
            var raw = record.GetFieldValue(fieldName);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (double.TryParse(
                    raw.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value)
                && value > 0)
            {
                return value;
            }
        }

        return null;
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
