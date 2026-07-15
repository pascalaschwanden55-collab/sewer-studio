using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolOcrReaderServiceTests
{
    [Fact]
    public void BlockiertPdfUeberDerAngefordertenSeitengrenze()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var pdfPath = Path.Combine(directory, "zwei-seiten.pdf");
        try
        {
            WriteTwoPagePdf(pdfPath);
            var ocr = new ThrowingPdfOcrExtractor();
            var reader = new SchachtProtocolOcrReaderService(new PdfFileSafetyService(), ocr);

            var result = reader.TryRead(pdfPath, maxPages: 1);

            Assert.False(result.Success);
            Assert.Equal(2, result.TotalPages);
            Assert.Equal(0, result.ExtractedPages);
            Assert.Contains("zu viele Seiten", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Limit 1", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, ocr.PageCalls);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FuegtLesbareSeitenZusammenUndMeldetErstenSeitenfehler()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        var pdfPath = Path.Combine(directory, "zwei-seiten.pdf");
        try
        {
            WriteTwoPagePdf(pdfPath);
            var ocr = new SequencePdfOcrExtractor(
                new PdfOcrPageExtractionResult(true, "  Schachtprotokoll\r\nNr. 80454  ", null),
                new PdfOcrPageExtractionResult(false, null, "Seite nicht lesbar."));
            var reader = new SchachtProtocolOcrReaderService(new PdfFileSafetyService(), ocr);

            var result = reader.TryRead(pdfPath);

            Assert.True(result.Success);
            Assert.Equal("Schachtprotokoll\nNr. 80454", result.Text);
            Assert.Equal(2, result.TotalPages);
            Assert.Equal(1, result.ExtractedPages);
            Assert.Equal("Seite 2: Seite nicht lesbar.", result.Message);
            Assert.Equal(2, ocr.PageCalls);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteTwoPagePdf(string pdfPath)
    {
        using var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        builder.AddPage(PageSize.A4);
        File.WriteAllBytes(pdfPath, builder.Build());
    }

    private sealed class ThrowingPdfOcrExtractor : IPdfOcrExtractor
    {
        public int PageCalls { get; private set; }

        public PdfOcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
        {
            PageCalls++;
            throw new InvalidOperationException("OCR darf bei verletzter Seitengrenze nicht starten.");
        }

        public PdfOcrDocumentExtractionResult TryExtractAllPages(string pdfPath)
            => throw new InvalidOperationException("Ganzdokument-OCR darf hier nicht verwendet werden.");
    }

    private sealed class SequencePdfOcrExtractor : IPdfOcrExtractor
    {
        private readonly IReadOnlyList<PdfOcrPageExtractionResult> _pages;

        public SequencePdfOcrExtractor(params PdfOcrPageExtractionResult[] pages)
        {
            _pages = pages;
        }

        public int PageCalls { get; private set; }

        public PdfOcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
        {
            PageCalls++;
            return _pages[pageNumber - 1];
        }

        public PdfOcrDocumentExtractionResult TryExtractAllPages(string pdfPath)
            => throw new InvalidOperationException("Ganzdokument-OCR darf hier nicht verwendet werden.");
    }
}
