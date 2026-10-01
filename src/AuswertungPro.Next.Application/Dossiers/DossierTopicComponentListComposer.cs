using System;
using System.Collections.Generic;
using System.Linq;

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
    public const string StyleValueKey = ValueKey + DossierTopicTextFormatting.StyleRangesSuffix;


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

    /// <summary>
    /// Die aktuelle Bauteilliste samt der beim Erzeugen festgelegten
    /// Zustandsfarben. Fehlt die Zusatzangabe bei einem alten Aufrufer, bleibt
    /// die Liste wie bisher unformatiert.
    /// </summary>
    public static DossierTopicTextFormatting.FormattedText ComponentFormattedText(
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var text = ComponentText(values);
        values.TryGetValue(StyleValueKey, out var encodedStyles);
        var styles = DossierTopicTextFormatting.Normalize(
            text,
            DossierTopicTextFormatting.Decode(encodedStyles));

        return new DossierTopicTextFormatting.FormattedText(text, styles);
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

        var componentList = ComponentFormattedText(values);

        // Alte Dossiers koennen noch getrennte Marken fuer Haltungen und
        // Schaechte enthalten. Die erste dieser Marken bestimmt weiterhin die
        // Position und Formatierung, erhaelt jetzt aber die ganze geordnete
        // Liste. Weitere Marken werden geleert, damit nichts doppelt erscheint.
        var source = topic.Text ?? string.Empty;
        var firstMarkerKey = FindFirstComponentMarkerKey(source);
        var replacements = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        foreach (var key in ComponentValueKeys)
            replacements[key] = string.Empty;

        var insertionStart = -1;
        if (firstMarkerKey is not null)
        {
            var markerIndex = source.IndexOf(
                "{{" + firstMarkerKey + "}}",
                StringComparison.OrdinalIgnoreCase);
            // Nur diese erste Marke erhaelt die Liste. Der gleich lange interne
            // Name haelt alle vorhandenen Zeichenbereiche an ihrer Position;
            // weitere gleiche Altmarken werden wie die anderen geleert.
            var uniqueKey = new string('~', firstMarkerKey.Length);
            source = source[..(markerIndex + 2)]
                + uniqueKey
                + source[(markerIndex + 2 + firstMarkerKey.Length)..];
            replacements[uniqueKey] = componentList.Text;
            insertionStart = DossierTopicTextFormatting.ReplacePlaceholders(
                source[..markerIndex],
                replacements,
                Array.Empty<DossierTextStyleRange>()).Text.Length;
        }

        var formatted = DossierTopicTextFormatting.ReplacePlaceholders(
            source,
            replacements,
            ranges);

        var text = formatted.Text.TrimEnd();
        var styles = DossierTopicTextFormatting.Normalize(text, formatted.StyleRanges);
        if (insertionStart >= 0 && componentList.StyleRanges.Count > 0)
        {
            var componentStyles = componentList.StyleRanges.Select(range => new DossierTextStyleRange
            {
                Start = insertionStart + range.Start,
                Length = range.Length,
                ColorHex = range.ColorHex,
                Bold = range.Bold,
                Italic = range.Italic,
                Underline = range.Underline
            });
            styles = DossierTopicTextFormatting.OverlayStyles(
                text,
                styles,
                componentStyles);
        }

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
