using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Ein anklickbarer Baustein fuer einen Ordner- oder Dateinamen.</summary>
public sealed record DistributionPatternBlock(
    string Label,
    string Value,
    string Description,
    bool IsPlaceholder = false);

/// <summary>Ein sichtbar zusammengesetzter Teil des aktuellen Musters.</summary>
public sealed record DistributionPatternPart(
    string Text,
    string RawValue,
    bool IsPlaceholder);

/// <summary>
/// Reine Hilfslogik fuer den grafischen Muster-Editor. Sie veraendert keine Dateien
/// und kennt weder Einstellungen noch Exportdienste.
/// </summary>
public static class DistributionPatternBlockComposer
{
    private static readonly DistributionPatternBlock[] Blocks =
    [
        new("Haltungen", "Haltungen", "Fester Text: Haltungen"),
        new("Schaechte", "Schaechte", "Fester Text: Schaechte"),
        new("Datum", "{Datum}", "Inspektionsdatum, zum Beispiel 20260626", true),
        new("Jahr", "{Jahr}", "Jahr, zum Beispiel 2026", true),
        new("Monat", "{Monat}", "Monat, zum Beispiel 06", true),
        new("_", "_", "Unterstrich als Trennzeichen"),
        new("-", "-", "Bindestrich als Trennzeichen"),
        new("Leer", " ", "Leerzeichen als Trennzeichen")
    ];

    private static readonly DistributionPatternBlock[] DirectoryBlocks =
    [
        new("Gemeinde", "{Gemeinde}", "Projektgemeinde, zum Beispiel Altdorf", true),
        new("Datum", "{Datum}", "Inspektionsdatum, zum Beispiel 20260626", true),
        new("Jahr", "{Jahr}", "Jahr, zum Beispiel 2026", true),
        new("Monat", "{Monat}", "Monat, zum Beispiel 06", true),
        new("_", "_", "Unterstrich als Trennzeichen"),
        new("-", "-", "Bindestrich als Trennzeichen"),
        new("Leer", " ", "Leerzeichen als Trennzeichen")
    ];

    private static readonly (string Value, string Label)[] PlaceholderLabels =
    [
        ("{Datum}", "Datum"),
        ("{Jahr}", "Jahr"),
        ("{Monat}", "Monat"),
        ("{Gemeinde}", "Gemeinde"),
        ("{Haltung}", "Haltung"),
        ("{Schachtnummer}", "Schachtnummer")
    ];

    public static IReadOnlyList<DistributionPatternBlock> AvailableExcelBlocks => Blocks;

    public static IReadOnlyList<DistributionPatternBlock> AvailableDirectoryBlocks => DirectoryBlocks;

    public static string Append(string? pattern, DistributionPatternBlock? block)
        => block is null ? pattern ?? string.Empty : (pattern ?? string.Empty) + block.Value;

    public static string RemoveLast(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return string.Empty;

        var knownValues = Blocks.Concat(DirectoryBlocks).Select(block => block.Value)
            .Concat(PlaceholderLabels.Select(item => item.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => value.Length);

        foreach (var value in knownValues)
        {
            if (pattern.EndsWith(value, StringComparison.OrdinalIgnoreCase))
                return pattern[..^value.Length];
        }

        return pattern[..^1];
    }

    public static IReadOnlyList<DistributionPatternPart> Parse(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return Array.Empty<DistributionPatternPart>();

        var result = new List<DistributionPatternPart>();
        var literalStart = 0;
        var index = 0;

        while (index < pattern.Length)
        {
            var placeholder = PlaceholderLabels.FirstOrDefault(item =>
                pattern.AsSpan(index).StartsWith(item.Value, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(placeholder.Value))
            {
                AddLiteralParts(pattern, literalStart, index, result);
                result.Add(new DistributionPatternPart(
                    placeholder.Label,
                    pattern.Substring(index, placeholder.Value.Length),
                    true));
                index += placeholder.Value.Length;
                literalStart = index;
                continue;
            }

            index++;
        }

        AddLiteralParts(pattern, literalStart, pattern.Length, result);
        return result;
    }

    private static void AddLiteralParts(
        string pattern,
        int start,
        int end,
        ICollection<DistributionPatternPart> result)
    {
        var textStart = start;
        for (var index = start; index < end; index++)
        {
            if (!IsSeparator(pattern[index]))
                continue;

            AddText(pattern, textStart, index, result);
            var separator = pattern[index].ToString();
            result.Add(new DistributionPatternPart(
                pattern[index] == ' ' ? "Leer" : separator,
                separator,
                false));
            textStart = index + 1;
        }

        AddText(pattern, textStart, end, result);
    }

    private static void AddText(
        string pattern,
        int start,
        int end,
        ICollection<DistributionPatternPart> result)
    {
        if (end <= start)
            return;

        var text = pattern[start..end];
        result.Add(new DistributionPatternPart(text, text, false));
    }

    private static bool IsSeparator(char value)
        => value is '_' or '-' or ' ';
}
