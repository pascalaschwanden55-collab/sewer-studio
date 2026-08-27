using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

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
        => Fill(document, values, DocxLiteralFormatting.Empty);

    /// <summary>
    /// Fuellt die Platzhalter und uebernimmt dabei die zuvor erfassten
    /// Zeichenformate fester Beschriftungen im selben Absatz.
    /// </summary>
    internal static void Fill(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string> values,
        DocxLiteralFormatting literalFormatting)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(literalFormatting);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidOperationException("Die Word-Vorlage hat keinen Hauptteil.");

        if (mainPart.Document?.Body is not null)
            FillPart(mainPart.Document.Body, values, literalFormatting);

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

        // Die Spaltenschluessel stammen aus der Vorlagenzeile - VOR dem Fuellen,
        // danach stehen dort die Werte. Jede erzeugte Zelle bekommt daraus ihre
        // unsichtbare Marke; sie wird beim Umwandeln zum benannten PDF-Ziel und
        // macht die Vorschau-Zuordnung exakt statt vom Text abhaengig.
        var cellKeys = DocxFieldMarkerWriter.CellKeys(templateRow);
        var markerId = DocxFieldMarkerWriter.NextId(document);

        OpenXmlElement anchor = templateRow;
        var rowIndex = 0;
        foreach (var row in rows)
        {
            var clone = (TableRow)templateRow.CloneNode(deep: true);
            FillPart(clone, row);
            markerId = DocxFieldMarkerWriter.MarkRow(
                clone, markerName, rowIndex, cellKeys, markerId);
            anchor.Parent!.InsertAfter(clone, anchor);
            anchor = clone;
            rowIndex++;
        }

        templateRow.Remove();
    }

    private static void FillPart(
        OpenXmlElement scope,
        IReadOnlyDictionary<string, string> values,
        DocxLiteralFormatting? literalFormatting = null)
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

            FillParagraph(paragraph, values, literalFormatting);
        }
    }

    private static void FillParagraph(
        Paragraph paragraph,
        IReadOnlyDictionary<string, string> values,
        DocxLiteralFormatting? literalFormatting)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        if (texts.Count == 0)
            return;

        var combined = string.Concat(texts.Select(t => t.Text));
        if (!combined.Contains("{{", StringComparison.Ordinal))
            return;

        IReadOnlyList<DocxLiteralRange>? literalRanges = null;
        literalFormatting?.LiteralRanges.TryGetValue(paragraph, out literalRanges);
        var replaced = ReplacePlaceholders(combined, values, literalRanges);
        if (string.Equals(replaced, combined, StringComparison.Ordinal))
            return;

        // Die Schriftfarbe wird VOR dem Zurueckschreiben gesucht: danach steht
        // der Platzhaltername nicht mehr im Text.
        var farbe = FarbeFuer(combined, values, literalRanges);

        IReadOnlyList<DossierTextStyleRange>? festeFormate = null;
        literalFormatting?.Paragraphs.TryGetValue(paragraph, out festeFormate);
        var formatbereiche = FormatFuer(
            combined,
            replaced,
            values,
            festeFormate,
            literalRanges);
        if (formatbereiche.Count > 0)
            WriteBackFormatted(paragraph, texts, replaced, formatbereiche);
        else
            WriteBack(paragraph, texts, replaced);

        if (formatbereiche.Count == 0 && farbe is not null)
            SetzeFarbe(paragraph, farbe);
    }

    /// <summary>
    /// Die Schriftfarbe zu einem Platzhalter. Sie steht unter demselben Namen
    /// mit dem Zusatz "__Farbe" — so bleibt der Wertevorrat eine einfache
    /// Zeichenketten-Karte und der Fueller kennt keine Dossierbegriffe.
    /// </summary>
    private static string? FarbeFuer(
        string text,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<DocxLiteralRange>? literalRanges = null)
    {
        foreach (var treffer in PlaceholderMatches(text, literalRanges))
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

    private static List<DossierTextStyleRange> FormatFuer(
        string source,
        string replaced,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<DossierTextStyleRange>? festeFormate = null,
        IReadOnlyList<DocxLiteralRange>? literalRanges = null)
    {
        var ranges = new List<DossierTextStyleRange>();
        var sourcePosition = 0;
        var outputPosition = 0;

        foreach (var match in PlaceholderMatches(source, literalRanges))
        {
            // Der feste Text zwischen zwei Platzhaltern bleibt unformatiert.
            outputPosition += match.Index - sourcePosition;

            var name = match.Groups[1].Value;
            var value = values.TryGetValue(name, out var storedValue)
                ? storedValue ?? string.Empty
                : string.Empty;
            var formatKey = name + DossierTopicTextFormatting.StyleRangesSuffix;

            if (values.TryGetValue(formatKey, out var encoded))
            {
                ranges.AddRange(DossierTopicTextFormatting
                    .Normalize(value, DossierTopicTextFormatting.Decode(encoded))
                    .Select(range => new DossierTextStyleRange
                    {
                        Start = outputPosition + range.Start,
                        Length = range.Length,
                        ColorHex = range.ColorHex,
                        Bold = range.Bold,
                        Italic = range.Italic,
                        Underline = range.Underline
                    }));
            }

            outputPosition += value.Length;
            sourcePosition = match.Index + match.Length;
        }

        if (festeFormate is { Count: > 0 })
        {
            ranges.AddRange(VerschiebeFesteFormate(
                source,
                values,
                festeFormate,
                literalRanges));
        }

        return DossierTopicTextFormatting.Normalize(replaced, ranges);
    }

    /// <summary>
    /// Verschiebt Zeichenbereiche des festen Textes um die Laengen der
    /// eingesetzten Platzhalterwerte. Bereiche innerhalb eines Platzhalters
    /// werden bewusst nicht uebernommen; dafuer gelten dessen eigene Formate.
    /// </summary>
    private static IReadOnlyList<DossierTextStyleRange> VerschiebeFesteFormate(
        string source,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<DossierTextStyleRange> sourceRanges,
        IReadOnlyList<DocxLiteralRange>? literalRanges)
    {
        var result = new List<DossierTextStyleRange>();
        var sourcePosition = 0;
        var outputPosition = 0;

        foreach (var match in PlaceholderMatches(source, literalRanges))
        {
            Uebertrage(sourcePosition, match.Index, outputPosition);
            outputPosition += match.Index - sourcePosition;

            var name = match.Groups[1].Value;
            if (values.TryGetValue(name, out var value))
                outputPosition += (value ?? string.Empty).Length;

            sourcePosition = match.Index + match.Length;
        }

        Uebertrage(sourcePosition, source.Length, outputPosition);
        return result;

        void Uebertrage(int segmentStart, int segmentEnd, int targetStart)
        {
            if (segmentEnd <= segmentStart)
                return;

            foreach (var range in sourceRanges)
            {
                var start = Math.Max(segmentStart, range.Start);
                var end = (int)Math.Min(
                    segmentEnd,
                    (long)range.Start + range.Length);
                if (end <= start)
                    continue;

                result.Add(new DossierTextStyleRange
                {
                    Start = targetStart + start - segmentStart,
                    Length = end - start,
                    ColorHex = range.ColorHex,
                    Bold = range.Bold,
                    Italic = range.Italic,
                    Underline = range.Underline
                });
            }
        }
    }

    /// <summary>
    /// Schreibt einen Wert in mehrere Word-Runs. Eigenschaften der Vorlage wie
    /// Schriftgroesse und Absatzabstand bleiben dabei erhalten; nur Arial,
    /// Farbe, Fett, Kursiv und Unterstreichen stammen aus der Eingabe.
    /// </summary>
    internal static void WriteBackFormatted(
        Paragraph paragraph,
        List<Text> texts,
        string replaced,
        IReadOnlyList<DossierTextStyleRange> ranges)
    {
        var firstRun = texts[0].Ancestors<Run>().FirstOrDefault();
        if (firstRun?.Parent is null)
        {
            WriteBack(paragraph, texts, replaced);
            return;
        }

        foreach (var text in texts)
            text.Text = string.Empty;

        var baseProperties = firstRun.RunProperties is null
            ? new RunProperties()
            : (RunProperties)firstRun.RunProperties.CloneNode(deep: true);

        firstRun.RemoveAllChildren();
        OpenXmlElement anchor = firstRun;
        var first = true;

        foreach (var segment in DossierTopicTextFormatting.Split(replaced, ranges))
        {
            var run = first ? firstRun : new Run();
            run.RunProperties = FormatProperties(baseProperties, segment);
            AppendRunText(run, segment.Text);

            if (!first)
            {
                anchor.Parent!.InsertAfter(run, anchor);
                anchor = run;
            }

            first = false;
        }
    }

    private static RunProperties FormatProperties(
        RunProperties source,
        DossierTopicTextFormatting.Segment segment)
    {
        var properties = (RunProperties)source.CloneNode(deep: true);
        properties.RunFonts = new RunFonts
        {
            Ascii = "Arial",
            HighAnsi = "Arial",
            EastAsia = "Arial",
            ComplexScript = "Arial"
        };
        properties.Bold = segment.Bold ? new Bold() : null;
        properties.Italic = segment.Italic ? new Italic() : null;
        properties.Underline = segment.Underline
            ? new Underline { Val = UnderlineValues.Single }
            : null;
        properties.Color = new Color
        {
            Val = DossierTopicTextFormatting.IsColor(segment.ColorHex)
                ? segment.ColorHex
                : "000000"
        };
        return properties;
    }

    private static void AppendRunText(Run run, string value)
    {
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.AppendChild(new Break());

            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    /// <summary>
    /// Vereinheitlicht nur die Schriftfamilie. Groessen, Zeilenabstaende,
    /// Tabellenhoehen und Fusszeilenaufbau bleiben unveraendert.
    /// </summary>
    public static void SetArial(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var main = document.MainDocumentPart;
        if (main?.Document is not null)
            SetArial(main.Document);

        foreach (var header in main?.HeaderParts ?? Enumerable.Empty<HeaderPart>())
        {
            if (header.Header is not null)
                SetArial(header.Header);
        }

        foreach (var footer in main?.FooterParts ?? Enumerable.Empty<FooterPart>())
        {
            if (footer.Footer is not null)
                SetArial(footer.Footer);
        }
    }

    private static void SetArial(OpenXmlElement scope)
    {
        foreach (var run in scope.Descendants<Run>())
        {
            run.RunProperties ??= new RunProperties();
            run.RunProperties.RunFonts = new RunFonts
            {
                Ascii = "Arial",
                HighAnsi = "Arial",
                EastAsia = "Arial",
                ComplexScript = "Arial"
            };
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
        => ReplacePlaceholders(input, values, literalRanges: null);

    private static string ReplacePlaceholders(
        string input,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<DocxLiteralRange>? literalRanges)
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

            if (IsProtectedLiteral(start, literalRanges))
            {
                // Dieser Text stammt aus einer Benutzereingabe. Auch wenn er
                // wie {{Datum}} aussieht, ist er kein Steuerzeichen der
                // Word-Vorlage und bleibt deshalb genau so stehen.
                result.Append(input, index, start + 2 - index);
                index = start + 2;
                continue;
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

    private static IEnumerable<Match> PlaceholderMatches(
        string input,
        IReadOnlyList<DocxLiteralRange>? literalRanges)
        => Regex.Matches(input, @"\{\{([A-Za-z0-9_]+)\}\}")
            .Cast<Match>()
            .Where(match => !IsProtectedLiteral(match.Index, literalRanges));

    private static bool IsProtectedLiteral(
        int position,
        IReadOnlyList<DocxLiteralRange>? literalRanges)
    {
        if (literalRanges is null)
            return false;

        foreach (var range in literalRanges)
        {
            if (position >= range.Start
                && position < (long)range.Start + range.Length)
            {
                return true;
            }
        }

        return false;
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
