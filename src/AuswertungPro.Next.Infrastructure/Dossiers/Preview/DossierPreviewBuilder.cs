using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Preview;

/// <summary>
/// Liest die ausgelieferte Word-Vorlage und baut daraus das Vorschaumodell.
///
/// Bewusst aus der ECHTEN Vorlage und nicht aus einer nachgebauten Beschreibung:
/// eine Vorschau, die eine andere Reihenfolge oder andere Felder zeigt als die
/// Vorlage, waere schlimmer als gar keine. Aendert jemand die Vorlage in Word,
/// aendert sich die Vorschau mit.
///
/// Die Platzhalter bleiben als Platzhalter stehen. Erst das Fenster setzt Werte
/// ein — nur so weiss es, welche Stelle zu welchem Feld gehoert.
/// </summary>
public static class DossierPreviewBuilder
{
    private static readonly Regex Platzhalter = new(
        @"\{\{(?<art>[@#]?)(?<name>[A-Za-z0-9_]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static DossierPreviewDocument Build(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("Kein Pfad zur Vorlage.", nameof(templatePath));

        using var document = WordprocessingDocument.Open(templatePath, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
            return new DossierPreviewDocument(Array.Empty<DossierPreviewPage>());

        return Build(body);
    }

    internal static DossierPreviewDocument Build(Body body)
    {
        var seiten = new List<DossierPreviewPage>();
        var aktuell = new List<DossierPreviewBlock>();

        // Die Liste wird geleert, nicht ersetzt: an anderer Stelle liegt eine
        // Referenz darauf, und eine neue Liste haette die Bloecke weiter in die
        // bereits abgeschlossene Seite geschrieben.
        void SeiteAbschliessen()
        {
            if (aktuell.Count == 0)
                return;

            seiten.Add(BaueSeite(seiten.Count + 1, aktuell.ToList()));
            aktuell.Clear();
        }

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph absatz:
                    VerarbeiteAbsatz(absatz, aktuell, SeiteAbschliessen);
                    break;

                case Table tabelle:
                    var block = BaueTabelle(tabelle);
                    if (block is not null)
                        aktuell.Add(block);
                    break;
            }
        }

        SeiteAbschliessen();
        return new DossierPreviewDocument(seiten);
    }

    private static void VerarbeiteAbsatz(
        Paragraph absatz, List<DossierPreviewBlock> ziel, Action seiteAbschliessen)
    {
        // Ein Absatz mit Kind-Absaetzen ist die Huelle um Textfelder. Seine
        // Kinder sind der eigentliche Inhalt — die Rueckfallfassung, die Word
        // zu jedem Feld ablegt, wird dabei uebersprungen.
        var innere = absatz.Descendants<Paragraph>().ToList();
        if (innere.Count > 0)
        {
            // Die Huelle traegt oft selbst Text und den Seitenumbruch — auf dem
            // Deckblatt haengen die Textfelder an genau dem Absatz, der auch
            // "Aenderungswesen:" enthaelt. Wird nur nach innen geschaut, geht
            // dieser Text verloren und der Umbruch bleibt unbemerkt.
            if (UmbruchVorText(absatz))
                seiteAbschliessen();

            foreach (var inneres in innere.Where(p => !LiegtInRueckfall(p)))
                FuegeAbsatzHinzu(inneres, ziel);

            var eigenerText = EigenerText(absatz);
            if (eigenerText.Trim().Length > 0)
                ziel.Add(new DossierPreviewParagraph(LiesStil(absatz), Zerlege(eigenerText)));

            return;
        }

        var stil = LiesStil(absatz);

        // Steht der Umbruch VOR dem Text, eroeffnet dieser Absatz die neue
        // Seite. Steht er dahinter, schliesst er die laufende ab. In dieser
        // Vorlage ist es der erste Fall — ohne die Unterscheidung landete die
        // Kapitelueberschrift allein auf einer Seite und ihr Inhalt auf der
        // naechsten.
        var umbruchVorText = UmbruchVorText(absatz);

        // Ein Kapitel beginnt ebenfalls eine neue Seite; die echte Seitenzahl
        // kaeme erst aus dem Umbruch in Word.
        if (umbruchVorText || (stil == DossierPreviewStyle.Heading && ziel.Count > 0))
            seiteAbschliessen();

        FuegeAbsatzHinzu(absatz, ziel);

        if (!umbruchVorText && HatSeitenumbruch(absatz))
            seiteAbschliessen();
    }

    /// <summary>
    /// Der Text, der dem Absatz selbst gehoert — ohne die Textfelder, die an
    /// ihm haengen.
    /// </summary>
    private static string EigenerText(Paragraph absatz)
        => string.Concat(absatz.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
            .Where(t => !LiegtInRueckfall(t))
            .Where(t => t.Ancestors<Paragraph>().FirstOrDefault() == absatz)
            .Select(t => t.Text));

    private static bool HatSeitenumbruch(Paragraph absatz)
        => absatz.Descendants<Break>()
            .Any(b => b.Type is not null && b.Type.Value == BreakValues.Page);

    /// <summary>
    /// Wahr, wenn der Seitenumbruch vor dem ersten sichtbaren Text steht.
    /// </summary>
    private static bool UmbruchVorText(Paragraph absatz)
    {
        foreach (var element in absatz.Descendants())
        {
            if (element is Break bruch
                && bruch.Type is not null
                && bruch.Type.Value == BreakValues.Page)
            {
                return true;
            }

            if (element is DocumentFormat.OpenXml.Wordprocessing.Text text
                && text.Text.Trim().Length > 0)
            {
                return false;
            }
        }

        return false;
    }

    private static void FuegeAbsatzHinzu(Paragraph absatz, List<DossierPreviewBlock> ziel)
    {
        var text = Text(absatz);
        if (text.Trim().Length == 0)
            return;

        // Ein Absatz, der NUR aus einer Bildmarke besteht, ist die Bildstelle.
        var nurBild = Platzhalter.Match(text.Trim());
        if (nurBild.Success
            && nurBild.Length == text.Trim().Length
            && nurBild.Groups["art"].Value == "@")
        {
            ziel.Add(new DossierPreviewImage(nurBild.Groups["name"].Value));
            return;
        }

        ziel.Add(new DossierPreviewParagraph(LiesStil(absatz), Zerlege(text)));
    }

    /// <summary>
    /// Zerlegt einen Text in feste Stuecke und Platzhalter. Wiederholmarken
    /// ("{{#Themen}}") gehoeren zur Tabelle und erscheinen nicht als Feld.
    /// </summary>
    internal static IReadOnlyList<DossierPreviewRun> Zerlege(string text)
    {
        var runs = new List<DossierPreviewRun>();
        var stelle = 0;

        foreach (Match treffer in Platzhalter.Matches(text))
        {
            if (treffer.Index > stelle)
                runs.Add(DossierPreviewRun.Literal(text[stelle..treffer.Index]));

            var art = treffer.Groups["art"].Value;
            if (art != "#")
                runs.Add(DossierPreviewRun.Field(treffer.Groups["name"].Value));

            stelle = treffer.Index + treffer.Length;
        }

        if (stelle < text.Length)
            runs.Add(DossierPreviewRun.Literal(text[stelle..]));

        return runs;
    }

    private static DossierPreviewBlock? BaueTabelle(Table tabelle)
    {
        var zeilen = tabelle.Elements<TableRow>().ToList();
        if (zeilen.Count == 0)
            return null;

        var kopf = Zellen(zeilen[0]).Select(z => Text(z).Trim()).ToList();
        var feste = new List<IReadOnlyList<DossierPreviewRun>>();
        string? wiederholung = null;
        var wiederholZellen = new List<string>();

        foreach (var zeile in zeilen.Skip(1))
        {
            var zellen = Zellen(zeile).Select(Text).ToList();
            var marke = zellen.Count == 0
                ? Match.Empty
                : Regex.Match(zellen[0], @"\{\{#(?<name>[A-Za-z0-9_]+)\}\}");

            if (marke.Success)
            {
                wiederholung = marke.Groups["name"].Value;
                wiederholZellen = zellen
                    .Select(z => Platzhalter.Match(z))
                    .Select(t => t.Success && t.Groups["art"].Value != "#"
                        ? t.Groups["name"].Value
                        : NaechstesFeld(t))
                    .ToList();

                // Die erste Zelle traegt Marke UND Feld.
                var erstes = Platzhalter.Matches(zellen[0])
                    .Select(t => t)
                    .FirstOrDefault(t => t.Groups["art"].Value != "#");
                wiederholZellen[0] = erstes?.Groups["name"].Value ?? string.Empty;
                continue;
            }

            foreach (var zelle in zellen)
                feste.Add(Zerlege(zelle));
        }

        return new DossierPreviewTable(kopf, feste, wiederholung, wiederholZellen);
    }

    private static string NaechstesFeld(Match treffer)
        => treffer.Success && treffer.Groups["art"].Value != "#"
            ? treffer.Groups["name"].Value
            : string.Empty;

    private static IEnumerable<TableCell> Zellen(TableRow zeile)
        => zeile.Elements<TableCell>();

    private static string Text(OpenXmlElement element)
        => string.Concat(element.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
            .Where(t => !LiegtInRueckfall(t))
            .Select(t => t.Text));

    /// <summary>
    /// Word legt zu jedem Textfeld eine Rueckfallfassung ab. Ohne diese Grenze
    /// erschiene jede Deckblattzeile doppelt.
    /// </summary>
    private static bool LiegtInRueckfall(OpenXmlElement element)
    {
        for (var eltern = element.Parent; eltern is not null; eltern = eltern.Parent)
        {
            if (eltern is AlternateContentFallback)
                return true;
        }

        return false;
    }

    private static DossierPreviewStyle LiesStil(Paragraph absatz)
    {
        var name = absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;

        if (name.StartsWith("berschrift", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Überschrift", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            return DossierPreviewStyle.Heading;
        }

        if (name.Equals("Titel", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            return DossierPreviewStyle.Title;
        }

        if (name.StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
        {
            return DossierPreviewStyle.Small;
        }

        // Das Deckblatt arbeitet ohne Formatvorlage, nur mit direkter
        // Schriftgroesse. Sie ist der einzige Hinweis auf die Bedeutung.
        var groesse = absatz.Descendants<FontSize>()
            .Select(f => int.TryParse(f.Val?.Value, out var wert) ? wert : 0)
            .DefaultIfEmpty(0)
            .Max();

        return groesse switch
        {
            >= 40 => DossierPreviewStyle.Title,
            >= 28 => DossierPreviewStyle.Heading,
            _ => DossierPreviewStyle.Normal
        };
    }

    private static DossierPreviewPage BaueSeite(int nummer, List<DossierPreviewBlock> blocks)
    {
        var titel = blocks
            .OfType<DossierPreviewParagraph>()
            .Where(p => p.Style is DossierPreviewStyle.Heading or DossierPreviewStyle.Title)
            .Select(p => Klartext(p.Runs))
            .FirstOrDefault(t => t.Length > 0);

        // Die erste Seite heisst immer Deckblatt. Ihr groesster Text ist der
        // Dokumenttitel; ihn als Seitennamen zu fuehren waere nur verwirrend.
        if (nummer == 1 || string.IsNullOrWhiteSpace(titel))
            titel = nummer == 1 ? "Deckblatt" : "Seite " + nummer;

        return new DossierPreviewPage(nummer, titel, blocks, SammleFelder(blocks));
    }

    private static string Klartext(IEnumerable<DossierPreviewRun> runs)
        => string.Concat(runs.Where(r => !r.IsField).Select(r => r.Text)).Trim();

    private static IReadOnlyList<string> SammleFelder(IEnumerable<DossierPreviewBlock> blocks)
    {
        var felder = new List<string>();

        void Sammle(IEnumerable<DossierPreviewRun> runs)
        {
            foreach (var run in runs.Where(r => r.IsField))
            {
                if (!felder.Contains(run.FieldKey!, StringComparer.Ordinal))
                    felder.Add(run.FieldKey!);
            }
        }

        foreach (var block in blocks)
        {
            switch (block)
            {
                case DossierPreviewParagraph absatz:
                    Sammle(absatz.Runs);
                    break;

                case DossierPreviewImage bild when !felder.Contains(bild.FieldKey, StringComparer.Ordinal):
                    felder.Add(bild.FieldKey);
                    break;

                case DossierPreviewTable tabelle:
                    foreach (var zeile in tabelle.FixedRowCells)
                        Sammle(zeile);

                    if (tabelle.RepeatKey is not null
                        && !felder.Contains(tabelle.RepeatKey, StringComparer.Ordinal))
                    {
                        felder.Add(tabelle.RepeatKey);
                    }

                    break;
            }
        }

        return felder;
    }
}
