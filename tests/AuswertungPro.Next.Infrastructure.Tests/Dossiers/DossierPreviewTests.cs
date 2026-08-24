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
        Assert.Contains("Betroffene Leitungen", titel);
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
    public void Der_Uebersichtsplan_ist_eine_Bildstelle_und_kein_Text()
    {
        var seite = Vorschau().Pages
            .Single(s => s.Title == "Übersichtsplan Werkleitungen");

        var bild = Assert.Single(seite.Blocks.OfType<DossierPreviewImage>());
        Assert.Equal("Uebersichtsplan", bild.FieldKey);
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
        Assert.Equal(new[] { "Thema", "Bemerkungen" }, themen.HeaderCells);
        Assert.Equal(new[] { "Thema", "Text" }, themen.RepeatCellKeys);
    }

    [Fact]
    public void Ein_fester_Text_bleibt_fester_Text()
    {
        var deckblatt = Vorschau().Pages.First();

        var feste = deckblatt.Blocks
            .OfType<DossierPreviewParagraph>()
            .SelectMany(p => p.Runs)
            .Where(r => !r.IsField)
            .Select(r => r.Text!.Trim())
            .ToList();

        Assert.Contains("Eigentümerdossier", feste);
        Assert.Contains(feste, t => t.StartsWith("Datum:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Datum: {{Datum}}", 2)]
    [InlineData("{{Gebietstitel}}", 1)]
    [InlineData("Ganz ohne Platzhalter", 1)]
    [InlineData("{{#Themen}}{{Thema}}", 1)]
    public void Zerlegt_Text_in_feste_Stuecke_und_Felder(string text, int erwartet)
    {
        // Die Wiederholmarke gehoert zur Tabelle und ist kein Feld.
        Assert.Equal(erwartet, DossierPreviewBuilder.Zerlege(text).Count);
    }

    [Fact]
    public void Ein_leerer_Pfad_wird_klar_abgewiesen()
    {
        Assert.Throws<ArgumentException>(() => DossierPreviewBuilder.Build("  "));
    }
}
