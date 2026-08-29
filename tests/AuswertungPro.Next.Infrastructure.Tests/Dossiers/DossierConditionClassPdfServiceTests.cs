using System.Text;
using System.Security.Cryptography;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Media;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierConditionClassPdfServiceTests
{
    [Fact]
    public void Definitionen_verwenden_die_fuenf_verbindlichen_Zustandsklassen()
    {
        Assert.Equal(1, DossierConditionClassDefinitions.PdfRequiredPageCount);
        Assert.Equal(
            ["Z0", "Z1", "Z2", "Z3", "Z4"],
            DossierConditionClassDefinitions.All.Select(definition => definition.Code));

        Assert.Collection(
            DossierConditionClassDefinitions.All,
            z0 =>
            {
                Assert.Equal("Nicht mehr funktionstüchtig", z0.Name);
                Assert.Equal("Sofort (innerhalb eines Jahres)", z0.Orientation);
                Assert.Contains("nicht mehr durchgängig", z0.Description, StringComparison.Ordinal);
            },
            z1 =>
            {
                Assert.Equal("Starke Defizite", z1.Name);
                Assert.Equal(
                    "Kurzfristig (innerhalb der nächsten 3 Jahre)",
                    z1.Orientation);
                Assert.Contains("statische Sicherheit, Hydraulik oder Dichtheit", z1.Description, StringComparison.Ordinal);
            },
            z2 =>
            {
                Assert.Equal("Mittlere Defizite", z2.Name);
                Assert.Equal(
                    "Mittelfristig (innerhalb der nächsten 8 Jahre)",
                    z2.Orientation);
                Assert.Contains("leichte Abflusshindernisse", z2.Description, StringComparison.Ordinal);
            },
            z3 =>
            {
                Assert.Equal("Leichte Defizite", z3.Name);
                Assert.Equal("Langfristig (mehr als 8 Jahre)", z3.Orientation);
                Assert.Contains("unbedeutenden Einfluss", z3.Description, StringComparison.Ordinal);
            },
            z4 =>
            {
                Assert.Equal("Keine Defizite", z4.Name);
                Assert.Equal("Keine relevanten Defizite festgestellt.", z4.Description);
                Assert.Equal(
                    "Keine Sanierungsmassnahmen bis zur nächsten Zustandserfassung "
                    + "und Zustandsbeurteilung erforderlich",
                    z4.Orientation);
            });
    }

    [Theory]
    [InlineData("0", "#FF0000", "#FFFFFF")]
    [InlineData("1", "#FF6600", "#FFFFFF")]
    [InlineData("2", "#FFFF00", "#1F2937")]
    [InlineData("3", "#AEB135", "#1F2937")]
    [InlineData("4", "#92D050", "#1F2937")]
    public void Pdf_verwendet_die_verbindliche_Berichtspalette(
        string value,
        string expectedBackground,
        string expectedForeground)
    {
        Assert.Equal(
            (expectedBackground, expectedForeground),
            DossierConditionClassPdfService.ResolveColors(value));
    }

    [Fact]
    public void CreatePdf_erzeugt_eine_lesbare_A4_Seite_mit_Klassen_und_Fristen()
    {
        var service = new DossierConditionClassPdfService(
            templateAssetFolder: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var pdf = service.CreatePdf();

        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        using var document = PdfDocument.Open(pdf);
        var pages = document.GetPages().ToList();
        Assert.Single(pages);
        Assert.All(pages, page =>
        {
            Assert.InRange(page.Width, 594, 596);
            Assert.InRange(page.Height, 841, 843);
        });

        var conditionText = string.Join(" ", pages[0].GetWords().Select(word => word.Text));
        Assert.Contains("Zustandsklassen Z0 bis Z4", conditionText, StringComparison.Ordinal);
        Assert.Contains("Z0", conditionText, StringComparison.Ordinal);
        Assert.Contains("Z1", conditionText, StringComparison.Ordinal);
        Assert.Contains("Z2", conditionText, StringComparison.Ordinal);
        Assert.Contains("Z3", conditionText, StringComparison.Ordinal);
        Assert.Contains("Z4", conditionText, StringComparison.Ordinal);
        Assert.Contains("Nicht mehr funktionstüchtig", conditionText, StringComparison.Ordinal);
        Assert.Contains("nicht mehr durchgängig", conditionText, StringComparison.Ordinal);
        Assert.Contains("Abflusshindernisse bestehen", conditionText, StringComparison.Ordinal);
        Assert.Contains("Starke Defizite", conditionText, StringComparison.Ordinal);
        Assert.Contains("statische Sicherheit", conditionText, StringComparison.Ordinal);
        Assert.Contains("stark ausgewaschene Rohrwandung", conditionText, StringComparison.Ordinal);
        Assert.Contains("Mittlere Defizite", conditionText, StringComparison.Ordinal);
        Assert.Contains("leichte Abflusshindernisse", conditionText, StringComparison.Ordinal);
        Assert.Contains("ausgewaschen usw.", conditionText, StringComparison.Ordinal);
        Assert.Contains("Leichte Defizite", conditionText, StringComparison.Ordinal);
        Assert.Contains("unbedeutenden Einfluss", conditionText, StringComparison.Ordinal);
        Assert.Contains("Auswaschungen der Rohrwandung", conditionText, StringComparison.Ordinal);
        Assert.Contains("Keine Defizite", conditionText, StringComparison.Ordinal);
        Assert.Contains("Keine relevanten Defizite festgestellt", conditionText, StringComparison.Ordinal);
        Assert.Contains("vollständige Bauwerksdaten", conditionText, StringComparison.Ordinal);
        Assert.Contains("Ausmass und Lage", conditionText, StringComparison.Ordinal);
        Assert.Contains("qualifizierte Fachperson", conditionText, StringComparison.Ordinal);
        Assert.Contains("Schutzbereiche", conditionText, StringComparison.Ordinal);
        Assert.Contains("Nutzung", conditionText, StringComparison.Ordinal);
        Assert.Contains("Grundwasserlage", conditionText, StringComparison.Ordinal);
        Assert.Contains("Netzbedeutung", conditionText, StringComparison.Ordinal);
        Assert.Contains("Sanierungsdringlichkeit", conditionText, StringComparison.Ordinal);
        Assert.Contains("keine feste Zuordnung", conditionText, StringComparison.Ordinal);
        Assert.Contains("nicht die Zustandsklasse", conditionText, StringComparison.Ordinal);
        Assert.Contains("Zeitraum (Orientierung)", conditionText, StringComparison.Ordinal);
        Assert.Contains("Sofort", conditionText, StringComparison.Ordinal);
        Assert.Contains("innerhalb eines Jahres", conditionText, StringComparison.Ordinal);
        Assert.Contains("Kurzfristig", conditionText, StringComparison.Ordinal);
        Assert.Contains("Mittelfristig", conditionText, StringComparison.Ordinal);
        Assert.Contains("Langfristig", conditionText, StringComparison.Ordinal);
        Assert.Contains("Keine Sanierungsmassnahmen", conditionText, StringComparison.Ordinal);
        Assert.DoesNotContain("350-400", conditionText, StringComparison.Ordinal);
        Assert.DoesNotContain("NULL", conditionText, StringComparison.Ordinal);
        Assert.Contains("Kapitel 2.2-2.3", conditionText, StringComparison.Ordinal);
        Assert.Contains("PDF-Seite 12", conditionText, StringComparison.Ordinal);
        Assert.Contains("keine Berechnung", conditionText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht mit Z4", conditionText, StringComparison.Ordinal);

        Assert.All(pages, page => Assert.Contains(
            DossierConditionClassDefinitions.PdfRequiredPageMarker,
            string.Join(" ", page.GetWords().Select(word => word.Text)),
            StringComparison.Ordinal));
    }

    [Fact]
    public void Gespeicherte_Auslieferungsvorlage_bleibt_bytegleich_und_einseitig()
    {
        var path = TestRepoPaths.RepoFile(
            "Export_Vorlage",
            DossierFolderPlanner.ConditionClassPdfFileName);
        var expected = File.ReadAllBytes(path);
        var service = new DossierConditionClassPdfTemplateService(path);

        var actual = service.CreatePdf();

        Assert.Equal(expected, actual);
        Assert.Equal(
            "58BBADBE64F7609C32A9EA06770D609CC828536EE059A3940E70BB213B947D3B",
            Convert.ToHexString(SHA256.HashData(actual)));
        using var document = PdfDocument.Open(actual);
        var page = Assert.Single(document.GetPages());
        var text = string.Join(" ", page.GetWords().Select(word => word.Text));
        Assert.Contains("Zustandsklassen", text, StringComparison.Ordinal);
        Assert.Contains(
            DossierConditionClassDefinitions.PdfRequiredPageMarker,
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Paket_reiht_Dossier_Erklaerblatt_und_Protokoll_in_dieser_Reihenfolge()
    {
        using var temp = new TempDirectory();
        var attachment = Path.Combine(temp.Path, "01_Originalprotokoll.pdf");
        File.WriteAllBytes(attachment, CreatePdf("ORIGINALPROTOKOLL"));

        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: temp.Path));

        var result = composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [attachment],
            temp.Path);

        using var document = PdfDocument.Open(result);
        Assert.Equal(3, document.NumberOfPages);
        Assert.Contains("WORD-DOSSIER", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Zustandsklassen", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("Sofort", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("ORIGINALPROTOKOLL", document.GetPage(3).Text, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(
            temp.Path,
            DossierPdfPackageComposer.TemporaryFilePrefix + "*.pdf",
            SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Paket_stoppt_wenn_das_Pflichtblatt_keine_gueltige_PDF_ist()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new FixedConditionClassPdfService([1, 2, 3]));

        var error = Assert.Throws<InvalidOperationException>(() => composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [],
            temp.Path));

        Assert.Contains("Erkläranhang", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.pdf", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Paket_stoppt_wenn_der_Pflichtanhang_mehr_als_eine_Seite_hat()
    {
        using var temp = new TempDirectory();
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new FixedConditionClassPdfService(CreateTwoPagePdf(
                "ERSTE SEITE",
                "ZWEITE SEITE")));

        var error = Assert.Throws<InvalidOperationException>(() => composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [],
            temp.Path));

        Assert.Contains("genau eine PDF-Seite", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.pdf", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Paket_loescht_das_temporaere_Pflichtblatt_auch_nach_einem_Mergefehler()
    {
        using var temp = new TempDirectory();
        var merge = new ThrowingMergeService();
        var composer = new DossierPdfPackageComposer(
            merge,
            new FixedConditionClassPdfService(CreatePdf("ZUSTANDSKLASSEN")));

        Assert.Throws<IOException>(() => composer.Compose(
            CreatePdf("WORD-DOSSIER"),
            [],
            temp.Path));

        Assert.True(merge.ExplanationExistedWhenCalled);
        Assert.NotNull(merge.ExplanationPath);
        Assert.False(File.Exists(merge.ExplanationPath));
        Assert.Empty(Directory.EnumerateFiles(
            temp.Path,
            DossierPdfPackageComposer.TemporaryFilePrefix + "*.pdf",
            SearchOption.TopDirectoryOnly));
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

    private sealed class FixedConditionClassPdfService(byte[] pdf)
        : IDossierConditionClassPdfService
    {
        public byte[] CreatePdf() => pdf;
    }

    private sealed class ThrowingMergeService : IPdfMergeService
    {
        public string? ExplanationPath { get; private set; }

        public bool ExplanationExistedWhenCalled { get; private set; }

        public byte[] MergeWithOriginals(
            byte[] generatedPdf,
            IReadOnlyList<string> originalPdfPaths)
        {
            _ = generatedPdf;
            ExplanationPath = Assert.Single(originalPdfPaths);
            ExplanationExistedWhenCalled = File.Exists(ExplanationPath);
            throw new IOException("Absichtlicher Mergefehler im Test.");
        }

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
            => throw new NotSupportedException();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio_DossierConditionPdfTests_" + Guid.NewGuid().ToString("N"));
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
