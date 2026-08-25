using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Eine Beschriftung, die mit ihrem Platzhalter im selben Absatz steht.
///
/// „Datum: {{Datum}}" ist EIN Textlauf. Der Textersetzer liess solche Absätze
/// bewusst aus — eine Zeile mit Platzhalter gehörte dem Feld —, und die
/// Vorschau bot sie deshalb gar nicht erst an. Gemessen an der echten Vorlage
/// waren das fünf Beschriftungen: „Datum:", „Revision:", „Proj. Nr. AWU  :",
/// „Erstellungsdatum:" und „Autoren:". Sie sind der Rest, der von „jeder Text
/// bearbeitbar" noch fehlte.
/// </summary>
public sealed class DossierMixedLiteralTests
{
    [Theory]
    [InlineData("Datum: {{Datum}}", "Datum:")]
    [InlineData("Proj. Nr. AWU  : {{Projekt_Nr}}", "Proj. Nr. AWU  :")]
    [InlineData("{{Autoren}} als Verfasser", "als Verfasser")]
    public void Die_Beschriftung_neben_einem_Platzhalter_ist_der_Schluessel(
        string absatz, string erwartet)
    {
        Assert.Equal(erwartet, DossierMixedParagraphLiteral.Schluessel(absatz));
    }

    [Fact]
    public void Ein_Absatz_aus_reinem_Platzhalter_hat_keine_Beschriftung()
    {
        Assert.Null(DossierMixedParagraphLiteral.Schluessel("{{Autoren}}"));
        Assert.Null(DossierMixedParagraphLiteral.Schluessel("  {{Autoren}}  "));
    }

    [Fact]
    public void Ohne_Platzhalter_ist_der_alte_Weg_zustaendig()
    {
        // Solche Absaetze ersetzt der Textersetzer laengst als Ganzes.
        Assert.Null(DossierMixedParagraphLiteral.Schluessel("Eigentumsverhältnisse"));
    }

    [Fact]
    public void Zwei_getrennte_Textstuecke_bleiben_gesperrt()
    {
        // „Von {{A}} bis {{B}}" haette zwei Stellen. Welche gemeint ist, laesst
        // sich nicht entscheiden — also lieber gar nicht anbieten.
        Assert.Null(DossierMixedParagraphLiteral.Schluessel("Von {{A}} bis {{B}}"));
    }

    [Fact]
    public void Der_Bereich_zeigt_auf_den_getrimmten_Text()
    {
        var bereich = DossierMixedParagraphLiteral.Bereich("Datum: {{Datum}}");

        Assert.NotNull(bereich);
        Assert.Equal(0, bereich!.Value.Start);
        Assert.Equal("Datum:".Length, bereich.Value.Length);
    }

    [Fact]
    public void Die_Vorschau_bietet_die_fuenf_Beschriftungen_an()
    {
        var dokument = DossierPreviewBuilder.Build(Vorlage());

        var angeboten = dokument.Pages
            .SelectMany(DossierPreviewTextInventory.Literals)
            .ToList();

        foreach (var beschriftung in new[]
                 {
                     "Datum:", "Revision:", "Proj. Nr. AWU  :",
                     "Erstellungsdatum:", "Autoren:"
                 })
        {
            Assert.Contains(beschriftung, angeboten);
        }
    }

    [Fact]
    public void Eine_eigene_Beschriftung_laesst_den_Platzhalter_stehen()
    {
        using var datei = new Kopie();
        using var document = WordprocessingDocument.Open(datei.Pfad, true);

        var geaendert = DocxLiteralTextReplacer.Apply(
            document, new Dictionary<string, string> { ["Datum:"] = "Erfasst am:" });

        Assert.True(geaendert > 0);

        var absatz = Absatz(document, "{{Datum}}");
        Assert.Equal("Erfasst am: {{Datum}}", absatz);
    }

    [Fact]
    public void Eine_geleerte_Beschriftung_nimmt_den_Platzhalter_nicht_mit()
    {
        // Bei einem reinen Textabsatz heisst leer „Zeile weg". Hier haenge am
        // selben Absatz aber das Datumsfeld — es zu loeschen waere ein
        // Datenverlust, den niemand bestellt hat.
        using var datei = new Kopie();
        using var document = WordprocessingDocument.Open(datei.Pfad, true);

        DocxLiteralTextReplacer.Apply(
            document, new Dictionary<string, string> { ["Autoren:"] = "" });

        var absatz = Absatz(document, "{{Autoren}}");
        Assert.Equal("{{Autoren}}", absatz.Trim());
    }

    [Fact]
    public void Der_Platzhalter_wird_danach_weiterhin_gefuellt()
    {
        using var datei = new Kopie();
        using var document = WordprocessingDocument.Open(datei.Pfad, true);

        DocxLiteralTextReplacer.Apply(
            document, new Dictionary<string, string> { ["Datum:"] = "Erfasst am:" });

        DocxPlaceholderFiller.Fill(
            document, new Dictionary<string, string> { ["Datum"] = "24.08.2026" });

        Assert.Equal("Erfasst am: 24.08.2026", Absatz(document, "24.08.2026"));
    }

    private static string Vorlage()
        => Path.Combine(
            new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
                .Locate(AppContext.BaseDirectory)!,
            "Export_Vorlage",
            DossierWordTemplate.TemplateFileName);

    private static string Absatz(WordprocessingDocument document, string teil)
        => document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .Where(p => !p.Descendants<Paragraph>().Any())
            .First(p => p.InnerText.Contains(teil, StringComparison.Ordinal))
            .InnerText;

    private sealed class Kopie : IDisposable
    {
        private readonly string _ordner = Path.Combine(
            Path.GetTempPath(), "dossier_misch_" + Guid.NewGuid().ToString("N"));

        public Kopie()
        {
            Directory.CreateDirectory(_ordner);
            Pfad = Path.Combine(_ordner, DossierWordTemplate.TemplateFileName);
            File.Copy(Vorlage(), Pfad);
        }

        public string Pfad { get; }

        public void Dispose()
        {
            try { Directory.Delete(_ordner, recursive: true); } catch { }
        }
    }
}
