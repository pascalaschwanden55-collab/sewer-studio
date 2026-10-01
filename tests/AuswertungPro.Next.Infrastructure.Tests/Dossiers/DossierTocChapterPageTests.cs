using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Seitenzahl einer Kapitelzeile im Inhaltsverzeichnis.
///
/// Sie ist ein Word-Feld (PAGEREF) und deshalb bisher gesperrt, waehrend die
/// Beilagenzeilen darunter eine frei aenderbare Seitenzahl haben. Genau diese
/// Ungleichheit ist der Gegenstand: eine eigene Angabe ersetzt das Feld durch
/// Text, eine fehlende laesst Word weiterrechnen.
/// </summary>
public sealed class DossierTocChapterPageTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), "toc_seite_" + Guid.NewGuid().ToString("N"));

    private readonly string _datei;

    public DossierTocChapterPageTests()
    {
        Directory.CreateDirectory(_ordner);
        _datei = Path.Combine(_ordner, "Eigentuemerdossier.docx");

        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        File.Copy(
            Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName),
            _datei);
    }

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch { }
    }

    private static (string Nummer, string Titel, string Seite, bool IstFeld) Zeile(
        WordprocessingDocument document, string titel)
    {
        var absatz = document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .First(p => p.InnerText.Contains(titel, StringComparison.Ordinal)
                && (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "")
                    .StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase));

        var texte = absatz.Descendants<Text>().Select(t => t.Text).ToList();
        var istFeld = absatz.Descendants<FieldCode>()
            .Any(code => code.Text.Contains("PAGEREF", StringComparison.OrdinalIgnoreCase));

        return (texte.FirstOrDefault() ?? "", titel, texte.LastOrDefault() ?? "", istFeld);
    }

    [Fact]
    public void Ohne_eigene_Angabe_bleibt_die_Seitenzahl_ein_Word_Feld()
    {
        using var document = WordprocessingDocument.Open(_datei, true);

        var geaendert = DocxTocPageEditor.Apply(document, new Dictionary<string, string>());

        Assert.Equal(0, geaendert);
        Assert.True(Zeile(document, "Übersichtsplan Werkleitungen").IstFeld);
    }

    [Fact]
    public void Eine_eigene_Angabe_ersetzt_das_Feld_durch_Text()
    {
        using var document = WordprocessingDocument.Open(_datei, true);

        var geaendert = DocxTocPageEditor.Apply(
            document,
            new Dictionary<string, string> { ["Übersichtsplan Werkleitungen"] = "7" });

        Assert.Equal(1, geaendert);

        var zeile = Zeile(document, "Übersichtsplan Werkleitungen");
        Assert.False(zeile.IstFeld);
        Assert.Equal("7", zeile.Seite);
    }

    [Fact]
    public void Die_uebrigen_Kapitel_bleiben_unberuehrt()
    {
        using var document = WordprocessingDocument.Open(_datei, true);

        DocxTocPageEditor.Apply(
            document,
            new Dictionary<string, string> { ["Übersichtsplan Werkleitungen"] = "7" });

        Assert.True(Zeile(document, "Eigentumsverhältnisse").IstFeld);
        Assert.True(Zeile(document, "Informationen Sanierung").IstFeld);
    }

    [Fact]
    public void Eine_geleerte_Angabe_nimmt_die_Seitenzahl_ganz_weg()
    {
        // Wer die Zahl bewusst loescht, will keine — und schon gar nicht die
        // alte Feldrechnung zurueck.
        using var document = WordprocessingDocument.Open(_datei, true);

        DocxTocPageEditor.Apply(
            document,
            new Dictionary<string, string> { ["Eigentumsverhältnisse"] = "" });

        var zeile = Zeile(document, "Eigentumsverhältnisse");
        Assert.False(zeile.IstFeld);
        Assert.Equal("Eigentumsverhältnisse", zeile.Seite);
    }

    [Fact]
    public void Nummer_und_Titel_bleiben_stehen()
    {
        using var document = WordprocessingDocument.Open(_datei, true);

        DocxTocPageEditor.Apply(
            document,
            new Dictionary<string, string> { ["Informationen Sanierung"] = "9" });

        var absatz = document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .First(p => p.InnerText.Contains("Informationen Sanierung", StringComparison.Ordinal)
                && (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "")
                    .StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase));

        Assert.StartsWith("3.", absatz.InnerText, StringComparison.Ordinal);
        Assert.Contains("Informationen Sanierung", absatz.InnerText, StringComparison.Ordinal);
        Assert.EndsWith("9", absatz.InnerText, StringComparison.Ordinal);

        // Die zwei Tabulatoren tragen Einzug und Punktlinie.
        Assert.Equal(2, absatz.Descendants<TabChar>().Count());
    }

    [Fact]
    public void Die_neue_Seitenzahl_traegt_die_Formatierung_der_alten()
    {
        // Sonst stuende sie fett oder in anderer Groesse da als die Zeilen daneben.
        using var document = WordprocessingDocument.Open(_datei, true);

        var vorher = SeitenLaufFormat(document, "Eigentumsverhältnisse");

        DocxTocPageEditor.Apply(
            document,
            new Dictionary<string, string> { ["Eigentumsverhältnisse"] = "7" });

        Assert.Equal(vorher, SeitenLaufFormat(document, "Eigentumsverhältnisse"));
    }

    private static string SeitenLaufFormat(WordprocessingDocument document, string titel)
    {
        var absatz = document.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .First(p => p.InnerText.Contains(titel, StringComparison.Ordinal)
                && (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "")
                    .StartsWith("Verzeichnis", StringComparison.OrdinalIgnoreCase));

        var lauf = absatz.Descendants<Run>()
            .Last(run => run.Descendants<Text>().Any());

        return lauf.RunProperties?.OuterXml ?? "(keine)";
    }
}

/// <summary>
/// Die Verzeichniszeilen im Zusammenspiel: alle fuenf gleich bearbeitbar.
/// </summary>
public sealed class DossierTocGleichbehandlungTests
{
    [Fact]
    public void Das_Dossier_fuehrt_eigene_Seitenzahlen_fuer_die_Kapitel()
    {
        var dossier = new AuswertungPro.Next.Domain.Models.Dossiers.DossierDefinition();

        Assert.NotNull(dossier.TocChapterPages);
        Assert.Empty(dossier.TocChapterPages);
    }

    [Fact]
    public void Eine_alte_Datei_ohne_das_Feld_stuerzt_nicht_ab()
    {
        var dokument = new AuswertungPro.Next.Domain.Models.Dossiers.DossierDocument
        {
            SchemaVersion = 8,
            Dossiers =
            {
                new AuswertungPro.Next.Domain.Models.Dossiers.DossierDefinition
                {
                    Name = "Liegenschaft Nr. 439 Dittli",
                    TocChapterPages = null!
                }
            }
        };

        var dossier = AuswertungPro.Next.Application.Dossiers.DossierDocumentMigration
            .MigrateToCurrent(dokument).Dossiers[0];

        Assert.NotNull(dossier.TocChapterPages);
        Assert.Equal(9, dokument.SchemaVersion);
    }

    [Fact]
    public void Der_Export_setzt_die_eigene_Seitenzahl()
    {
        var quelle = File.ReadAllText(Path.Combine(
            new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
                .Locate(AppContext.BaseDirectory)!,
            "src", "AuswertungPro.Next.Infrastructure", "Dossiers",
            "DossierWordTemplateExportService.cs"));

        Assert.Contains(
            "DocxTocPageEditor.Apply(document, request.Dossier.TocChapterPages)",
            quelle,
            StringComparison.Ordinal);
    }
}
