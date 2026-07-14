using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Media;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

public sealed class PdfMergeServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio_PdfMergeServiceTests_" + Guid.NewGuid().ToString("N"));

    public PdfMergeServiceTests()
        => Directory.CreateDirectory(_tempRoot);

    [Fact]
    public void MergeWithOriginals_haengt_Originalseiten_an_und_ueberspringt_fehlende_Dateien()
    {
        var originalPath = Path.Combine(_tempRoot, "original.pdf");
        File.WriteAllBytes(originalPath, CreatePdf(pageCount: 1));
        IPdfMergeService service = new PdfMergeService();

        var merged = service.MergeWithOriginals(
            CreatePdf(pageCount: 2),
            [Path.Combine(_tempRoot, "fehlt.pdf"), originalPath]);

        using var document = PdfDocument.Open(merged);
        Assert.Equal(3, document.NumberOfPages);
    }

    [Fact]
    public void MergeWithOriginals_gibt_unlesbares_erzeugtes_Pdf_unveraendert_zurueck()
    {
        IPdfMergeService service = new PdfMergeService();
        var invalidPdf = new byte[] { 1, 2, 3, 4 };

        var result = service.MergeWithOriginals(invalidPdf, ["original.pdf"]);

        Assert.Same(invalidPdf, result);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Aufraeumfehler duerfen das Testergebnis nicht verdecken.
        }
    }

    private static byte[] CreatePdf(int pageCount)
    {
        using var builder = new PdfDocumentBuilder();
        for (var i = 0; i < pageCount; i++)
            builder.AddPage(PageSize.A4);
        return builder.Build();
    }
}
