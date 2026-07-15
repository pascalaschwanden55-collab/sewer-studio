using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfOcrExtractorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    [Fact]
    public void Fehlende_Pdf_liefert_verstaendliche_Meldung()
    {
        var result = PdfOcrExtractor.TryExtractPageText(
            Path.Combine(_directory, "fehlt.pdf"),
            pageNumber: 1);

        Assert.False(result.Success);
        Assert.Null(result.Text);
        Assert.Equal("PDF wurde nicht gefunden.", result.Message);
    }

    [Fact]
    public void Seitennummer_null_wird_vor_Programmstart_abgelehnt()
    {
        var pdfPath = Path.Combine(_directory, "vorhanden.pdf");
        File.WriteAllBytes(pdfPath, []);

        var result = PdfOcrExtractor.TryExtractPageText(pdfPath, pageNumber: 0);

        Assert.False(result.Success);
        Assert.Null(result.Text);
        Assert.Equal("Ungueltige Seitennummer.", result.Message);
    }

    [Fact]
    public void Instanzdienst_behaelt_die_Meldung_fuer_fehlende_Pdf()
    {
        var service = new PdfOcrExtractionService(new PdfTextExtractionService());

        var result = service.TryExtractPageText(
            Path.Combine(_directory, "fehlt.pdf"),
            pageNumber: 1);

        Assert.False(result.Success);
        Assert.Equal("PDF wurde nicht gefunden.", result.Message);
    }

    [Fact]
    public void PdfImport_verwendet_injizierten_Ocr_Dienst()
    {
        var ocr = new FakeOcrExtractor();
        var service = new LegacyPdfImportService(new EmptyTextExtractor(), ocr);

        var result = service.ImportPdf("scan.pdf", new Project());

        Assert.Equal(1, ocr.AllPagesCalls);
        Assert.Equal(1, result.Errors);
        Assert.Contains(result.Messages, message =>
            message.Message.Contains("OCR absichtlich leer", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class EmptyTextExtractor : IPdfTextExtractor
    {
        public string FindPdfToTextPath(string? explicitPath = null) => throw new NotSupportedException();

        public PdfTextExtractionResult ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
            => new(Array.Empty<string>(), string.Empty);

        public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
            => throw new NotSupportedException();
    }

    private sealed class FakeOcrExtractor : IPdfOcrExtractor
    {
        public int AllPagesCalls { get; private set; }

        public PdfOcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
            => throw new NotSupportedException();

        public PdfOcrDocumentExtractionResult TryExtractAllPages(string pdfPath)
        {
            AllPagesCalls++;
            return new PdfOcrDocumentExtractionResult(
                Array.Empty<string>(),
                "OCR absichtlich leer");
        }
    }
}
