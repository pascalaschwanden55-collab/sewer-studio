using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers.Preview;

using A = DocumentFormat.OpenXml.Drawing;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Preview;

/// <summary>
/// Liest die ausgelieferte Word-Vorlage und baut daraus das Vorschaumodell —
/// mit den echten Massen: Seitenformat, Raender, Spaltenbreiten, Abstaende,
/// Schriften und die Lage der schwebenden Kaesten des Deckblatts.
///
/// Bewusst aus der ECHTEN Vorlage und nicht aus einer nachgebauten Beschreibung:
/// eine Vorschau, die anders aussieht als das Dokument, waere schlimmer als gar
/// keine. Aendert jemand die Vorlage in Word, aendert sich die Vorschau mit.
///
/// Die Platzhalter bleiben als Platzhalter stehen. Erst das Fenster setzt Werte
/// ein — nur so weiss es, welche Stelle zu welchem Feld gehoert.
/// </summary>
public static class DossierPreviewBuilder
{
    private static readonly Regex Platzhalter = new(
        @"\{\{(?<art>[@#]?)(?<name>[A-Za-z0-9_]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>A4, falls die Vorlage kein Format nennt.</summary>
    private static readonly DossierPreviewGeometry StandardSeite = new(
        DocxFormatResolver.TwipsZuPixel(11906),
        DocxFormatResolver.TwipsZuPixel(16838),
        DossierPreviewEdges.All(DocxFormatResolver.TwipsZuPixel(1134)));

    public static DossierPreviewDocument Build(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("Kein Pfad zur Vorlage.", nameof(templatePath));

        using var document = WordprocessingDocument.Open(templatePath, false);
        var mainPart = document.MainDocumentPart;
        var body = mainPart?.Document?.Body;

        if (mainPart is null || body is null)
            return new DossierPreviewDocument(Array.Empty<DossierPreviewPage>());

        return new Leser(mainPart).Baue(body);
    }

    /// <summary>
    /// Der eigentliche Lauf. Als eigene Instanz, damit Formatleser und
    /// Bildteile nicht durch jede Methode gereicht werden muessen.
    /// </summary>
    private sealed class Leser
    {
        private readonly MainDocumentPart _mainPart;
        private readonly DocxFormatResolver _format;

        private readonly List<DossierPreviewPage> _seiten = new();
        private readonly List<DossierPreviewBlock> _bloecke = new();

        private DossierPreviewGeometry _geometrie = StandardSeite;

        public Leser(MainDocumentPart mainPart)
        {
            _mainPart = mainPart;
            _format = new DocxFormatResolver(mainPart);
        }

        public DossierPreviewDocument Baue(Body body)
        {
            _geometrie = LiesGeometrie(body);

            foreach (var element in body.ChildElements)
            {
                switch (element)
                {
                    case Paragraph absatz:
                        VerarbeiteAbsatz(absatz);
                        break;

                    case Table tabelle:
                        _bloecke.Add(BaueTabelle(tabelle));
                        break;
                }
            }

            SeiteAbschliessen();
            return new DossierPreviewDocument(_seiten);
        }

        private static DossierPreviewGeometry LiesGeometrie(Body body)
        {
            var sect = body.Elements<SectionProperties>().FirstOrDefault()
                ?? body.Descendants<SectionProperties>().FirstOrDefault();

            var groesse = sect?.Elements<PageSize>().FirstOrDefault();
            var rand = sect?.Elements<PageMargin>().FirstOrDefault();

            if (groesse is null)
                return StandardSeite;

            return new DossierPreviewGeometry(
                DocxFormatResolver.TwipsZuPixel(groesse.Width?.Value ?? 11906),
                DocxFormatResolver.TwipsZuPixel(groesse.Height?.Value ?? 16838),
                new DossierPreviewEdges(
                    DocxFormatResolver.TwipsZuPixel(rand?.Left?.Value ?? 1134),
                    DocxFormatResolver.TwipsZuPixel(rand?.Top?.Value ?? 1134),
                    DocxFormatResolver.TwipsZuPixel(rand?.Right?.Value ?? 1134),
                    DocxFormatResolver.TwipsZuPixel(rand?.Bottom?.Value ?? 1134)));
        }

        // ── Seiten ────────────────────────────────────────────────────────

        private void SeiteAbschliessen()
        {
            if (_bloecke.Count == 0)
                return;

            var bloecke = _bloecke.ToList();
            RueckeVerzeichnisBeilagenDirektAnDieKapitel(bloecke);
            DossierPreviewTocLayout.Apply(bloecke);

            _seiten.Add(new DossierPreviewPage(
                _seiten.Count + 1,
                Seitentitel(_seiten.Count + 1, bloecke),
                _geometrie,
                bloecke,
                SammleFelder(bloecke)));

            _bloecke.Clear();
        }

        /// <summary>
        /// Die Vorlagenmarke für zusätzliche Verzeichnispunkte steht hinter
        /// einem leeren Absatz. Für die bearbeitbare Vorschau gehört sie
        /// unmittelbar unter den letzten echten Word-Eintrag. Es werden nur
        /// vollständig leere Absätze dazwischen entfernt; sichtbarer Inhalt
        /// bleibt unangetastet.
        /// </summary>
        private static void RueckeVerzeichnisBeilagenDirektAnDieKapitel(
            List<DossierPreviewBlock> blocks)
        {
            var attachmentIndex = blocks.FindIndex(block =>
                block is DossierPreviewParagraph paragraph
                && paragraph.Runs.Any(run =>
                    string.Equals(
                        run.FieldKey,
                        "Verzeichnis_Beilagen",
                        StringComparison.OrdinalIgnoreCase)));

            if (attachmentIndex <= 0)
                return;

            var lastEntryIndex = blocks.FindLastIndex(
                attachmentIndex - 1,
                block => block is DossierPreviewParagraph paragraph
                    && paragraph.TocEntry is not null);

            if (lastEntryIndex < 0)
                return;

            var entryParagraph = (DossierPreviewParagraph)blocks[lastEntryIndex];
            var attachmentParagraph = (DossierPreviewParagraph)blocks[attachmentIndex];
            var entryRunFormat = entryParagraph.Runs.FirstOrDefault()?.Format;
            if (entryRunFormat is not null)
            {
                blocks[attachmentIndex] = attachmentParagraph with
                {
                    Format = entryParagraph.Format,
                    Runs = attachmentParagraph.Runs
                        .Select(run => run with { Format = entryRunFormat })
                        .ToList()
                };
            }

            for (var index = attachmentIndex - 1; index > lastEntryIndex; index--)
            {
                if (blocks[index] is DossierPreviewParagraph paragraph
                    && paragraph.TocEntry is null
                    && paragraph.Floating.Count == 0
                    && paragraph.Runs.All(run =>
                        !run.IsField && string.IsNullOrWhiteSpace(run.Text)))
                {
                    blocks.RemoveAt(index);
                }
            }
        }

        private static string Seitentitel(int nummer, IReadOnlyList<DossierPreviewBlock> bloecke)
        {
            if (nummer == 1)
                return "Deckblatt";

            var absaetze = bloecke.OfType<DossierPreviewParagraph>().ToList();

            // Zuerst das Kapitel, sonst der groesste Titel der Seite. Ein
            // Kapitel beginnt eine Seite, ein Titel nicht — deshalb zaehlt er
            // nur fuer die Beschriftung.
            var titel = absaetze
                .Where(p => p.Format.IsHeading)
                .Select(Klartext)
                .FirstOrDefault(t => t.Length > 0)
                ?? absaetze
                    .Where(p => p.Format.IsTitle)
                    .Select(Klartext)
                    .FirstOrDefault(t => t.Length > 0);

            return string.IsNullOrWhiteSpace(titel) ? "Seite " + nummer : titel;
        }

        private static string Klartext(DossierPreviewParagraph absatz)
            => string.Concat(absatz.Runs.Where(r => !r.IsField).Select(r => r.Text)).Trim();

        // ── Absaetze ──────────────────────────────────────────────────────

        private void VerarbeiteAbsatz(Paragraph absatz)
        {
            var format = _format.AbsatzFormat(absatz);

            // Steht der Umbruch VOR dem Text, eroeffnet dieser Absatz die neue
            // Seite. Ohne diese Unterscheidung landete die Kapitelueberschrift
            // allein auf einer Seite und ihr Inhalt auf der naechsten.
            var umbruchVorText = UmbruchVorText(absatz);
            var eigenerText = EigenerText(absatz);

            if (umbruchVorText
                || (format.IsHeading && eigenerText.Trim().Length > 0 && _bloecke.Count > 0))
            {
                SeiteAbschliessen();
            }

            var schwebend = LiesSchwebende(absatz);

            foreach (var bild in LiesEingebetteteBilder(absatz))
                _bloecke.Add(bild);

            // Auch ein LEERER Absatz wird uebernommen: er traegt im Dokument den
            // senkrechten Abstand, und die an ihm haengenden Kaesten zaehlen ihre
            // Hoehe ab genau dieser Stelle.
            FuegeTextHinzu(absatz, eigenerText, format, schwebend);

            if (!umbruchVorText && HatSeitenumbruch(absatz))
                SeiteAbschliessen();
        }

        private void FuegeTextHinzu(
            Paragraph absatz,
            string text,
            DossierPreviewParagraphFormat format,
            IReadOnlyList<DossierPreviewFloating> schwebend)
        {
            // Ein Absatz, der NUR aus einer Bildmarke besteht, ist die Bildstelle.
            var nurBild = Platzhalter.Match(text.Trim());
            if (nurBild.Success
                && nurBild.Length == text.Trim().Length
                && nurBild.Groups["art"].Value == "@")
            {
                // Dieselbe Breite, die der Export ins Dokument setzt. Die Hoehe
                // folgt erst beim Zeichnen aus dem echten Seitenverhaeltnis.
                var breite = DossierWordTemplateExportService.PlanMaxWidthCm / 2.54 * 96.0;

                _bloecke.Add(new DossierPreviewImage(nurBild.Groups["name"].Value, breite));
                return;
            }

            var runs = text.Length > 0
                ? Zerlege(absatz, text)
                : new[] { DossierPreviewRun.Literal(string.Empty, _format.RunFormat(absatz, null)) };

            var toc = format.IsTableOfContentsEntry
                ? DocxTocEntryReader.Read(absatz)
                : null;

            _bloecke.Add(new DossierPreviewParagraph(
                runs,
                format,
                schwebend,
                toc is null
                    ? null
                    : new DossierPreviewTocEntry(toc.Number, toc.Title, toc.PageNumber)));
        }

        /// <summary>
        /// Zerlegt den Absatz in Textstuecke und Platzhalter. Das Zeichenformat
        /// stammt von dem Run, in dem die Stelle beginnt — so behaelt ein
        /// eingesetzter Wert die Schrift, die die Vorlage dort vorsieht.
        /// </summary>
        private IReadOnlyList<DossierPreviewRun> Zerlege(Paragraph absatz, string text)
        {
            var stuecke = EigeneStuecke(absatz).ToList();
            var runs = new List<DossierPreviewRun>();
            var stelle = 0;

            DossierPreviewRunFormat FormatAn(int position)
            {
                var lauf = 0;
                foreach (var (run, laenge) in stuecke)
                {
                    lauf += laenge;
                    if (position < lauf)
                        return _format.RunFormat(absatz, run);
                }

                return _format.RunFormat(
                    absatz, stuecke.Count > 0 ? stuecke[^1].Run : null);
            }

            foreach (Match treffer in Platzhalter.Matches(text))
            {
                if (treffer.Index > stelle)
                {
                    runs.Add(DossierPreviewRun.Literal(
                        text[stelle..treffer.Index], FormatAn(stelle)));
                }

                if (treffer.Groups["art"].Value != "#")
                {
                    runs.Add(DossierPreviewRun.Field(
                        treffer.Groups["name"].Value, FormatAn(treffer.Index)));
                }

                stelle = treffer.Index + treffer.Length;
            }

            if (stelle < text.Length)
                runs.Add(DossierPreviewRun.Literal(text[stelle..], FormatAn(stelle)));

            return runs;
        }

        private static List<(Run? Run, int Laenge)> EigeneStuecke(Paragraph absatz)
            => EigeneTexte(absatz)
                .Select(t => (t.Ancestors<Run>().FirstOrDefault(), t.Text.Length))
                .ToList();

        /// <summary>
        /// Der Text, der dem Absatz selbst gehoert — ohne die Kaesten, die an
        /// ihm haengen.
        /// </summary>
        private static string EigenerText(Paragraph absatz)
            => string.Concat(EigeneTexte(absatz).Select(t => t.Text));

        private static IEnumerable<WText> EigeneTexte(Paragraph absatz)
            => absatz.Descendants<WText>()
                .Where(t => !t.Ancestors<Drawing>().Any())
                .Where(t => t.Ancestors<Paragraph>().FirstOrDefault() == absatz);

        private static bool HatSeitenumbruch(Paragraph absatz)
            => absatz.Descendants<Break>()
                .Any(b => b.Type is not null && b.Type.Value == BreakValues.Page);

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

                if (element is WText text && text.Text.Trim().Length > 0)
                    return false;
            }

            return false;
        }

        // ── Schwebende Objekte ────────────────────────────────────────────

        /// <summary>
        /// Die Kaesten, Bilder und Rahmen, die an diesem Absatz haengen. Gelesen
        /// wird nur der moderne Zweig; die Rueckfallfassung, die Word zu jedem
        /// Kasten zusaetzlich ablegt, kommt so gar nicht erst in die Naehe.
        /// </summary>
        private List<DossierPreviewFloating> LiesSchwebende(Paragraph absatz)
        {
            var ergebnis = new List<DossierPreviewFloating>();

            foreach (var anker in absatz.Descendants<Anchor>())
            {
                var ausdehnung = anker.Descendants<Extent>().FirstOrDefault();
                if (ausdehnung is null)
                    continue;

                var breite = DocxFormatResolver.EmuZuPixel(ausdehnung.Cx ?? 0);
                var hoehe = DocxFormatResolver.EmuZuPixel(ausdehnung.Cy ?? 0);

                var (links, oben) = Lage(anker, breite);
                var (randbreite, randfarbe, fuellung) = Umriss(anker);

                ergebnis.Add(new DossierPreviewFloating(
                    links, oben, breite, hoehe,
                    InhaltDesKastens(anker, breite, hoehe),
                    randbreite, randfarbe, fuellung));
            }

            return ergebnis;
        }

        /// <summary>
        /// Lage auf dem Blatt. Waagrecht zaehlt der Bezug: "page" ab dem
        /// Blattrand, alles andere ab dem Satzspiegel. Senkrecht bezieht sich
        /// Word auf den Absatz — auf dem Deckblatt ist das der obere Rand.
        /// </summary>
        private (double Links, double Oben) Lage(Anchor anker, double breite)
        {
            var h = anker.HorizontalPosition;
            var v = anker.VerticalPosition;

            var abPage = h?.RelativeFrom?.Value == HorizontalRelativePositionValues.Page;
            var basis = abPage ? 0 : _geometrie.Margin.Left;

            double links;
            if (h?.PositionOffset?.Text is { } versatz
                && long.TryParse(versatz, out var emu))
            {
                links = basis + DocxFormatResolver.EmuZuPixel(emu);
            }
            else if (h?.HorizontalAlignment?.Text is { } ausrichtung)
            {
                var satzbreite = _geometrie.WidthPx
                    - _geometrie.Margin.Left - _geometrie.Margin.Right;

                links = ausrichtung.Trim().ToLowerInvariant() switch
                {
                    "right" => basis + satzbreite - breite,
                    "center" => basis + (satzbreite - breite) / 2,
                    _ => basis
                };
            }
            else
            {
                links = basis;
            }

            // Senkrecht zaehlt Word ab dem Absatz, an dem das Objekt haengt.
            // Der obere Seitenrand steckt bereits in der Lage dieses Absatzes —
            // ihn hier nochmals zu addieren schoebe jeden Kasten um einen
            // ganzen Rand nach unten.
            var oben = 0.0;
            if (v?.PositionOffset?.Text is { } hoch && long.TryParse(hoch, out var emuV))
                oben = DocxFormatResolver.EmuZuPixel(emuV);

            return (links, oben);
        }

        private static (double Breite, string? Farbe, string? Fuellung) Umriss(Anchor anker)
        {
            // Die Formangaben liegen je nach Objektart in verschiedenen
            // Namensraeumen (Zeichnung, Textkasten, Bild). Gesucht wird deshalb
            // ueber den lokalen Namen.
            var form = anker.Descendants()
                .FirstOrDefault(e => string.Equals(e.LocalName, "spPr", StringComparison.Ordinal));

            if (form is null)
                return (0, null, null);

            var fuellung = form.Elements<A.SolidFill>().FirstOrDefault()
                ?.Elements<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value;

            var linie = form.Elements<A.Outline>().FirstOrDefault();
            if (linie is null)
                return (0, null, fuellung);

            var farbe = linie.Elements<A.SolidFill>().FirstOrDefault()
                ?.Elements<A.RgbColorModelHex>().FirstOrDefault()?.Val?.Value;

            if (farbe is null)
                return (0, null, fuellung);

            var breite = linie.Width is { } w ? DocxFormatResolver.EmuZuPixel(w) : 1;
            return (Math.Max(0.5, breite), farbe, fuellung);
        }

        private List<DossierPreviewBlock> InhaltDesKastens(
            Anchor anker, double breite, double hoehe)
        {
            var inhalt = new List<DossierPreviewBlock>();

            foreach (var absatz in anker.Descendants<TextBoxContent>()
                         .SelectMany(t => t.Elements<Paragraph>()))
            {
                var text = string.Concat(absatz.Descendants<WText>().Select(t => t.Text));
                if (text.Trim().Length == 0)
                    continue;

                inhalt.Add(new DossierPreviewParagraph(
                    Zerlege(absatz, text), _format.AbsatzFormat(absatz)));
            }

            inhalt.AddRange(LiesBilder(anker, breite, hoehe));
            return inhalt;
        }

        private List<DossierPreviewBlock> LiesEingebetteteBilder(Paragraph absatz)
        {
            var ergebnis = new List<DossierPreviewBlock>();

            foreach (var inline in absatz.Descendants<Inline>())
            {
                var ausdehnung = inline.Descendants<Extent>().FirstOrDefault();
                ergebnis.AddRange(LiesBilder(
                    inline,
                    DocxFormatResolver.EmuZuPixel(ausdehnung?.Cx ?? 0),
                    DocxFormatResolver.EmuZuPixel(ausdehnung?.Cy ?? 0)));
            }

            return ergebnis;
        }

        private List<DossierPreviewBlock> LiesBilder(
            OpenXmlElement wurzel, double breite, double hoehe)
        {
            var ergebnis = new List<DossierPreviewBlock>();

            foreach (var blip in wurzel.Descendants<A.Blip>())
            {
                var id = blip.Embed?.Value;
                if (id is null)
                    continue;

                try
                {
                    if (_mainPart.GetPartById(id) is not ImagePart teil)
                        continue;

                    using var strom = teil.GetStream();
                    using var speicher = new System.IO.MemoryStream();
                    strom.CopyTo(speicher);
                    ergebnis.Add(new DossierPreviewPicture(speicher.ToArray(), breite, hoehe));
                }
                catch (Exception)
                {
                    // Ein unlesbares Bild darf die Vorschau nicht verhindern.
                }
            }

            return ergebnis;
        }

        // ── Tabellen ──────────────────────────────────────────────────────

        private DossierPreviewTable BaueTabelle(Table tabelle)
        {
            var breiten = tabelle.Elements<TableGrid>().FirstOrDefault()
                ?.Elements<GridColumn>()
                .Select(c => DocxFormatResolver.TwipsZuPixel(Zahl(c.Width?.Value) ?? 0))
                .ToList()
                ?? new List<double>();

            var eigenschaften = tabelle.Elements<TableProperties>().FirstOrDefault();
            var einzug = DocxFormatResolver.TwipsZuPixel(
                Zahl(eigenschaften?.TableIndentation?.Width?.Value.ToString()) ?? 0);

            var standardRand = ZellRand(eigenschaften?.TableCellMarginDefault);
            var tabellenrahmen = eigenschaften?.TableBorders;

            var zeilen = new List<DossierPreviewTableRow>();
            string? wiederholung = null;
            var wiederholZellen = new List<string>();
            DossierPreviewTableRow? bauplan = null;
            var wiederholStelle = -1;

            foreach (var zeile in tabelle.Elements<TableRow>())
            {
                var zellen = zeile.Elements<TableCell>().ToList();
                var texte = zellen
                    .Select(z => string.Concat(z.Descendants<WText>().Select(t => t.Text)))
                    .ToList();

                var marke = texte.Count == 0
                    ? Match.Empty
                    : Regex.Match(texte[0], @"\{\{#(?<name>[A-Za-z0-9_]+)\}\}");

                var gebaut = new DossierPreviewTableRow(
                    zellen.Select(z => BaueZelle(z, standardRand, tabellenrahmen)).ToList());

                if (marke.Success)
                {
                    wiederholung = marke.Groups["name"].Value;
                    wiederholZellen = texte
                        .Select(t => Platzhalter.Matches(t)
                            .FirstOrDefault(m => m.Groups["art"].Value != "#")
                            ?.Groups["name"].Value ?? string.Empty)
                        .ToList();
                    bauplan = gebaut;
                    wiederholStelle = zeilen.Count;
                    continue;
                }

                zeilen.Add(gebaut);
            }

            return new DossierPreviewTable(
                breiten, einzug, zeilen, wiederholung, wiederholZellen, bauplan,
                wiederholStelle);
        }

        private DossierPreviewTableCell BaueZelle(
            TableCell zelle,
            DossierPreviewEdges standardRand,
            TableBorders? tabellenrahmen)
        {
            var eigenschaften = zelle.Elements<TableCellProperties>().FirstOrDefault();

            var absaetze = zelle.Elements<Paragraph>()
                .Select(p => new DossierPreviewParagraph(
                    Zerlege(p, string.Concat(p.Descendants<WText>().Select(t => t.Text))),
                    _format.AbsatzFormat(p)))
                .ToList();

            var rand = eigenschaften?.TableCellMargin is { } eigen
                ? ZellRand(eigen)
                : standardRand;

            var fuellung = eigenschaften?.Shading?.Fill?.Value;

            return new DossierPreviewTableCell(
                absaetze,
                rand,
                Rahmen(eigenschaften?.TableCellBorders, tabellenrahmen),
                fuellung is not null
                    && !string.Equals(fuellung, "auto", StringComparison.OrdinalIgnoreCase)
                        ? fuellung
                        : null,
                eigenschaften?.GridSpan?.Val?.Value ?? 1);
        }

        private static DossierPreviewEdges ZellRand(OpenXmlElement? rand)
        {
            var standard = DocxFormatResolver.TwipsZuPixel(108);

            if (rand is null)
                return DossierPreviewEdges.All(standard);

            double Wert(string name)
            {
                var element = rand.ChildElements
                    .FirstOrDefault(e => string.Equals(e.LocalName, name, StringComparison.Ordinal));

                var attribut = element?.GetAttributes()
                    .FirstOrDefault(a => a.LocalName == "w");

                return Zahl(attribut?.Value) is { } zahl
                    ? DocxFormatResolver.TwipsZuPixel(zahl)
                    : standard;
            }

            return new DossierPreviewEdges(
                Wert("left"), Wert("top"), Wert("right"), Wert("bottom"));
        }

        /// <summary>
        /// Rahmenbreiten der Zelle. Word zaehlt sie in Achtelpunkten; die
        /// Angabe der Zelle sticht die der Tabelle.
        /// </summary>
        private static DossierPreviewEdges Rahmen(
            TableCellBorders? zelle, TableBorders? tabelle)
        {
            double Breite(BorderType? kante)
                => kante?.Val?.Value is { } art
                    && art != BorderValues.None
                    && art != BorderValues.Nil
                        ? Math.Max(0.5, (kante.Size?.Value ?? 4) / 6.0)
                        : 0;

            BorderType? Waehle(BorderType? eigen, BorderType? tabellenkante, BorderType? innen)
                => eigen ?? tabellenkante ?? innen;

            return new DossierPreviewEdges(
                Breite(Waehle(zelle?.LeftBorder, tabelle?.LeftBorder, tabelle?.InsideVerticalBorder)),
                Breite(Waehle(zelle?.TopBorder, tabelle?.TopBorder, tabelle?.InsideHorizontalBorder)),
                Breite(Waehle(zelle?.RightBorder, tabelle?.RightBorder, tabelle?.InsideVerticalBorder)),
                Breite(Waehle(zelle?.BottomBorder, tabelle?.BottomBorder, tabelle?.InsideHorizontalBorder)));
        }

        private static double? Zahl(string? wert)
            => double.TryParse(
                wert,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var zahl)
                    ? zahl
                    : null;

        // ── Felder ────────────────────────────────────────────────────────

        private static IReadOnlyList<string> SammleFelder(IEnumerable<DossierPreviewBlock> bloecke)
        {
            var felder = new List<string>();

            void Nimm(string? key)
            {
                if (key is null || felder.Contains(key, StringComparer.Ordinal))
                    return;

                felder.Add(key);
            }

            void AusAbsatz(DossierPreviewParagraph absatz)
            {
                foreach (var run in absatz.Runs.Where(r => r.IsField))
                    Nimm(run.FieldKey);
            }

            foreach (var block in bloecke)
            {
                switch (block)
                {
                    case DossierPreviewParagraph absatz:
                        AusAbsatz(absatz);

                        foreach (var kasten in absatz.Floating)
                        {
                            foreach (var inneres in kasten.Blocks.OfType<DossierPreviewParagraph>())
                                AusAbsatz(inneres);
                        }

                        break;

                    case DossierPreviewImage bild:
                        Nimm(bild.FieldKey);
                        break;

                    case DossierPreviewTable tabelle:
                        foreach (var absatz in tabelle.Rows
                                     .SelectMany(z => z.Cells)
                                     .SelectMany(z => z.Paragraphs))
                        {
                            AusAbsatz(absatz);
                        }

                        Nimm(tabelle.RepeatKey);
                        break;
                }
            }

            return felder;
        }
    }
}
