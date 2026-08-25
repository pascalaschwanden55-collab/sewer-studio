using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Zeichenformate und geschuetzte Textbereiche fester Beschriftungen in
/// Absaetzen, deren echte Vorlagen-Platzhalter erst danach gefuellt werden.
/// Die Absatzreferenzen leben nur waehrend eines Exportlaufs und werden nie
/// gespeichert.
/// </summary>
internal sealed record DocxLiteralFormatting(
    IReadOnlyDictionary<Paragraph, IReadOnlyList<DossierTextStyleRange>> Paragraphs,
    IReadOnlyDictionary<Paragraph, IReadOnlyList<DocxLiteralRange>> LiteralRanges)
{
    public static DocxLiteralFormatting Empty { get; } = new(
        new Dictionary<Paragraph, IReadOnlyList<DossierTextStyleRange>>(),
        new Dictionary<Paragraph, IReadOnlyList<DocxLiteralRange>>());
}

/// <summary>
/// Ein vom Benutzer geschriebener Bereich. Platzhalter-aehnlicher Text darin
/// ist Inhalt und kein Steuerzeichen der Word-Vorlage.
/// </summary>
internal readonly record struct DocxLiteralRange(int Start, int Length);

/// <summary>
/// Ersetzt feste Texte der Vorlage durch eigene Angaben des Dossiers —
/// Kapitelueberschriften, Spaltentitel, jede Zeile ohne Platzhalter.
///
/// Der Schluessel ist der urspruengliche Text selbst. Das hat einen Vorteil und
/// eine Grenze, und beide sind gewollt: es braucht keine kuenstliche Nummer,
/// die beim Umbau der Vorlage verrutscht — und wird der Text in Word geaendert,
/// greift die Ersetzung nicht mehr. Dann steht wieder der Text der Vorlage da,
/// nicht ein Rest von gestern.
///
/// Ein LEERER Ersatz entfernt den Absatz. Wer eine Zeile nicht braucht, soll
/// keine leere Zeile verschicken.
/// </summary>
public static class DocxLiteralTextReplacer
{
    public static int Apply(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, List<DossierTextStyleRange>>? fieldStyles = null)
        => ApplyCore(document, overrides, fieldStyles).Changed;

    /// <summary>
    /// Ersetzt feste Texte, bevor die normalen Platzhalter gefuellt werden, und
    /// liefert die Formate gemischter Beschriftungen fuer diesen Fuellvorgang.
    /// </summary>
    internal static DocxLiteralFormatting ApplyBeforePlaceholderFill(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, List<DossierTextStyleRange>>? fieldStyles = null)
        => ApplyCore(document, overrides, fieldStyles).Formatting;

    private static (int Changed, DocxLiteralFormatting Formatting) ApplyCore(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, string>? overrides,
        IReadOnlyDictionary<string, List<DossierTextStyleRange>>? fieldStyles)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (overrides is null || overrides.Count == 0)
            return (0, DocxLiteralFormatting.Empty);

        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
            return (0, DocxLiteralFormatting.Empty);

        var karte = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (schluessel, wert) in overrides)
        {
            var sauber = (schluessel ?? string.Empty).Trim();
            if (sauber.Length > 0)
                karte[sauber] = wert ?? string.Empty;
        }

        var geaendert = 0;
        var gemischteFormate = new Dictionary<Paragraph, IReadOnlyList<DossierTextStyleRange>>();
        var literalTextbereiche = new Dictionary<Paragraph, IReadOnlyList<DocxLiteralRange>>();

        foreach (var absatz in body.Descendants<Paragraph>().ToList())
        {
            // Nur der innerste Absatz: eine Huelle um Textfelder traegt den
            // Text aller Felder zusammen und gehoert keinem davon.
            if (absatz.Descendants<Paragraph>().Any())
                continue;

            // Verzeichniszeilen gehoeren dem Verzeichnis-Editor. Ginge dieser
            // Weg darueber, schriebe er den ganzen Text in den ersten Lauf und
            // leerte die uebrigen — mitsamt Seitenzahl und Tabulatoren. Genau
            // das ist passiert, als der Schluessel noch die ganze Zeile war
            // ("1.Uebersichtsplan Werkleitungen3"): Die Zeile stand danach ohne
            // Seitenzahl und ohne Einzug da. Solche Eintraege liegen in alten
            // Dossiers weiterhin gespeichert und muessen wirkungslos bleiben.
            if (DossierTocStyle.IsEntry(
                    absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value))
            {
                continue;
            }

            var stuecke = absatz.Descendants<Text>().ToList();
            if (stuecke.Count == 0)
                continue;

            var text = string.Concat(stuecke.Select(t => t.Text)).Trim();

            if (text.Length == 0)
                continue;

            // Eine Zeile mit Platzhalter gehoert dem Feld — ihre BESCHRIFTUNG
            // aber nicht. In der Vorlage steht „Datum: {{Datum}}" als ein
            // einziger Lauf; frueher war damit auch das Wort „Datum:" gesperrt.
            if (text.Contains("{{", StringComparison.Ordinal))
            {
                if (ErsetzeBeschriftung(
                        stuecke,
                        text,
                        karte,
                        out var schluessel,
                        out var beschriftungsErsatz,
                        out var start))
                {
                    geaendert++;

                    if (beschriftungsErsatz.Length > 0)
                    {
                        literalTextbereiche[absatz] =
                        [
                            new DocxLiteralRange(start, beschriftungsErsatz.Length)
                        ];
                    }

                    var beschriftungsStyleKey =
                        DossierTopicTextFormatting.LiteralStyleKey(schluessel);
                    if (fieldStyles is not null
                        && fieldStyles.TryGetValue(
                            beschriftungsStyleKey,
                            out var gespeichert))
                    {
                        var beschriftungsRanges = DossierTopicTextFormatting
                            .Normalize(beschriftungsErsatz, gespeichert)
                            .Select(range => new DossierTextStyleRange
                            {
                                Start = start + range.Start,
                                Length = range.Length,
                                ColorHex = range.ColorHex,
                                Bold = range.Bold,
                                Italic = range.Italic,
                                Underline = range.Underline
                            })
                            .ToList();

                        if (beschriftungsRanges.Count > 0)
                            gemischteFormate[absatz] = beschriftungsRanges;
                    }
                }

                continue;
            }

            if (!karte.TryGetValue(text, out var ersatz))
                continue;

            if (ersatz.Trim().Length == 0)
            {
                absatz.Remove();
                geaendert++;
                continue;
            }

            var styleKey = DossierTopicTextFormatting.LiteralStyleKey(text);
            var ranges = fieldStyles is not null
                && fieldStyles.TryGetValue(styleKey, out var stored)
                    ? DossierTopicTextFormatting.Normalize(ersatz, stored)
                    : new List<DossierTextStyleRange>();

            if (ranges.Count > 0)
            {
                DocxPlaceholderFiller.WriteBackFormatted(absatz, stuecke, ersatz, ranges);
            }
            else
            {
                stuecke[0].Text = ersatz;
                stuecke[0].Space = SpaceProcessingModeValues.Preserve;

                for (var i = 1; i < stuecke.Count; i++)
                    stuecke[i].Text = string.Empty;
            }

            // Auch eine frei bearbeitete reine Ueberschrift darf Zeichen wie
            // {{Datum}} enthalten. Sie sind Benutzereingabe und kein nach dem
            // Ersetzen neu entstandener Vorlagen-Platzhalter.
            literalTextbereiche[absatz] =
            [
                new DocxLiteralRange(0, ersatz.Length)
            ];

            geaendert++;
        }

        return (
            geaendert,
            gemischteFormate.Count == 0 && literalTextbereiche.Count == 0
                ? DocxLiteralFormatting.Empty
                : new DocxLiteralFormatting(gemischteFormate, literalTextbereiche));
    }

    /// <summary>
    /// Ersetzt nur die Beschriftung eines Absatzes, in dem auch ein Platzhalter
    /// steht — Zeichen fuer Zeichen genau an ihrer Stelle.
    ///
    /// Der Platzhalter und alles um ihn herum bleiben unangetastet: Ein leerer
    /// Ersatz entfernt hier deshalb nur die Beschriftung und NICHT den Absatz.
    /// Bei einem reinen Textabsatz heisst leer „Zeile weg"; hier haenge am
    /// selben Absatz aber das Feld, und es mitzunehmen waere ein Datenverlust,
    /// den niemand bestellt hat.
    /// </summary>
    private static bool ErsetzeBeschriftung(
        List<Text> stuecke,
        string text,
        IReadOnlyDictionary<string, string> karte,
        out string schluessel,
        out string ersatz,
        out int start)
    {
        schluessel = string.Empty;
        ersatz = string.Empty;
        start = 0;

        if (DossierMixedParagraphLiteral.Bereich(text) is not { } bereich)
            return false;

        schluessel = text.Substring(bereich.Start, bereich.Length);
        if (!karte.TryGetValue(schluessel, out var eigenerText))
            return false;

        ersatz = eigenerText;

        // `text` ist getrimmt, die Stuecke sind es nicht. Ohne diesen Versatz
        // laege der Bereich um die fuehrenden Leerzeichen daneben.
        var ganz = string.Concat(stuecke.Select(t => t.Text));
        var versatz = ganz.IndexOf(text, StringComparison.Ordinal);
        if (versatz < 0)
            return false;

        var von = versatz + bereich.Start;
        var bis = von + bereich.Length;
        start = von;
        var gesetzt = false;
        var gelesen = 0;

        foreach (var stueck in stuecke)
        {
            var laenge = stueck.Text.Length;
            var stueckVon = gelesen;
            var stueckBis = gelesen + laenge;
            gelesen = stueckBis;

            if (stueckBis <= von || stueckVon >= bis)
                continue;

            var vorne = stueck.Text[..Math.Max(0, von - stueckVon)];
            var hinten = stueckBis > bis ? stueck.Text[(bis - stueckVon)..] : string.Empty;
            var mitte = gesetzt ? string.Empty : ersatz;
            gesetzt = true;

            stueck.Text = vorne + mitte + hinten;
            stueck.Space = SpaceProcessingModeValues.Preserve;
        }

        return gesetzt;
    }
}
