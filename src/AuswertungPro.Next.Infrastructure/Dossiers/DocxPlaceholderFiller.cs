using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Ersetzt Platzhalter der Form <c>{{Name}}</c> in einem Word-Dokument.
///
/// Der Knackpunkt: Word zerlegt Text in einzelne <see cref="Run"/>-Elemente,
/// sobald sich Formatierung, Rechtschreibpruefung oder Bearbeitungsstand
/// aendern. Ein Platzhalter steht deshalb oft NICHT in einem Stueck da,
/// sondern verteilt als "{{", "Eigen", "tuemer", "}}". Wer je Textstueck
/// ersetzt, findet ihn nicht — die Vorlage sieht dann richtig aus und bleibt
/// trotzdem leer.
///
/// Deshalb wird je Absatz der gesamte Text zusammengesetzt, darauf ersetzt und
/// das Ergebnis in das erste Textstueck geschrieben; die uebrigen werden
/// geleert. Die Formatierung des ersten Textstuecks gilt dann fuer den ganzen
/// Absatz — das ist bei Formularfeldern gewollt und in der mitgelieferten
/// Vorlage entsprechend aufgebaut.
/// </summary>
public static class DocxPlaceholderFiller
{
    /// <summary>Markiert die Vorlagenzeile einer Wiederholtabelle.</summary>
    public const string RepeatMarkerPrefix = "{{#";

    /// <summary>Zusatz, unter dem die Schriftfarbe eines Platzhalters steht.</summary>
    public const string FarbSuffix = "__Farbe";

    /// <summary>
    /// Ersetzt alle Platzhalter in Haupttext, Kopf- und Fusszeilen.
    /// </summary>
    public static void Fill(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(values);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidOperationException("Die Word-Vorlage hat keinen Hauptteil.");

        if (mainPart.Document?.Body is not null)
            FillPart(mainPart.Document.Body, values);

        foreach (var header in mainPart.HeaderParts)
        {
            if (header.Header is not null)
                FillPart(header.Header, values);
        }

        foreach (var footer in mainPart.FooterParts)
        {
            if (footer.Footer is not null)
                FillPart(footer.Footer, values);
        }
    }

    /// <summary>
    /// Vervielfaeltigt die mit <c>{{#Name}}</c> markierte Tabellenzeile: je
    /// Datensatz eine Zeile, mit den Werten des Datensatzes gefuellt. Gibt es
    /// keinen Datensatz, bleibt eine Zeile mit dem Hinweistext stehen, damit im
    /// Dokument keine kopflose Tabelle erscheint.
    /// </summary>
    /// <param name="document">Geoeffnetes Word-Dokument.</param>
    /// <param name="markerName">Name ohne Klammern, z.B. "Haltungen".</param>
    /// <param name="rows">Werte je Zeile.</param>
    /// <param name="emptyText">Text, wenn es keine Datensaetze gibt.</param>
    public static void FillRepeatingRows(
        WordprocessingDocument document,
        string markerName,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string emptyText)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerName);
        ArgumentNullException.ThrowIfNull(rows);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return;

        var marker = RepeatMarkerPrefix + markerName + "}}";
        var templateRow = body
            .Descendants<TableRow>()
            .FirstOrDefault(row => GetRowText(row).Contains(marker, StringComparison.Ordinal));

        if (templateRow is null)
            return;

        // Den Marker selbst nie im Ergebnis stehen lassen.
        RemoveMarker(templateRow, marker);

        if (rows.Count == 0)
        {
            var placeholderRow = (TableRow)templateRow.CloneNode(deep: true);
            ClearAllCellsExceptFirst(placeholderRow, emptyText);
            templateRow.Parent!.InsertAfter(placeholderRow, templateRow);
            templateRow.Remove();
            return;
        }

        OpenXmlElement anchor = templateRow;
        foreach (var row in rows)
        {
            var clone = (TableRow)templateRow.CloneNode(deep: true);
            FillPart(clone, row);
            anchor.Parent!.InsertAfter(clone, anchor);
            anchor = clone;
        }

        templateRow.Remove();
    }

    private static void FillPart(
        OpenXmlElement scope,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var paragraph in scope.Descendants<Paragraph>().ToList())
        {
            // Ein Absatz, der selbst Absaetze enthaelt, ist nur die Huelle um
            // Textfelder — auf dem Deckblatt liegt jede Zeile in einem eigenen
            // Feld. Wuerde die Huelle mitgefuellt, liefe der Text ALLER Felder
            // in ihrem ersten Run zusammen und die uebrigen Felder blieben leer.
            // Gefuellt wird deshalb nur der innerste Absatz.
            if (paragraph.Descendants<Paragraph>().Any())
                continue;

            FillParagraph(paragraph, values);
        }
    }

    private static void FillParagraph(
        Paragraph paragraph,
        IReadOnlyDictionary<string, string> values)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0)
            return;

        var combined = string.Concat(texts.Select(t => t.Text));
        if (!combined.Contains("{{", StringComparison.Ordinal))
            return;

        var replaced = ReplacePlaceholders(combined, values);
        if (string.Equals(replaced, combined, StringComparison.Ordinal))
            return;

        // Die Schriftfarbe wird VOR dem Zurueckschreiben gesucht: danach steht
        // der Platzhaltername nicht mehr im Text.
        var farbe = FarbeFuer(combined, values);

        WriteBack(paragraph, texts, replaced);

        if (farbe is not null)
            SetzeFarbe(paragraph, farbe);
    }

    /// <summary>
    /// Die Schriftfarbe zu einem Platzhalter. Sie steht unter demselben Namen
    /// mit dem Zusatz "__Farbe" — so bleibt der Wertevorrat eine einfache
    /// Zeichenketten-Karte und der Fueller kennt keine Dossierbegriffe.
    /// </summary>
    private static string? FarbeFuer(
        string text, IReadOnlyDictionary<string, string> values)
    {
        foreach (Match treffer in Regex.Matches(text, @"\{\{([A-Za-z0-9_]+)\}\}"))
        {
            var name = treffer.Groups[1].Value + FarbSuffix;

            if (values.TryGetValue(name, out var wert) && IstFarbe(wert))
                return wert.Trim();
        }

        return null;
    }

    /// <summary>
    /// Sechs Hexziffern, sonst nichts. Ein unbrauchbarer Wert laesst die Farbe
    /// der Vorlage stehen, statt Word eine ungueltige Angabe unterzuschieben.
    /// </summary>
    private static bool IstFarbe(string? wert)
        => wert is not null
            && wert.Trim().Length == 6
            && wert.Trim().All(Uri.IsHexDigit);

    private static void SetzeFarbe(Paragraph paragraph, string hex)
    {
        foreach (var run in paragraph.Descendants<Run>())
        {
            run.RunProperties ??= new RunProperties();
            run.RunProperties.Color = new Color { Val = hex };
        }
    }

    /// <summary>
    /// Schreibt den fertigen Text zurueck. Zeilenumbrueche im Wert werden zu
    /// echten Word-Umbruechen, damit mehrzeilige Angaben wie eine Adresse nicht
    /// in einer Zeile zusammenlaufen.
    /// </summary>
    private static void WriteBack(Paragraph paragraph, List<Text> texts, string replaced)
    {
        var first = texts[0];
        for (var i = 1; i < texts.Count; i++)
            texts[i].Text = string.Empty;

        var lines = replaced.Replace("\r\n", "\n").Split('\n');
        first.Text = lines[0];
        first.Space = SpaceProcessingModeValues.Preserve;

        if (lines.Length == 1)
            return;

        var run = first.Ancestors<Run>().FirstOrDefault();
        if (run is null)
        {
            // Ohne umgebenden Run bleibt nur der einzeilige Text; ein stiller
            // Verlust der Folgezeilen waere schlimmer als ein sichtbarer Umbruch.
            first.Text = string.Join(" ", lines);
            return;
        }

        OpenXmlElement anchor = first;
        for (var i = 1; i < lines.Length; i++)
        {
            var br = new Break();
            run.InsertAfter(br, anchor);
            var text = new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve };
            run.InsertAfter(text, br);
            anchor = text;
        }
    }

    /// <summary>
    /// Ersetzt alle <c>{{Name}}</c>. Ein Platzhalter, fuer den kein Wert
    /// vorliegt, wird zu Leertext — im uebergebenen Dokument darf nie eine
    /// geschweifte Klammer stehen bleiben, die der Eigentuemer zu sehen bekommt.
    /// </summary>
    public static string ReplacePlaceholders(
        string input,
        IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        var result = new System.Text.StringBuilder(input.Length);
        var index = 0;

        while (index < input.Length)
        {
            var start = input.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(input, index, input.Length - index);
                break;
            }

            var end = input.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                result.Append(input, index, input.Length - index);
                break;
            }

            result.Append(input, index, start - index);

            var name = input.Substring(start + 2, end - start - 2).Trim();
            if (values.TryGetValue(name, out var value))
                result.Append(value ?? string.Empty);

            index = end + 2;
        }

        return result.ToString();
    }

    private static string GetRowText(TableRow row)
        => string.Concat(row.Descendants<Text>().Select(t => t.Text));

    private static void RemoveMarker(TableRow row, string marker)
    {
        foreach (var paragraph in row.Descendants<Paragraph>().ToList())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count == 0)
                continue;

            var combined = string.Concat(texts.Select(t => t.Text));
            if (!combined.Contains(marker, StringComparison.Ordinal))
                continue;

            var cleaned = combined.Replace(marker, string.Empty);
            for (var i = 1; i < texts.Count; i++)
                texts[i].Text = string.Empty;

            texts[0].Text = cleaned;
            texts[0].Space = SpaceProcessingModeValues.Preserve;
        }
    }

    private static void ClearAllCellsExceptFirst(TableRow row, string firstCellText)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var i = 0; i < cells.Count; i++)
        {
            var texts = cells[i].Descendants<Text>().ToList();
            for (var t = 0; t < texts.Count; t++)
                texts[t].Text = t == 0 && i == 0 ? firstCellText : string.Empty;
        }
    }
}
