using System;
using System.Collections.Generic;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Gibt dem Inhaltsverzeichnis eine ruhige, gut lesbare Form. Die Vorlage
/// sperrt jedes Zeichen um einen Punkt und setzt vor jede Zeile 18 Punkt Luft;
/// dadurch wirkt das Verzeichnis auseinandergezogen. Nummern, Word-Felder,
/// Punktlinie und Seitenzahlen bleiben unverändert.
/// </summary>
internal static class DocxTocLayoutFormatter
{
    public static int Apply(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        var paragraphs = body.Elements<Paragraph>().ToList();
        var entries = paragraphs
            .Where(paragraph => DossierTocStyle.IsEntry(
                paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value)
                && !string.IsNullOrWhiteSpace(paragraph.InnerText))
            .ToList();
        if (entries.Count == 0)
            return 0;

        foreach (var entry in entries)
        {
            entry.ParagraphProperties ??= new ParagraphProperties();
            entry.ParagraphProperties.SpacingBetweenLines = new SpacingBetweenLines
            {
                Before = DossierTocLayoutPolicy.EntrySpaceBeforeTwips,
                After = "0"
            };

            foreach (var run in entry.Descendants<Run>())
                FormatRun(
                    run,
                    halfPoints: DossierTocLayoutPolicy.EntryFontHalfPoints,
                    bold: false);
        }

        var firstIndex = paragraphs.IndexOf(entries[0]);
        var heading = paragraphs
            .Take(firstIndex)
            .LastOrDefault(paragraph => !string.IsNullOrWhiteSpace(paragraph.InnerText));

        if (heading is not null)
        {
            heading.ParagraphProperties ??= new ParagraphProperties();
            var spacing = heading.ParagraphProperties.SpacingBetweenLines
                ?? new SpacingBetweenLines();
            spacing.After = DossierTocLayoutPolicy.HeadingSpaceAfterTwips;
            heading.ParagraphProperties.SpacingBetweenLines = spacing;

            foreach (var run in heading.Descendants<Run>())
                FormatRun(
                    run,
                    halfPoints: DossierTocLayoutPolicy.HeadingFontHalfPoints,
                    bold: true);
        }

        return entries.Count;
    }

    private static void FormatRun(Run run, string halfPoints, bool bold)
    {
        run.RunProperties ??= new RunProperties();
        var properties = run.RunProperties;
        properties.RunFonts = new RunFonts
        {
            Ascii = DossierTocLayoutPolicy.FontFamily,
            HighAnsi = DossierTocLayoutPolicy.FontFamily,
            EastAsia = DossierTocLayoutPolicy.FontFamily,
            ComplexScript = DossierTocLayoutPolicy.FontFamily
        };
        properties.Bold = new Bold { Val = bold };
        properties.Italic = new Italic { Val = false };
        properties.Underline = new Underline { Val = UnderlineValues.None };
        properties.Color = new Color { Val = DossierTocLayoutPolicy.ColorHex };
        properties.Spacing = new Spacing { Val = 0 };
        properties.FontSize = new FontSize { Val = halfPoints };
        properties.FontSizeComplexScript = new FontSizeComplexScript { Val = halfPoints };
    }
}
