using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Vorschau entsteht aus der ausgelieferten Vorlage. Geprueft wird gegen
/// genau diese Datei — eine im Test nachgebaute Vorlage wuerde einen Weg
/// beweisen, den das Programm nie geht.
/// </summary>
public sealed class DossierPreviewBuilderTests
{
    private static DossierPreviewDocument Vorschau()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");

        return DossierPreviewBuilder.Build(pfad);
    }

    [Fact]
    public void Die_Vorlage_zerfaellt_in_benannte_Seiten()
    {
        var vorschau = Vorschau();

        var titel = vorschau.Pages.Select(s => s.Title).ToList();

        Assert.Equal("Deckblatt", titel[0]);
        Assert.Contains("Übersichtsplan Werkleitungen", titel);
        Assert.Contains("Eigentumsverhältnisse", titel);
        Assert.Contains("Informationen Sanierung", titel);

        // Fortlaufend nummeriert, damit das Fenster blaettern kann.
        Assert.Equal(
            Enumerable.Range(1, vorschau.Pages.Count),
            vorschau.Pages.Select(s => s.Number));
    }

    [Fact]
    public void Jede_Deckblattzeile_erscheint_genau_einmal()
    {
        // Word legt zu jedem Textfeld eine Rueckfallfassung ab. Ohne Grenze
        // stuende jede Zeile des Deckblatts doppelt in der Vorschau.
        var deckblatt = Vorschau().Pages.First();

        var titelFelder = deckblatt.Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .SelectMany(f => f.Blocks)
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Runs)
            .Count(r => r.FieldKey == "Gebietstitel");

        Assert.Equal(1, titelFelder);
    }

    [Fact]
    public void Das_Deckblatt_kennt_seine_Felder()
    {
        var deckblatt = Vorschau().Pages.First();

        Assert.Contains("Gebietstitel", deckblatt.FieldKeys);
        Assert.Contains("Parzellen_Zeile", deckblatt.FieldKeys);
        Assert.Contains("Revision", deckblatt.FieldKeys);
        Assert.Contains("Projekt_Nr", deckblatt.FieldKeys);
    }

    [Fact]
    public void Inhaltsverzeichnis_trennt_Titel_und_Seitenzahl_fuer_die_Bearbeitung()
    {
        var eintraege = Vorschau().Pages
            .SelectMany(seite => seite.Blocks)
            .OfType<DossierPreviewParagraph>()
            .Where(absatz => absatz.TocEntry is not null)
            .Select(absatz => absatz.TocEntry!)
            .ToList();

        Assert.Equal(3, eintraege.Count);
        Assert.Equal("1.", eintraege[0].Number);
        Assert.Equal("Übersichtsplan Werkleitungen", eintraege[0].Title);
        Assert.Equal("3", eintraege[0].PageNumber);
        Assert.Equal("Eigentumsverhältnisse", eintraege[1].Title);
        Assert.Equal("Informationen Sanierung", eintraege[2].Title);
    }

    [Fact]
    public void Zusaetzliche_Verzeichnispunkte_folgen_ohne_Leerzeile_auf_das_letzte_Kapitel()
    {
        var seite = Vorschau().Pages.Single(page => page.Blocks
            .OfType<DossierPreviewParagraph>()
            .Any(paragraph => paragraph.TocEntry is not null));
        var bloecke = seite.Blocks.ToList();
        var letztesKapitel = bloecke.FindLastIndex(block =>
            block is DossierPreviewParagraph paragraph && paragraph.TocEntry is not null);
        var beilage = bloecke.FindIndex(block =>
            block is DossierPreviewParagraph paragraph
            && paragraph.Runs.Any(run => run.FieldKey == "Verzeichnis_Beilagen"));

        Assert.True(letztesKapitel >= 0);
        Assert.Equal(letztesKapitel + 1, beilage);

        var kapitelFormat = Assert.IsType<DossierPreviewParagraph>(bloecke[letztesKapitel]).Format;
        var beilageFormat = Assert.IsType<DossierPreviewParagraph>(bloecke[beilage]).Format;
        Assert.Equal(kapitelFormat, beilageFormat);
    }

    [Fact]
    public void Der_Uebersichtsplan_ist_eine_Bildstelle_und_kein_Text()
    {
        var seite = Vorschau().Pages
            .Single(s => s.Title == "Übersichtsplan Werkleitungen");

        var bild = Assert.Single(seite.Blocks.OfType<DossierPreviewImage>());
        Assert.Equal("Uebersichtsplan", bild.FieldKey);
        Assert.True(bild.WidthPx > 400, "Die Bildstelle nimmt die Satzbreite ein.");
    }

    [Fact]
    public void Die_Wiederholzeilen_sind_als_solche_erkannt()
    {
        var vorschau = Vorschau();

        var tabellen = vorschau.Pages
            .SelectMany(s => s.Blocks)
            .OfType<DossierPreviewTable>()
            .Where(t => t.RepeatKey is not null)
            .ToList();

        Assert.Contains(tabellen, t => t.RepeatKey == "Aenderungen");
        Assert.Contains(tabellen, t => t.RepeatKey == "Eigentuemer");
        Assert.Contains(tabellen, t => t.RepeatKey == "Themen");

        var themen = tabellen.Single(t => t.RepeatKey == "Themen");
        Assert.Equal(new[] { "Thema", "Text" }, themen.RepeatCellKeys);
        Assert.NotNull(themen.RepeatTemplate);

        // Die Kopfzeile bleibt eine feste Zeile der Tabelle.
        var kopf = themen.Rows[0].Cells
            .Select(z => string.Concat(z.Paragraphs
                .SelectMany(a => a.Runs)
                .Where(r => !r.IsField)
                .Select(r => r.Text)).Trim())
            .ToList();

        Assert.Equal(new[] { "Thema", "Bemerkungen" }, kopf);
    }

    [Fact]
    public void Ein_fester_Text_bleibt_fester_Text()
    {
        var deckblatt = Vorschau().Pages.First();

        var feste = deckblatt.Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .SelectMany(f => f.Blocks)
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Runs)
            .Where(r => !r.IsField)
            .Select(r => r.Text!.Trim())
            .ToList();

        Assert.Contains("Eigentümerdossier", feste);
        Assert.Contains(feste, t => t.StartsWith("Datum:", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Absatz_mit_Beschriftung_und_Platzhalter_bleibt_zweiteilig()
    {
        // "Datum: {{Datum}}" ist fester Text PLUS Feld — sonst waere die
        // Beschriftung nicht mehr von ihrem Wert zu unterscheiden.
        var absatz = Vorschau().Pages
            .SelectMany(s => s.Blocks)
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating.SelectMany(f => f.Blocks).Append(p))
            .OfType<DossierPreviewParagraph>()
            .First(p => p.Runs.Any(r => r.FieldKey == "Datum")
                && p.Runs.Any(r => !r.IsField && r.Text!.Contains("Datum:", StringComparison.Ordinal)));

        Assert.Equal(2, absatz.Runs.Count);
        Assert.False(absatz.Runs[0].IsField);
        Assert.Equal("Datum", absatz.Runs[1].FieldKey);
    }

    [Fact]
    public void Die_erzeugten_Zeilen_stehen_vor_Aktennotiz_und_Rueckmeldung()
    {
        // In der Informationstabelle folgen unter der Wiederholzeile noch zwei
        // feste Zeilen. Werden die erzeugten Zeilen angehaengt statt eingesetzt,
        // rutschen Aktennotiz und Rueckmeldung darueber — die Reihenfolge im
        // Dossier waere falsch.
        var themen = Vorschau().Pages
            .SelectMany(s => s.Blocks)
            .OfType<DossierPreviewTable>()
            .Single(t => t.RepeatKey == "Themen");

        // Kopfzeile, danach die Wiederholzeile, danach die zwei festen.
        Assert.Equal(1, themen.RepeatIndex);
        Assert.Equal(3, themen.Rows.Count);

        string Zeilentext(int i) => string.Concat(themen.Rows[i].Cells
            .SelectMany(z => z.Paragraphs)
            .SelectMany(a => a.Runs)
            .Where(r => !r.IsField)
            .Select(r => r.Text));

        Assert.Contains("Thema", Zeilentext(0), StringComparison.Ordinal);
        Assert.Contains("Aktennotiz", Zeilentext(1), StringComparison.Ordinal);
        Assert.Contains("Rückmeldung", Zeilentext(2), StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Blatt_hat_die_Masse_der_Vorlage()
    {
        // A4 bei 96 dpi und die Raender aus dem Abschnitt der Vorlage. Ohne
        // diese Masse waere die Vorschau eine Nachbildung statt ein Abbild.
        var seite = Vorschau().Pages.First();

        Assert.Equal(794, seite.Geometry.WidthPx, 0);
        Assert.Equal(1123, seite.Geometry.HeightPx, 0);
        Assert.Equal(95, seite.Geometry.Margin.Left, 0);
        Assert.Equal(38, seite.Geometry.Margin.Top, 0);
        Assert.Equal(76, seite.Geometry.Margin.Right, 0);
    }

    [Fact]
    public void Die_Spaltenbreiten_stammen_aus_der_Tabelle()
    {
        var themen = Vorschau().Pages
            .SelectMany(s => s.Blocks)
            .OfType<DossierPreviewTable>()
            .Single(t => t.RepeatKey == "Themen");

        // 2333 und 6456 Twips aus dem Raster der Vorlage.
        Assert.Equal(new[] { 156, 430 }, themen.ColumnWidthsPx.Select(w => (int)Math.Round(w)));
        Assert.True(themen.IndentPx > 0, "Der Einzug der Tabelle fehlt.");
    }

    [Fact]
    public void Schrift_und_Groesse_stammen_aus_Vorlage_und_Formatvorlage()
    {
        // Die Deckblattzeilen tragen keine eigene Schriftangabe — sie erben sie
        // von der Standardvorlage. Wer die nicht liest, zeichnet Times statt
        // Arial.
        var titel = Vorschau().Pages.First().Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .SelectMany(f => f.Blocks)
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Runs)
            .First(r => r.FieldKey == "Gebietstitel");

        Assert.Equal("Arial", titel.Format.FontFamily);
        Assert.True(titel.Format.Bold);

        // 40 halbe Punkte = 20 pt = 26,67 Bildpunkte.
        Assert.Equal(26.67, titel.Format.FontSizePx, 1);
    }

    [Fact]
    public void Logo_und_Wappen_liegen_an_ihrer_Stelle_und_tragen_ihre_Bytes()
    {
        var kaesten = Vorschau().Pages.First().Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .Where(f => f.Blocks.OfType<DossierPreviewPicture>().Any())
            .ToList();

        Assert.Equal(2, kaesten.Count);

        var logo = kaesten[0];
        Assert.Equal(229, logo.WidthPx, 0);
        Assert.Equal(94, logo.HeightPx, 0);
        Assert.True(logo.LeftPx > 0, "Das Logo sitzt nicht am linken Blattrand.");
        Assert.NotEmpty(logo.Blocks.OfType<DossierPreviewPicture>().Single().Bytes);
    }

    [Fact]
    public void Die_Hoehe_eines_Kastens_zaehlt_ab_seinem_Absatz()
    {
        // Word fuehrt die senkrechte Lage eines schwebenden Objekts relativ zum
        // Absatz, an dem es haengt. Der obere Seitenrand steckt schon in der
        // Lage dieses Absatzes; wird er hier noch addiert, rutscht jeder Kasten
        // um einen ganzen Rand nach unten — und der Fussstreifen des Deckblatts
        // faellt aus dem Rahmen.
        var seite = Vorschau().Pages.First();

        var rahmen = seite.Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .First(f => f.WidthPx > 700);

        Assert.True(
            rahmen.TopPx < 1,
            $"Der Rahmen sitzt {rahmen.TopPx:0.0} Punkte unter seinem Absatz — "
            + "der Seitenrand wird doppelt gezählt.");

        // Und die Kaesten des Fussstreifens haengen alle am selben Absatz.
        var fuss = seite.Blocks
            .OfType<DossierPreviewParagraph>()
            .First(p => p.Floating.Count >= 6)
            .Floating;

        Assert.All(fuss, f => Assert.True(f.TopPx < 30, "Der Fussstreifen hängt zu tief."));
        Assert.Equal(6, fuss.Count);
    }

    [Fact]
    public void Der_Deckblattrahmen_wird_als_Umriss_gelesen()
    {
        var rahmen = Vorschau().Pages.First().Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Floating)
            .First(f => f.WidthPx > 700);

        Assert.True(rahmen.BorderWidthPx >= 1, "Der Rahmen des Deckblatts hat keine Linie.");
        Assert.Equal("000000", rahmen.BorderColorHex);
    }

    [Fact]
    public void Leere_Absaetze_bleiben_erhalten()
    {
        // Sie tragen im Dokument den senkrechten Abstand. Wer sie wegwirft,
        // schiebt das halbe Deckblatt nach oben.
        var leere = Vorschau().Pages.First().Blocks
            .OfType<DossierPreviewParagraph>()
            .Count(p => p.Runs.All(r => string.IsNullOrEmpty(r.Text) && !r.IsField));

        Assert.True(leere > 5, $"Nur {leere} leere Absätze — der Abstand des Deckblatts fehlt.");
    }

    [Fact]
    public void Ein_leerer_Pfad_wird_klar_abgewiesen()
    {
        Assert.Throws<ArgumentException>(() => DossierPreviewBuilder.Build("  "));
    }
}

public sealed class DossierPreviewNavigationTests
{
    [Fact]
    public void Seiten_werden_unter_ihrem_Kapitel_gruppiert_und_Fortsetzungen_bleiben_dort()
    {
        var pages = new[]
        {
            Seite(1, "Deckblatt", "", heading: false),
            Seite(2, "Inhaltsverzeichnis", "Inhaltsverzeichnis", heading: false, title: true),
            Seite(3, "Kapitel A", "Kapitel A", heading: true),
            Seite(4, "Seite 4", "Fortsetzung", heading: false),
            Seite(5, "Kapitel B", "Kapitel B", heading: true)
        };

        var navigation = DossierPreviewNavigation.Build(pages);

        Assert.Equal(
            new[] { "Deckblatt", "Inhaltsverzeichnis", "Kapitel A", "Kapitel A", "Kapitel B" },
            navigation.Select(item => item.ChapterTitle));
        Assert.Equal(
            new[] { "Seite 1", "Seite 2", "Seite 3", "Seite 4", "Seite 5" },
            navigation.Select(item => item.PageLabel));
        Assert.Same(pages[3], navigation[3].Page);
    }

    private static DossierPreviewPage Seite(
        int number,
        string pageTitle,
        string text,
        bool heading,
        bool title = false)
    {
        var format = DossierPreviewParagraphFormat.Default with
        {
            IsHeading = heading,
            IsTitle = title
        };

        return new DossierPreviewPage(
            number,
            pageTitle,
            new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
            new DossierPreviewBlock[]
            {
                new DossierPreviewParagraph(
                    new[] { DossierPreviewRun.Literal(text, DossierPreviewRunFormat.Default) },
                    format)
            },
            Array.Empty<string>());
    }
}
