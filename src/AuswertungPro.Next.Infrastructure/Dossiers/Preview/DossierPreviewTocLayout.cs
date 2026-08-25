using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Preview;

/// <summary>Überträgt die kompakte Word-Verzeichnisform in das Vorschaumodell.</summary>
internal static class DossierPreviewTocLayout
{
    public static void Apply(List<DossierPreviewBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var firstEntry = blocks.FindIndex(block =>
            block is DossierPreviewParagraph paragraph
            && paragraph.TocEntry is not null);
        if (firstEntry < 0)
            return;

        for (var index = firstEntry; index < blocks.Count; index++)
        {
            if (blocks[index] is not DossierPreviewParagraph paragraph)
                continue;

            var isAttachment = paragraph.Runs.Any(run => string.Equals(
                run.FieldKey,
                "Verzeichnis_Beilagen",
                StringComparison.OrdinalIgnoreCase));
            if (paragraph.TocEntry is null && !isAttachment)
                continue;

            blocks[index] = FormatEntry(paragraph);
        }

        var headingIndex = firstEntry > 0
            ? blocks.FindLastIndex(
                firstEntry - 1,
                block => block is DossierPreviewParagraph paragraph
                    && paragraph.Runs.Any(run =>
                        !run.IsField && !string.IsNullOrWhiteSpace(run.Text)))
            : -1;
        if (headingIndex >= 0
            && blocks[headingIndex] is DossierPreviewParagraph heading)
        {
            blocks[headingIndex] = FormatHeading(heading);
        }
    }

    private static DossierPreviewParagraph FormatEntry(DossierPreviewParagraph paragraph)
        => paragraph with
        {
            Format = paragraph.Format with
            {
                SpaceBeforePx = DossierTocLayoutPolicy.EntrySpaceBeforePx,
                SpaceAfterPx = 0
            },
            Runs = paragraph.Runs
                .Select(run => run with
                {
                    Format = FormatRun(
                        run.Format,
                        DossierTocLayoutPolicy.EntryFontPx,
                        bold: false)
                })
                .ToList()
        };

    private static DossierPreviewParagraph FormatHeading(DossierPreviewParagraph heading)
        => heading with
        {
            Format = heading.Format with
            {
                SpaceAfterPx = DossierTocLayoutPolicy.HeadingSpaceAfterPx
            },
            Runs = heading.Runs
                .Select(run => run with
                {
                    Format = FormatRun(
                        run.Format,
                        DossierTocLayoutPolicy.HeadingFontPx,
                        bold: true)
                })
                .ToList()
        };

    private static DossierPreviewRunFormat FormatRun(
        DossierPreviewRunFormat format,
        double fontSizePx,
        bool bold)
        => format with
        {
            FontFamily = DossierTocLayoutPolicy.FontFamily,
            FontSizePx = fontSizePx,
            Bold = bold,
            Italic = false,
            Underline = false,
            ColorHex = DossierTocLayoutPolicy.ColorHex
        };
}
