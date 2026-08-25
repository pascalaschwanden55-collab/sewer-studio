using System;
using System.Collections.Generic;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Loest alte Bauteilmarken in Themen auf. Neue Dossiers erhalten die Liste
/// nur noch durch den ausdruecklichen Import im Editor. Danach liegt sie als
/// normaler, frei bearbeitbarer Text im Dossier.
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

    public static bool IsComponentImportTitle(string? title)
        => DossierTopicTitles.Matches(DossierTopicTitles.WithComponentImport, title);

    public static string ComponentText(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.TryGetValue(ValueKey, out var text) ? text ?? string.Empty : string.Empty;
    }

    public static DossierTopicTextFormatting.FormattedText Compose(
        DossierTopicRow topic,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(values);

        var ranges = DossierTopicTextFormatting.EffectiveRanges(topic);
        if (!IsComponentImportTitle(topic.Title))
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

        return new DossierTopicTextFormatting.FormattedText(text, styles);
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
