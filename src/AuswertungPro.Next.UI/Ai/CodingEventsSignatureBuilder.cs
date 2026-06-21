using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEventsSignatureBuilder
{
    public static string Build(IEnumerable<CodingEvent> events)
        => string.Join("\n", events
            .OrderBy(e => e.Entry.EntryId)
            .ThenBy(e => e.MeterAtCapture)
            .Select(BuildEventSignature));

    public static string BuildEventSignature(CodingEvent codingEvent)
    {
        var entry = codingEvent.Entry;
        var parameters = entry.CodeMeta?.Parameters is null
            ? string.Empty
            : string.Join(";", entry.CodeMeta.Parameters
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => $"{p.Key}={p.Value}"));

        return string.Join("|", new[]
        {
            entry.EntryId.ToString("N"),
            entry.Code ?? string.Empty,
            entry.Beschreibung ?? string.Empty,
            FormatNullable(entry.MeterStart),
            FormatNullable(entry.MeterEnd),
            entry.IsStreckenschaden ? "1" : "0",
            entry.Mpeg ?? string.Empty,
            entry.Zeit?.ToString() ?? string.Empty,
            entry.Source.ToString(),
            entry.IsDeleted ? "1" : "0",
            parameters,
            FormatNullable(codingEvent.MeterAtCapture),
            codingEvent.VideoTimestamp.ToString()
        });
    }

    private static string FormatNullable(double? value)
        => value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;
}
