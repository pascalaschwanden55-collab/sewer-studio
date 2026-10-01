using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>Eine strukturell gelesene Zeile des echten Word-Verzeichnisses.</summary>
internal sealed record DocxTocEntry(
    Paragraph Paragraph,
    string Number,
    string Title,
    string PageNumber,
    List<Text> TitleTexts);

/// <summary>
/// Liest Nummer, Titel und Seitenzahl getrennt aus einer Word-Verzeichniszeile.
/// Die Seitenzahl wird am PAGEREF-Feld erkannt und nicht anhand ihrer Ziffern.
/// Damit bleibt auch ein Titel mit einer Zahl am Ende eindeutig.
/// </summary>
internal static class DocxTocEntryReader
{
    public static DocxTocEntry? Read(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (!DossierTocStyle.IsEntry(style))
            return null;

        var tokens = paragraph.Descendants()
            .Where(element => element is Text or TabChar or FieldChar or FieldCode)
            .ToList();

        var pageFieldStart = tokens.FindIndex(IsPageReferenceStart);
        if (pageFieldStart < 0)
            return null;

        var tabs = tokens
            .Take(pageFieldStart)
            .Select((element, index) => (element, index))
            .Where(item => item.element is TabChar)
            .Select(item => item.index)
            .ToList();

        if (tabs.Count < 2)
            return null;

        var numberEnd = tabs[^2];
        var titleEnd = tabs[^1];
        var titleTexts = tokens
            .Skip(numberEnd + 1)
            .Take(titleEnd - numberEnd - 1)
            .OfType<Text>()
            .ToList();

        var title = string.Concat(titleTexts.Select(text => text.Text)).Trim();
        if (title.Length == 0)
            return null;

        var number = string.Concat(tokens
                .Take(numberEnd)
                .OfType<Text>()
                .Select(text => text.Text))
            .Trim();

        var pageNumber = LiesFeldergebnis(tokens, pageFieldStart);
        return new DocxTocEntry(paragraph, number, title, pageNumber, titleTexts);

        bool IsPageReferenceStart(OpenXmlElement element)
        {
            if (element is not FieldChar field
                || field.FieldCharType?.Value != FieldCharValues.Begin)
            {
                return false;
            }

            var index = tokens.IndexOf(element);
            for (var i = index + 1; i < tokens.Count; i++)
            {
                if (tokens[i] is FieldChar)
                    return false;

                if (tokens[i] is FieldCode code
                    && code.Text.Contains("PAGEREF", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static string LiesFeldergebnis(IReadOnlyList<OpenXmlElement> tokens, int start)
    {
        var nachTrennung = false;
        var text = new List<string>();

        for (var i = start + 1; i < tokens.Count; i++)
        {
            if (tokens[i] is FieldChar field)
            {
                if (field.FieldCharType?.Value == FieldCharValues.Separate)
                {
                    nachTrennung = true;
                    continue;
                }

                if (field.FieldCharType?.Value == FieldCharValues.End)
                    break;
            }

            if (nachTrennung && tokens[i] is Text part)
                text.Add(part.Text);
        }

        return string.Concat(text).Trim();
    }
}

/// <summary>
/// Schreibt eine eigene Fassung in den Titel einer Verzeichniszeile. Nummer,
/// Tabulatoren und PAGEREF-Seitenzahl bleiben dabei unangetastet. Die passende
/// Kapitelüberschrift wird danach vom normalen Vorlagentext-Ersetzer geändert;
/// deshalb bleibt der Titel auch nach einer Aktualisierung in Word erhalten.
/// </summary>
public static class DocxTocEntryEditor
{
    public static int Apply(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, List<DossierTextStyleRange>>? fieldStyles = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (overrides is null || overrides.Count == 0)
            return 0;

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        var entries = body.Descendants<Paragraph>()
            .Select(DocxTocEntryReader.Read)
            .Where(entry => entry is not null)
            .Cast<DocxTocEntry>()
            .ToList();
        var changed = 0;

        foreach (var entry in entries)
        {
            if (!overrides.TryGetValue(entry.Title, out var replacement))
                continue;

            replacement ??= string.Empty;
            var styleKey = DossierTopicTextFormatting.LiteralStyleKey(entry.Title);
            var ranges = fieldStyles is not null
                && fieldStyles.TryGetValue(styleKey, out var stored)
                    ? DossierTopicTextFormatting.Normalize(replacement, stored)
                    : new List<DossierTextStyleRange>();

            if (ranges.Count > 0)
            {
                DocxPlaceholderFiller.WriteBackFormatted(
                    entry.Paragraph, entry.TitleTexts, replacement, ranges);
            }
            else
            {
                entry.TitleTexts[0].Text = replacement;
                entry.TitleTexts[0].Space = SpaceProcessingModeValues.Preserve;
                foreach (var text in entry.TitleTexts.Skip(1))
                    text.Text = string.Empty;
            }

            changed++;
        }

        return changed;
    }
}
