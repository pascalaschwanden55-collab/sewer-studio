using System;
using System.IO;
using System.Linq;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DocxChapterRemoverTests
{
    private static string VorlagenPfad()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var pfad = Path.Combine(wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName);
        Assert.True(File.Exists(pfad), $"'{pfad}' fehlt.");
        return pfad;
    }

    private static MemoryStream Kopie()
    {
        var strom = new MemoryStream();
        using (var datei = File.OpenRead(VorlagenPfad()))
            datei.CopyTo(strom);

        strom.Position = 0;
        return strom;
    }

    private static string Text(MemoryStream strom)
    {
        strom.Position = 0;
        using var document = WordprocessingDocument.Open(strom, false);

        return string.Concat(document.MainDocumentPart!.Document.Body!
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
            .Select(t => t.Text));
    }

    [Fact]
    public void Die_Vorlage_nennt_ihre_Kapitel()
    {
        using var strom = Kopie();
        using var document = WordprocessingDocument.Open(strom, false);

        var kapitel = DocxChapterRemover.Chapters(document);

        Assert.Contains("Übersichtsplan Werkleitungen", kapitel);
        Assert.Contains("Eigentumsverhältnisse", kapitel);
        Assert.Contains("Informationen Sanierung", kapitel);
    }

    [Fact]
    public void Ein_weggelassenes_Kapitel_verschwindet_samt_Inhalt_und_Verzeichniszeile()
    {
        using var strom = Kopie();

        using (var document = WordprocessingDocument.Open(strom, true))
        {
            Assert.True(DocxChapterRemover.Remove(document, "Übersichtsplan Werkleitungen"));
            document.MainDocumentPart!.Document.Save();
        }

        var text = Text(strom);

        Assert.DoesNotContain("Übersichtsplan Werkleitungen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{{@Uebersichtsplan}}", text, StringComparison.Ordinal);

        // Die uebrigen Kapitel bleiben stehen.
        Assert.Contains("Eigentumsverhältnisse", text, StringComparison.Ordinal);
        Assert.Contains("Informationen Sanierung", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_unbekanntes_Kapitel_raeumt_nichts_ab()
    {
        // Lieber ein Kapitel zu viel als ein halb abgeraeumtes Dokument.
        using var strom = Kopie();
        var vorher = Text(strom);

        using (var document = WordprocessingDocument.Open(strom, true))
        {
            Assert.False(DocxChapterRemover.Remove(document, "Gibt es nicht"));
            document.MainDocumentPart!.Document.Save();
        }

        Assert.Equal(vorher, Text(strom));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ohne_Titel_geschieht_nichts(string? titel)
    {
        using var strom = Kopie();
        using var document = WordprocessingDocument.Open(strom, true);

        Assert.False(DocxChapterRemover.Remove(document, titel));
    }

    [Fact]
    public void Das_letzte_Kapitel_laesst_den_Abschnitt_der_Seite_stehen()
    {
        // Die Abschnittsangaben tragen Blattformat und Raender — ohne sie
        // waere das Dokument kaputt.
        using var strom = Kopie();

        using (var document = WordprocessingDocument.Open(strom, true))
        {
            Assert.True(DocxChapterRemover.Remove(document, "Informationen Sanierung"));
            document.MainDocumentPart!.Document.Save();
        }

        strom.Position = 0;
        using var wieder = WordprocessingDocument.Open(strom, false);

        Assert.NotEmpty(wieder.MainDocumentPart!.Document.Body!
            .Elements<SectionProperties>());
    }
}
