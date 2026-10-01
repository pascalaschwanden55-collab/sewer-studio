using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Media;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Haltungs- und Schachtliste gehoeren fest ins Gesamt-PDF: direkt hinter das
/// Erklaerblatt und vor die Protokolle.
/// </summary>
public sealed class DossierComponentListInPackageTests
{
    [Fact]
    public void Paket_reiht_Dossier_Erklaerblatt_Listen_und_danach_die_Protokolle()
    {
        using var temp = new TempDirectory();
        var attachment = Path.Combine(temp.Path, "01_Originalprotokoll.pdf");
        File.WriteAllBytes(attachment, CreatePdf("ORIGINALPROTOKOLL"));

        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var result = composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [CreatePdf("HALTUNGSLISTE"), CreatePdf("SCHACHTLISTE")],
            [attachment],
            temp.Path,
            out var pflichtseiten);

        using var document = PdfDocument.Open(result);
        Assert.Equal(5, document.NumberOfPages);
        Assert.Contains("WORD-DOSSIER", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Zustandsklassen", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("HALTUNGSLISTE", document.GetPage(3).Text, StringComparison.Ordinal);
        Assert.Contains("SCHACHTLISTE", document.GetPage(4).Text, StringComparison.Ordinal);
        Assert.Contains("ORIGINALPROTOKOLL", document.GetPage(5).Text, StringComparison.Ordinal);
        Assert.Equal([2, 3, 4], pflichtseiten.OrderBy(seite => seite));
    }

    [Fact]
    public void Paket_meldet_auch_mehrseitige_Listen_vollstaendig_als_Pflichtblaetter()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var result = composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [CreateTwoPagePdf("HALTUNGEN SEITE 1", "HALTUNGEN SEITE 2")],
            [],
            temp.Path,
            out var pflichtseiten);

        using var document = PdfDocument.Open(result);
        Assert.Equal(4, document.NumberOfPages);
        Assert.Equal([2, 3, 4], pflichtseiten.OrderBy(seite => seite));
    }

    [Fact]
    public void Paket_ohne_Listen_bleibt_unveraendert()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var result = composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [],
            [],
            temp.Path,
            out var pflichtseiten);

        using var document = PdfDocument.Open(result);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Equal([2], pflichtseiten.OrderBy(seite => seite));
    }

    [Fact]
    public void Paket_laesst_keine_temporaere_Listendatei_zurueck()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [CreatePdf("HALTUNGSLISTE"), CreatePdf("SCHACHTLISTE")],
            [],
            temp.Path,
            out _);

        Assert.Empty(Directory.EnumerateFiles(
            temp.Path,
            DossierPdfPackageComposer.TemporaryFilePrefix + "*.pdf",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Paket_stoppt_wenn_eine_Liste_keine_lesbare_PDF_ist()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var fehler = Assert.Throws<InvalidOperationException>(() => composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [[1, 2, 3]],
            [],
            temp.Path,
            out _));

        Assert.Contains("Bauteilliste", fehler.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(
            temp.Path,
            DossierPdfPackageComposer.TemporaryFilePrefix + "*.pdf",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Paket_stoppt_wenn_eine_angeforderte_Beilage_fehlt()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var error = Assert.Throws<InvalidOperationException>(() => composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [],
            [Path.Combine(temp.Path, "fehlende-beilage.pdf")],
            temp.Path,
            out _));

        Assert.Contains("Beilage fehlt", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreatePdf(string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text(text);
            });
        }).GeneratePdf();
    }

    private static byte[] CreateTwoPagePdf(string first, string second)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text(first);
            });
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text(second);
            });
        }).GeneratePdf();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio_DossierListPackageTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
