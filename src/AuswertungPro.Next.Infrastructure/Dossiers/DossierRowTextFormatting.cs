using System;
using System.Collections.Generic;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Bereinigt formatierten Zelltext, ohne die Zeichenbereiche beim Abschneiden
/// von Leerraum zu verschieben oder zu verlieren.
/// </summary>
internal static class DossierRowTextFormatting
{
    public static IReadOnlyList<DossierTextStyleRange> Styles(
        Dictionary<string, List<DossierTextStyleRange>>? styles,
        string key)
        => styles is not null && styles.TryGetValue(key, out var ranges)
            ? ranges
            : Array.Empty<DossierTextStyleRange>();

    public static void AddValue(
        IDictionary<string, string> row,
        string key,
        string? value,
        IReadOnlyList<DossierTextStyleRange> styles)
    {
        var formatted = Clean(value, styles);
        row[key] = formatted.Text;
        row[key + DossierTopicTextFormatting.StyleRangesSuffix] =
            DossierTopicTextFormatting.Encode(formatted.StyleRanges);
    }

    public static DossierTopicTextFormatting.FormattedText Clean(
        string? value,
        IReadOnlyList<DossierTextStyleRange> styles)
    {
        var original = value ?? string.Empty;
        var start = 0;
        while (start < original.Length && char.IsWhiteSpace(original[start]))
            start++;

        var end = original.Length;
        while (end > start && char.IsWhiteSpace(original[end - 1]))
            end--;

        var text = original[start..end];
        if (text.Length == 0)
        {
            return new DossierTopicTextFormatting.FormattedText(
                string.Empty,
                Array.Empty<DossierTextStyleRange>());
        }

        var ranges = new List<DossierTextStyleRange>();
        foreach (var range in DossierTopicTextFormatting.Normalize(original, styles))
        {
            var overlapStart = Math.Max(start, range.Start);
            var overlapEnd = Math.Min(end, range.Start + range.Length);
            if (overlapEnd <= overlapStart)
                continue;

            ranges.Add(new DossierTextStyleRange
            {
                Start = overlapStart - start,
                Length = overlapEnd - overlapStart,
                ColorHex = range.ColorHex,
                Bold = range.Bold,
                Italic = range.Italic,
                Underline = range.Underline
            });
        }

        return new DossierTopicTextFormatting.FormattedText(text, ranges);
    }
}
