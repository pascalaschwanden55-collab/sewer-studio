using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfTextExtractionServiceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    [Fact]
    public void Instanzdienst_liest_Text_auch_ohne_pdftotext()
    {
        var pdfPath = Path.Combine(_directory, "text.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText("Sewer Studio PDF Text", 12, new PdfPoint(40, 780), font);
            File.WriteAllBytes(pdfPath, builder.Build());
        }

        var service = new PdfTextExtractionService();
        var result = service.ExtractPages(
            pdfPath,
            explicitPdfToTextPath: Path.Combine(_directory, "fehlt", "pdftotext.exe"));

        Assert.Single(result.Pages);
        Assert.Contains("Sewer Studio PDF Text", result.FullText, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
