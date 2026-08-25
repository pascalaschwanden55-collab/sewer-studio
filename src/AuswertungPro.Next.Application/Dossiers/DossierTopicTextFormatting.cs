using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Reine Logik fuer formatierte Teile eines Thementextes. Gespeichert werden
/// nur Klartext und kleine Bereiche; Word- oder WPF-Daten gelangen nie ins
/// Dossierformat.
/// </summary>
public static class DossierTopicTextFormatting
{
    public const string StyleRangesSuffix = "__Formatbereiche";
    private const string LiteralStylePrefix = "__Vorlagentext__:";

    /// <summary>
    /// Eindeutiger Formatschluessel fuer eine bearbeitete Beschriftung oder
    /// Ueberschrift der Vorlage. Der Wortlaut bleibt die stabile Identitaet;
    /// eine Seitenposition oder laufende Nummer wird nicht gespeichert.
    /// </summary>
    public static string LiteralStyleKey(string originalText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalText);
        return LiteralStylePrefix + originalText.Trim();
    }

    public sealed record Segment(
        string Text,
        string? ColorHex,
        bool Bold,
        bool Italic,
        bool Underline);

    public sealed record FormattedText(
        string Text,
        IReadOnlyList<DossierTextStyleRange> StyleRanges);

    private sealed record Style(string? ColorHex, bool Bold, bool Italic, bool Underline);

    public static List<DossierTextStyleRange> EffectiveRanges(DossierTopicRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var text = row.Text ?? string.Empty;
        if (row.StyleRanges is { Count: > 0 })
            return Normalize(text, row.StyleRanges);

        return IsColor(row.ColorHex) && text.Length > 0
            ? new List<DossierTextStyleRange>
            {
                new() { Start = 0, Length = text.Length, ColorHex = row.ColorHex.Trim() }
            }
            : new List<DossierTextStyleRange>();
    }

    public static List<DossierTextStyleRange> Normalize(
        string? text,
        IEnumerable<DossierTextStyleRange>? ranges)
    {
        var length = (text ?? string.Empty).Length;
        if (length == 0 || ranges is null)
            return new List<DossierTextStyleRange>();

        var styles = new Style?[length];
        foreach (var range in ranges.Where(r => r is not null))
        {
            if (range.Length <= 0 || !HasStyle(range))
                continue;

            var start = Math.Clamp(range.Start, 0, length);
            var end = (int)Math.Clamp((long)range.Start + range.Length, 0L, length);
            if (end <= start)
                continue;

            var style = new Style(
                IsColor(range.ColorHex) ? range.ColorHex.Trim().ToUpperInvariant() : null,
                range.Bold,
                range.Italic,
                range.Underline);

            for (var i = start; i < end; i++)
                styles[i] = style;
        }

        return BuildRanges(styles);
    }

    public static IReadOnlyList<Segment> Split(
        string? text,
        IEnumerable<DossierTextStyleRange>? ranges)
    {
        var value = text ?? string.Empty;
        if (value.Length == 0)
            return new[] { new Segment(string.Empty, null, false, false, false) };

        var styles = Styles(value, Normalize(value, ranges));
        var result = new List<Segment>();
        var start = 0;

        for (var i = 1; i <= value.Length; i++)
        {
            if (i < value.Length && Equals(styles[i], styles[start]))
                continue;

            var style = styles[start];
            result.Add(new Segment(
                value[start..i],
                style?.ColorHex,
                style?.Bold ?? false,
                style?.Italic ?? false,
                style?.Underline ?? false));
            start = i;
        }

        return result;
    }

    public static FormattedText ReplacePlaceholders(
        string? input,
        IReadOnlyDictionary<string, string> values,
        IEnumerable<DossierTextStyleRange>? ranges)
    {
        ArgumentNullException.ThrowIfNull(values);

        var source = input ?? string.Empty;
        var sourceStyles = Styles(source, Normalize(source, ranges));
        var result = new StringBuilder(source.Length);
        var resultStyles = new List<Style?>();
        var index = 0;

        while (index < source.Length)
        {
            var start = source.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                Append(source[index..], sourceStyles, index, result, resultStyles);
                break;
            }

            Append(source[index..start], sourceStyles, index, result, resultStyles);
            var end = source.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                Append(source[start..], sourceStyles, start, result, resultStyles);
                break;
            }

            var name = source.Substring(start + 2, end - start - 2).Trim();
            if (values.TryGetValue(name, out var replacement))
            {
                var value = replacement ?? string.Empty;
                result.Append(value);
                var markerStyle = start < sourceStyles.Length ? sourceStyles[start] : null;
                for (var i = 0; i < value.Length; i++)
                    resultStyles.Add(markerStyle);
            }

            index = end + 2;
        }

        return new FormattedText(result.ToString(), BuildRanges(resultStyles));
    }

    public static string Encode(IEnumerable<DossierTextStyleRange>? ranges)
        => string.Join(";", (ranges ?? Enumerable.Empty<DossierTextStyleRange>())
            .Where(r => r is not null && r.Length > 0 && HasStyle(r))
            .Select(r => string.Join(",",
                r.Start.ToString(CultureInfo.InvariantCulture),
                r.Length.ToString(CultureInfo.InvariantCulture),
                IsColor(r.ColorHex) ? r.ColorHex.Trim().ToUpperInvariant() : "-",
                r.Bold ? "1" : "0",
                r.Italic ? "1" : "0",
                r.Underline ? "1" : "0")));

    public static List<DossierTextStyleRange> Decode(string? value)
    {
        var result = new List<DossierTextStyleRange>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var item in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(',');
            if (parts.Length != 6
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var length)
                || start < 0
                || length <= 0
                || (parts[2] != "-" && !IsColor(parts[2]))
                || !IstSchalter(parts[3])
                || !IstSchalter(parts[4])
                || !IstSchalter(parts[5]))
            {
                continue;
            }

            result.Add(new DossierTextStyleRange
            {
                Start = start,
                Length = length,
                ColorHex = parts[2] == "-" ? string.Empty : parts[2].ToUpperInvariant(),
                Bold = parts[3] == "1",
                Italic = parts[4] == "1",
                Underline = parts[5] == "1"
            });
        }

        return result;
    }

    private static bool IstSchalter(string value) => value is "0" or "1";

    public static bool IsColor(string? value)
        => value is not null
            && value.Trim().Length == 6
            && value.Trim().All(Uri.IsHexDigit);

    private static bool HasStyle(DossierTextStyleRange range)
        => IsColor(range.ColorHex) || range.Bold || range.Italic || range.Underline;

    private static Style?[] Styles(string text, IEnumerable<DossierTextStyleRange> ranges)
    {
        var result = new Style?[text.Length];
        foreach (var range in ranges)
        {
            var style = new Style(
                IsColor(range.ColorHex) ? range.ColorHex.Trim().ToUpperInvariant() : null,
                range.Bold,
                range.Italic,
                range.Underline);
            var end = Math.Min(text.Length, range.Start + range.Length);
            for (var i = Math.Max(0, range.Start); i < end; i++)
                result[i] = style;
        }

        return result;
    }

    private static List<DossierTextStyleRange> BuildRanges(IReadOnlyList<Style?> styles)
    {
        var result = new List<DossierTextStyleRange>();
        var start = 0;
        while (start < styles.Count)
        {
            var style = styles[start];
            if (style is null)
            {
                start++;
                continue;
            }

            var end = start + 1;
            while (end < styles.Count && Equals(styles[end], style))
                end++;

            result.Add(new DossierTextStyleRange
            {
                Start = start,
                Length = end - start,
                ColorHex = style.ColorHex ?? string.Empty,
                Bold = style.Bold,
                Italic = style.Italic,
                Underline = style.Underline
            });
            start = end;
        }

        return result;
    }

    private static void Append(
        string text,
        IReadOnlyList<Style?> sourceStyles,
        int sourceStart,
        StringBuilder result,
        ICollection<Style?> resultStyles)
    {
        result.Append(text);
        for (var i = 0; i < text.Length; i++)
            resultStyles.Add(sourceStyles[sourceStart + i]);
    }
}
