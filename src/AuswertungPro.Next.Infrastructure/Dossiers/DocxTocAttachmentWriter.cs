using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Schreibt zusätzliche Inhaltsverzeichnis-Punkte direkt hinter den letzten
/// echten Word-Eintrag. Jeder Punkt wird ein eigener Absatz und übernimmt das
/// Absatz- und Zeichenformat der letzten vorhandenen Verzeichniszeile.
/// </summary>
internal static class DocxTocAttachmentWriter
{
    private const string Placeholder = "{{Verzeichnis_Beilagen}}";

    public static int Apply(
        WordprocessingDocument document,
        IEnumerable<string?>? lines,
        IEnumerable<string?>? pageNumbers,
        int firstNumber)
    {
        ArgumentNullException.ThrowIfNull(document);

        var lineList = lines?.ToList() ?? new List<string?>();
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 0;

        var marker = body.Descendants<Paragraph>()
            .FirstOrDefault(paragraph => paragraph.InnerText.Contains(
                Placeholder,
                StringComparison.Ordinal));

        if (marker is null)
        {
            if (lineList.Any(line => !string.IsNullOrWhiteSpace(line)))
            {
                throw new InvalidDataException(
                    $"Die Word-Vorlage enthält die Stelle '{Placeholder}' nicht.");
            }

            return 0;
        }

        var lastEntry = body.Descendants<Paragraph>()
            .TakeWhile(paragraph => !ReferenceEquals(paragraph, marker))
            .Select(DocxTocEntryReader.Read)
            .Where(entry => entry is not null)
            .Cast<DocxTocEntry>()
            .LastOrDefault();

        var firstPageNumber = int.TryParse(
            lastEntry?.PageNumber,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var lastPageNumber)
            ? lastPageNumber + 1
            : 1;
        var entries = DossierTocAttachments.BuildEntries(
            lineList,
            pageNumbers,
            firstNumber,
            firstPageNumber);

        OpenXmlElement? anchor = lastEntry?.Paragraph;
        var formatTemplate = lastEntry?.Paragraph ?? marker;

        foreach (var entry in entries)
        {
            var paragraph = CreateParagraph(formatTemplate, lastEntry, entry);

            if (anchor is null)
                marker.InsertBeforeSelf(paragraph);
            else
                anchor.InsertAfterSelf(paragraph);

            anchor = paragraph;
        }

        marker.Remove();
        return entries.Count;
    }

    private static Paragraph CreateParagraph(
        Paragraph paragraphTemplate,
        DocxTocEntry? lastEntry,
        DossierTocAttachmentEntry entry)
    {
        var paragraph = new Paragraph();
        if (paragraphTemplate.ParagraphProperties is not null)
        {
            paragraph.Append(
                paragraphTemplate.ParagraphProperties.CloneNode(deep: true));
        }

        var numberProperties = lastEntry?.Paragraph
            .Descendants<Run>()
            .FirstOrDefault(run => run.Descendants<Text>().Any())
            ?.RunProperties;
        var tabProperties = lastEntry?.Paragraph
            .Descendants<Run>()
            .LastOrDefault(run => run.Descendants<TabChar>().Any())
            ?.RunProperties;
        var titleProperties = lastEntry?.TitleTexts
            .FirstOrDefault()?
            .Ancestors<Run>()
            .FirstOrDefault()?
            .RunProperties;
        var pageProperties = lastEntry?.Paragraph
            .Descendants<Run>()
            .LastOrDefault(run => run.Descendants<Text>().Any())
            ?.RunProperties;

        paragraph.Append(
            CreateRun(
                numberProperties,
                new Text($"{entry.Number}.") { Space = SpaceProcessingModeValues.Preserve }),
            CreateRun(tabProperties, new TabChar()),
            CreateRun(
                titleProperties,
                new Text(entry.Title) { Space = SpaceProcessingModeValues.Preserve }));

        if (!string.IsNullOrWhiteSpace(entry.PageNumber))
        {
            paragraph.Append(
                CreateRun(tabProperties, new TabChar()),
                CreateRun(
                    pageProperties,
                    new Text(entry.PageNumber) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return paragraph;
    }

    private static Run CreateRun(RunProperties? properties, OpenXmlElement content)
    {
        var run = new Run();
        if (properties is not null)
            run.Append(properties.CloneNode(deep: true));

        run.Append(content);
        return run;
    }
}
