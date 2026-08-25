using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

public sealed record DossierTocAttachmentEntry(
    int Number,
    string Title,
    string PageNumber,
    IReadOnlyList<DossierTextStyleRange> TitleStyles);

public sealed record DossierTocAttachmentStart(
    int FirstNumber,
    int FirstPageNumber);

/// <summary>
/// Die zusaetzlichen Zeilen des Inhaltsverzeichnisses.
///
/// Die drei Kapitel bleiben Word ueberlassen: es rechnet sie aus den
/// Ueberschriften und kennt ihre Seitenzahlen. Was am Schluss dazukommt —
/// TV-Protokolle, Schachtprotokolle, Plaene — steht dagegen gar nicht im
/// Word-Dokument, sondern liegt als eigene Datei daneben. Word kann diese
/// Zeilen also weder finden noch automatisch mit einer Seitenzahl versehen.
/// SewerStudio schlägt deshalb die nächste Seite vor; der Mensch kann sie je
/// Punkt ändern oder bewusst leeren.
/// </summary>
public static class DossierTocAttachments
{
    /// <summary>
    /// Eine vorangestellte Nummer, die der Mensch aus Gewohnheit mitgetippt
    /// hat: "4. TV-Protokolle" oder "5.Plaene". Sie wird entfernt, damit nicht
    /// "4.\t4. TV-Protokolle" im Dossier steht.
    ///
    /// Ein Punkt ist Pflicht — "3 Plaene" ist eine Menge und keine Nummer.
    /// </summary>
    private static readonly Regex FuehrendeNummer = new(
        @"^\d{1,2}\.\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Die Zeilen als Textblock, fortlaufend ab <paramref name="firstNumber"/>.
    /// Leere Zeilen fallen weg und zaehlen nicht mit, sonst entstuenden
    /// Luecken in der Nummerierung.
    /// </summary>
    public static string Build(
        IEnumerable<DossierTocAttachment?>? attachments,
        int firstNumber,
        int? firstPageNumber = null)
        => Format(BuildEntries(attachments, firstNumber, firstPageNumber));

    /// <summary>
    /// Die normalisierten Einträge. Vorschau und Word-Ausgabe verwenden damit
    /// dieselben Nummern und denselben Text, auch wenn eine Nummer mitgetippt
    /// oder eine leere Zeile erfasst wurde.
    /// </summary>
    public static IReadOnlyList<DossierTocAttachmentEntry> BuildEntries(
        IEnumerable<DossierTocAttachment?>? attachments,
        int firstNumber,
        int? firstPageNumber)
    {
        if (attachments is null)
            return Array.Empty<DossierTocAttachmentEntry>();

        var nummer = firstNumber;
        var vorgeschlageneSeite = firstPageNumber;
        var entries = new List<DossierTocAttachmentEntry>();

        foreach (var attachment in attachments)
        {
            if (attachment is null)
                continue;

            var rawTitle = attachment.Title ?? string.Empty;
            var outerTrimmed = rawTitle.Trim();
            var prefix = FuehrendeNummer.Match(outerTrimmed);
            var afterPrefix = outerTrimmed[prefix.Length..];
            var text = afterPrefix.Trim();
            if (text.Length == 0)
                continue;

            // Die Formatbereiche beziehen sich auf den Text im Editor. Beim
            // Export fallen Randabstand und eine versehentlich mitgetippte
            // Nummer weg; die Bereiche muessen um genau denselben Anteil
            // verschoben werden.
            var outerStart = rawTitle.Length - rawTitle.TrimStart().Length;
            var innerStart = afterPrefix.Length - afterPrefix.TrimStart().Length;
            var sourceStart = outerStart + prefix.Length + innerStart;
            var titleStyles = ShiftTitleStyles(
                rawTitle,
                attachment.TitleStyles,
                sourceStart,
                text.Length);

            var seite = attachment.PageNumber is null
                ? vorgeschlageneSeite?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                : attachment.PageNumber.Trim();

            entries.Add(new DossierTocAttachmentEntry(
                nummer,
                text,
                seite,
                titleStyles));
            nummer++;

            if (int.TryParse(
                    seite,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var verwendeteSeite) &&
                verwendeteSeite >= 0)
                vorgeschlageneSeite = verwendeteSeite + 1;
        }

        return entries;
    }

    private static IReadOnlyList<DossierTextStyleRange> ShiftTitleStyles(
        string source,
        IReadOnlyList<DossierTextStyleRange>? styles,
        int sourceStart,
        int targetLength)
    {
        var sourceEnd = sourceStart + targetLength;
        var result = new List<DossierTextStyleRange>();

        foreach (var range in DossierTopicTextFormatting.Normalize(source, styles))
        {
            var overlapStart = Math.Max(sourceStart, range.Start);
            var overlapEnd = Math.Min(sourceEnd, range.Start + range.Length);
            if (overlapEnd <= overlapStart)
                continue;

            result.Add(new DossierTextStyleRange
            {
                Start = overlapStart - sourceStart,
                Length = overlapEnd - overlapStart,
                ColorHex = range.ColorHex,
                Bold = range.Bold,
                Italic = range.Italic,
                Underline = range.Underline
            });
        }

        return result;
    }

    /// <summary>
    /// Ermittelt für die Vorschau dieselbe Anfangsnummer, die Word nach dem
    /// Entfernen ausgeblendeter Kapitel verwendet. Die nächste Seite folgt
    /// der höchsten sichtbaren Word-Seitenzahl.
    /// </summary>
    public static DossierTocAttachmentStart StartAfter(
        IEnumerable<DossierPreviewTocEntry?>? entries,
        IEnumerable<string?>? hiddenChapters)
    {
        var hidden = new HashSet<string>(
            (hiddenChapters ?? Array.Empty<string?>())
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .Select(title => title!.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var visible = (entries ?? Array.Empty<DossierPreviewTocEntry?>())
            .Where(entry => entry is not null
                && !hidden.Contains((entry.Title ?? string.Empty).Trim()))
            .Select(entry => entry!)
            .ToList();
        var lastPage = visible
            .Select(entry => int.TryParse(
                entry.PageNumber,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var page)
                    ? page
                    : 0)
            .DefaultIfEmpty(0)
            .Max();

        return new DossierTocAttachmentStart(visible.Count + 1, lastPage + 1);
    }

    private static string Format(IEnumerable<DossierTocAttachmentEntry> entries)
        => string.Join("\n", entries.Select(entry =>
            string.IsNullOrWhiteSpace(entry.PageNumber)
                ? $"{entry.Number}.\t{entry.Title}"
                : $"{entry.Number}.\t{entry.Title}\t{entry.PageNumber}"));
}
