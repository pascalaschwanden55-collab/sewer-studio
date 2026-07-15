using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DistributionPdfAssignmentControllerTests
{
    [Fact]
    public void ReadPages_liefert_alle_PDF_Seiten_mit_Quelle_und_Reihenfolge()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "seiten.pdf");
        WritePdf(pdfPath, "Erste Seite", "Zweite Seite");

        var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);

        Assert.Equal(2, pages.Count);
        Assert.Collection(
            pages,
            first =>
            {
                Assert.Equal(1, first.PageNumber);
                Assert.Contains("Erste Seite", first.Text);
                Assert.Equal(pdfPath, first.SourcePath);
            },
            second =>
            {
                Assert.Equal(2, second.PageNumber);
                Assert.Contains("Zweite Seite", second.Text);
                Assert.Equal(pdfPath, second.SourcePath);
            });
    }

    [Fact]
    public void ReadPages_BildPdfOhneText_ErhaeltLeereSeitenFuerOcr()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "bildseiten.pdf");
        WriteEmptyPdf(pdfPath, pageCount: 3);

        var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);

        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { 1, 2, 3 }, pages.Select(page => page.PageNumber));
        Assert.All(pages, page =>
        {
            Assert.Equal("", page.Text);
            Assert.Equal(pdfPath, page.SourcePath);
        });
    }

    [Fact]
    public void PageReadingService_verwendet_injizierten_Textleser()
    {
        var reader = new DistributionPdfPageReadingService(
            new FixedTextExtractor(["  Erste Seite\r\nZeile 2  ", "Zweite Seite"]),
            new UnexpectedFileSafetyChecker());

        var pages = reader.ReadPages("virtuell.pdf");

        Assert.Collection(
            pages,
            first =>
            {
                Assert.Equal(1, first.PageNumber);
                Assert.Equal("Erste Seite\nZeile 2", first.Text);
                Assert.Equal("virtuell.pdf", first.SourcePath);
            },
            second => Assert.Equal("Zweite Seite", second.Text));
    }

    [Fact]
    public void ExtractDichtheitPerPage_ordnet_Kontrollseite_und_Projekt_Richtung_zu()
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "6927-6928", FieldSource.Manual, userEdited: false);
        project.AddRecord(record);
        var pages = new[]
        {
            new DistributionPdfPage(
                1,
                "Prufgegenstand / Haltung 6928 -> 6927\nDatum 2026/07/12",
                "dichtheit.pdf"),
            new DistributionPdfPage(2, "Kontrollinformation", "dichtheit.pdf")
        };

        var assignments = DistributionPdfAssignmentController.ExtractDichtheitPerPage(
            pages,
            project,
            Path.GetTempPath());

        var assignment = Assert.Single(assignments);
        Assert.Equal("6927-6928", assignment.HaltungId);
        Assert.Equal("20260712", assignment.DateStamp);
        Assert.Equal([1, 2], assignment.PageNumbers);
        Assert.False(assignment.IsSchacht);
    }

    [Fact]
    public void ExtractPhotoHints_bevorzugt_beschriftete_Fotos_und_normalisiert_Schluessel()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "fotos.pdf");
        WritePdf(
            pdfPath,
            "999_888_777_B.jpg",
            "Foto: 001_02_0003_a.jpg");

        var keys = DistributionPdfAssignmentController.ExtractPhotoHints(pdfPath);

        Assert.Contains("001_02_0003_a.jpg", keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("1_2_3_A", keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("999_888_777_B.jpg", keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchPdfToHolding_findet_Haltung_auch_ohne_Knotenpraefixe()
    {
        var expectedFolder = Path.Combine("Ziel", "07.7695-07.7078");
        var distributed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["07.7695-07.7078"] = expectedFolder
        };

        var result = DistributionPdfAssignmentController.MatchPdfToHolding(
            Path.Combine("Quelle", "7695-7078_Dichtheit.pdf"),
            distributed);

        Assert.Equal(expectedFolder, result);
    }

    private static void WritePdf(string path, params string[] pageTexts)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pageTexts)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText(text, 12, new PdfPoint(40, 780), font);
        }
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteEmptyPdf(string path, int pageCount)
    {
        using var builder = new PdfDocumentBuilder();
        for (var pageNumber = 0; pageNumber < pageCount; pageNumber++)
            builder.AddPage(PageSize.A4);
        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-pdf-assignment-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class FixedTextExtractor(IReadOnlyList<string> pages) : IPdfTextExtractor
    {
        public string FindPdfToTextPath(string? explicitPath = null) => "virtuell";

        public PdfTextExtractionResult ExtractPages(
            string pdfPath,
            string? explicitPdfToTextPath = null) =>
            new(pages, string.Join("\n", pages));

        public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
        {
        }
    }

    private sealed class UnexpectedFileSafetyChecker : IPdfFileSafetyChecker
    {
        public long ResolveMaxBytes() => throw new InvalidOperationException("Dateipruefung war nicht erwartet.");

        public PdfFileSafetyResult CheckFileBudget(string pdfPath, long? maxBytes = null) =>
            throw new InvalidOperationException("Dateipruefung war nicht erwartet.");

        public void ThrowIfFileTooLarge(string pdfPath, long? maxBytes = null) =>
            throw new InvalidOperationException("Dateipruefung war nicht erwartet.");
    }
}
