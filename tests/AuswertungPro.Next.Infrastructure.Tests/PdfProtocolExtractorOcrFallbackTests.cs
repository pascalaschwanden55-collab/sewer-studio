using System.Text;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfProtocolExtractorOcrFallbackTests
{
    [Fact]
    public async Task ExtractAsync_UsesOcr_WhenPdfPigTextIsEmpty()
    {
        var pdfPath = WritePdf(pageCount: 1);
        try
        {
            var ocr = new FakeOcrFallback(new Dictionary<int, OcrPageExtractionResult>
            {
                [1] = new(true, "0.00 BCD 029 00:00:16 Rohranfang", null)
            });
            var extractor = new PdfProtocolExtractor(logger: null, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            var entry = Assert.Single(entries);
            Assert.Equal("BCD", entry.VsaCode);
            Assert.Equal(0.00, entry.MeterStart, precision: 2);
            Assert.Equal(new[] { 1 }, ocr.Calls);
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_UsesOcr_WhenPdfPigTextIsNearlyEmpty()
    {
        var pdfPath = WritePdf("abc");
        try
        {
            var ocr = new FakeOcrFallback(new Dictionary<int, OcrPageExtractionResult>
            {
                [1] = new(true, "0.00 BCD 029 00:00:16 Rohranfang", null)
            });
            var extractor = new PdfProtocolExtractor(logger: null, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            Assert.Single(entries);
            Assert.Equal(new[] { 1 }, ocr.Calls);
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_DoesNotCallOcr_WhenPdfPigTextHasEntries()
    {
        var pdfPath = WritePdf("0.00 BCD 029 00:00:16 Rohranfang mit langer Beschreibung fuer den Nicht-OCR-Pfad");
        try
        {
            var ocr = new FakeOcrFallback();
            var extractor = new PdfProtocolExtractor(logger: null, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            Assert.Single(entries);
            Assert.Empty(ocr.Calls);
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_OcrTextWithoutEntries_LogsDistinctReason()
    {
        var pdfPath = WritePdf(pageCount: 1);
        try
        {
            var logger = new CaptureLogger();
            var ocr = new FakeOcrFallback(new Dictionary<int, OcrPageExtractionResult>
            {
                [1] = new(true, "Scan wurde gelesen, aber ohne verwertbare VSA-Codes.", null)
            });
            var extractor = new PdfProtocolExtractor(logger, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            Assert.Empty(entries);
            Assert.Contains(logger.Messages, m => m.Contains("OCR versucht, 0 Befunde", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_OcrFallback_CombinesPagesInOrder()
    {
        var pdfPath = WritePdf(pageCount: 2);
        try
        {
            var ocr = new FakeOcrFallback(new Dictionary<int, OcrPageExtractionResult>
            {
                [1] = new(true, "0.00 BCD 029 00:00:16 Rohranfang", null),
                [2] = new(true, "1.20 BCE 030 00:00:18 Rohrende", null)
            });
            var extractor = new PdfProtocolExtractor(logger: null, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            Assert.Equal(new[] { "BCD", "BCE" }, entries.Select(e => e.VsaCode).ToArray());
            Assert.Equal(new[] { 1, 2 }, ocr.Calls);
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    [Fact]
    public async Task ExtractAsync_SkipsOcr_WhenOcrPageBudgetExceeded()
    {
        var pdfPath = WritePdf(pageCount: 41);
        try
        {
            var logger = new CaptureLogger();
            var ocr = new FakeOcrFallback();
            var extractor = new PdfProtocolExtractor(logger, ocr);

            var entries = await extractor.ExtractAsync(pdfPath);

            Assert.Empty(entries);
            Assert.Empty(ocr.Calls);
            Assert.Contains(logger.Messages, m => m.Contains("OCR uebersprungen (Budget)", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    private static string WritePdf(string? visibleText = null, int pageCount = 1)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdf_protocol_ocr_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, BuildPdfBytes(visibleText, pageCount));
        return path;
    }

    private static byte[] BuildPdfBytes(string? visibleText, int pageCount)
    {
        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"
        };

        var pageIds = Enumerable.Range(0, pageCount).Select(i => 3 + i).ToArray();
        var nextId = 3 + pageCount;
        var fontId = visibleText is null ? 0 : nextId++;
        var contentIds = visibleText is null
            ? Array.Empty<int>()
            : Enumerable.Range(0, pageCount).Select(_ => nextId++).ToArray();

        objects.Add($"2 0 obj\n<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageCount} >>\nendobj\n");

        for (var i = 0; i < pageCount; i++)
        {
            var resources = visibleText is null
                ? "<< >>"
                : $"<< /Font << /F1 {fontId} 0 R >> >>";
            var contents = visibleText is null
                ? ""
                : $" /Contents {contentIds[i]} 0 R";
            objects.Add($"{pageIds[i]} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources {resources}{contents} >>\nendobj\n");
        }

        if (visibleText is not null)
        {
            objects.Add($"{fontId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
            foreach (var contentId in contentIds)
            {
                var escaped = visibleText.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                var stream = $"BT /F1 12 Tf 50 750 Td ({escaped}) Tj ET";
                objects.Add($"{contentId} 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream\nendobj\n");
            }
        }

        var sb = new StringBuilder();
        var offsets = new List<int>();
        sb.Append("%PDF-1.4\n");
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            sb.Append($"{offset:0000000000} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private sealed class FakeOcrFallback : IPdfProtocolOcrFallback
    {
        private readonly IReadOnlyDictionary<int, OcrPageExtractionResult> _pages;

        public FakeOcrFallback(IReadOnlyDictionary<int, OcrPageExtractionResult>? pages = null)
            => _pages = pages ?? new Dictionary<int, OcrPageExtractionResult>();

        public List<int> Calls { get; } = new();

        public OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
        {
            Calls.Add(pageNumber);
            return _pages.TryGetValue(pageNumber, out var result)
                ? result
                : new OcrPageExtractionResult(false, null, "Test-OCR nicht konfiguriert.");
        }
    }

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
