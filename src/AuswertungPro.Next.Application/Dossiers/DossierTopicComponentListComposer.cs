using System;
using System.Collections.Generic;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Fuegt die zum Dossier gehoerenden Bauteile in die beiden fachlich passenden
/// Themen ein. Die Reihenfolge und Nummerierung kommen aus einem gemeinsamen
/// Wert, damit Vorschau und Word nie auseinanderlaufen.
/// </summary>
public static class DossierTopicComponentListComposer
{
    public const string ValueKey = "Bauteile_Text";
    public const string Placeholder = "{{Bauteile_Text}}";


    private static readonly string[] ComponentValueKeys =
    {
        ValueKey,
        "Haltungen_Text",
        "Schaechte_Text"
    };

    public static bool IsAutomaticTitle(string? title)
        => DossierTopicTitles.Matches(DossierTopicTitles.WithAutomaticComponents, title);

    public static DossierTopicTextFormatting.FormattedText Compose(
        DossierTopicRow topic,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(values);

        var ranges = DossierTopicTextFormatting.EffectiveRanges(topic);
        if (!IsAutomaticTitle(topic.Title))
            return DossierTopicTextFormatting.ReplacePlaceholders(topic.Text, values, ranges);

        values.TryGetValue(ValueKey, out var componentList);
        componentList ??= string.Empty;

        // Alte Dossiers koennen noch getrennte Marken fuer Haltungen und
        // Schaechte enthalten. Die erste dieser Marken bestimmt weiterhin die
        // Position und Formatierung, erhaelt jetzt aber die ganze geordnete
        // Liste. Weitere Marken werden geleert, damit nichts doppelt erscheint.
        var firstMarkerKey = FindFirstComponentMarkerKey(topic.Text);
        var replacements = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        foreach (var key in ComponentValueKeys)
            replacements[key] = string.Empty;

        if (firstMarkerKey is not null)
            replacements[firstMarkerKey] = componentList;

        var formatted = DossierTopicTextFormatting.ReplacePlaceholders(
            topic.Text,
            replacements,
            ranges);

        var text = formatted.Text.TrimEnd();
        var styles = DossierTopicTextFormatting.Normalize(text, formatted.StyleRanges);

        if (firstMarkerKey is not null || componentList.Length == 0)
            return new DossierTopicTextFormatting.FormattedText(text, styles);

        var separator = text.Length == 0 ? string.Empty : "\n";
        return new DossierTopicTextFormatting.FormattedText(
            text + separator + componentList,
            styles);
    }

    private static string? FindFirstComponentMarkerKey(string? text)
    {
        var source = text ?? string.Empty;
        string? result = null;
        var firstIndex = int.MaxValue;

        foreach (var key in ComponentValueKeys)
        {
            var index = source.IndexOf("{{" + key + "}}", StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index < firstIndex)
            {
                firstIndex = index;
                result = key;
            }
        }

        return result;
    }
}
