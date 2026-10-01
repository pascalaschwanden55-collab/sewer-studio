using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Setzt unsichtbare Word-Textmarken um die fuellbaren Stellen des erzeugten
/// Dossiers. Beim Umwandeln werden daraus benannte Ziele der PDF, mit
/// Seitennummer und exakter Position.
///
/// Damit erkennt die Vorschau ein Feld an seiner Marke statt an seinem Text.
/// Der bisherige Weg ueber den Text bleibt als Rueckfall bestehen; er scheitert
/// aber, sobald mehrere Felder denselben Text tragen (etwa dreizehnmal
/// „unbekannt") oder eine Zelle leer ist.
/// </summary>
internal static class DocxFieldMarkerWriter
{
    private static readonly Regex PlaceholderPattern = new(
        @"\{\{([A-Za-z0-9_]{1,60})\}\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Die Spaltenschluessel einer Wiederholzeile, in Spaltenreihenfolge.
    /// Leer, wo eine Zelle keinen Platzhalter traegt.
    /// </summary>
    internal static IReadOnlyList<string> CellKeys(TableRow templateRow)
    {
        ArgumentNullException.ThrowIfNull(templateRow);

        return templateRow
            .Elements<TableCell>()
            .Select(cell => PlaceholderPattern.Match(cell.InnerText) is { Success: true } treffer
                ? treffer.Groups[1].Value
                : string.Empty)
            .ToList();
    }

    /// <summary>
    /// Die naechste freie Marken-Nummer des Dokuments. Doppelte Nummern machen
    /// die Datei fuer Word ungueltig, deshalb wird der Bestand einmal gelesen.
    /// </summary>
    internal static int NextId(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return 1;

        var hoechste = 0;
        foreach (var marke in body.Descendants<BookmarkStart>())
        {
            if (int.TryParse(marke.Id?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && id > hoechste)
            {
                hoechste = id;
            }
        }

        return hoechste + 1;
    }

    /// <summary>
    /// Legt um jede Zelle der Zeile ihre Marke. Zellen ohne Spaltenschluessel
    /// bleiben unberuehrt - eine erfundene Adresse waere schlimmer als keine.
    /// </summary>
    internal static int MarkRow(
        TableRow row,
        string repeatKey,
        int rowIndex,
        IReadOnlyList<string> cellKeys,
        int nextId)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(repeatKey);
        ArgumentNullException.ThrowIfNull(cellKeys);

        var cells = row.Elements<TableCell>().ToList();
        for (var spalte = 0; spalte < cells.Count && spalte < cellKeys.Count; spalte++)
        {
            var cellKey = cellKeys[spalte];
            if (string.IsNullOrEmpty(cellKey))
                continue;

            var name = DossierPdfFieldMarker.Name(
                DossierPreviewTarget.RowCell(repeatKey, rowIndex, cellKey));
            if (MarkCell(cells[spalte], name, nextId))
                nextId++;
        }

        return nextId;
    }

    /// <summary>
    /// Setzt Anfang und Ende der Marke in die Zelle. Der Anfang gehoert hinter
    /// die Absatzeigenschaften: <c>w:pPr</c> muss das erste Kind eines Absatzes
    /// bleiben, sonst liest Word den Absatz nicht mehr.
    /// </summary>
    private static bool MarkCell(TableCell cell, string name, int id)
    {
        var absaetze = cell.Elements<Paragraph>().ToList();
        if (absaetze.Count == 0)
            return false;

        var nummer = id.ToString(CultureInfo.InvariantCulture);
        var erster = absaetze[0];
        var start = new BookmarkStart { Id = nummer, Name = name };

        if (erster.ParagraphProperties is { } eigenschaften)
            erster.InsertAfter(start, eigenschaften);
        else
            erster.InsertAt(start, 0);

        absaetze[^1].Append(new BookmarkEnd { Id = nummer });
        return true;
    }
}
