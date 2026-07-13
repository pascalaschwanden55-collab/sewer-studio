using System.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfExtractionBudgetTests
{
    [Fact]
    public void PageBudgetValidator_rejects_pdf_before_external_extraction()
    {
        using var directory = new TempDirectory();
        var pdfPath = Path.Combine(directory.Path, "two-pages.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            builder.AddPage(PageSize.A4);
            builder.AddPage(PageSize.A4);
            File.WriteAllBytes(pdfPath, builder.Build());
        }

        var exception = Assert.Throws<InvalidDataException>(
            () => PdfPageBudgetValidator.ThrowIfExceeded(pdfPath, maxPages: 1));

        Assert.Contains("zu viele Seiten", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadUtf8AtMost_does_not_read_beyond_character_budget()
    {
        using var directory = new TempDirectory();
        var textPath = Path.Combine(directory.Path, "pdftotext-output.txt");
        File.WriteAllText(textPath, "0123456789", Encoding.UTF8);

        var text = PdfExtractedTextBudget.ReadUtf8AtMost(textPath, maxCharacters: 5);

        Assert.Equal("01234", text);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
