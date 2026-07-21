using System.Globalization;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public static class ProtocolEntryOrdering
{
    public static IReadOnlyList<ProtocolEntry> Order(IEnumerable<ProtocolEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var indexed = entries
            .Select((entry, index) => new
            {
                Entry = entry,
                Index = index
            })
            .ToList();

        var active = indexed
            .Where(item => !item.Entry.IsDeleted)
            .Select(item => new
            {
                item.Entry,
                item.Index,
                MeterStart = GetPrimaryMeter(item.Entry),
                MeterEnd = GetSecondaryMeter(item.Entry)
            })
            .OrderBy(item => item.MeterStart.HasValue ? 0 : 1)
            .ThenBy(item => item.MeterStart ?? double.MaxValue)
            .ThenBy(item => item.MeterEnd.HasValue ? 0 : 1)
            .ThenBy(item => item.MeterEnd ?? double.MaxValue)
            .ThenBy(item => item.Entry.Code ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Index)
            .Select(item => item.Entry);

        var deleted = indexed
            .Where(item => item.Entry.IsDeleted)
            .Select(item => item.Entry);

        return active.Concat(deleted).ToList();
    }

    private static double? GetPrimaryMeter(ProtocolEntry entry)
    {
        var direct = entry.MeterStart ?? entry.MeterEnd;
        if (direct.HasValue)
            return direct;

        if (entry.CodeMeta?.Parameters is null)
            return null;

        foreach (var key in new[] { "vsa.distanz", "Distance" })
        {
            if (!entry.CodeMeta.Parameters.TryGetValue(key, out var raw)
                || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = raw.Trim().Replace(',', '.');
            if (double.TryParse(
                    normalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static double? GetSecondaryMeter(ProtocolEntry entry)
    {
        var direct = entry.MeterEnd ?? entry.MeterStart;
        if (direct.HasValue && !double.IsNaN(direct.Value) && !double.IsInfinity(direct.Value))
            return direct.Value;

        return GetPrimaryMeter(entry);
    }
}
