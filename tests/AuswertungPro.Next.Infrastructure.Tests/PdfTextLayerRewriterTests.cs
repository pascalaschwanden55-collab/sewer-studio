using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfTextLayerRewriterTests
{
    [Fact]
    public void TryRewriteHoldingNumber_ErzeugtKorrigiertePdfMitNeuerNummer()
    {
        using var temp = new TempDirectory();
        var sourcePath = Path.Combine(temp.Path, "quelle.pdf");
        WritePdf(sourcePath, "Haltung 1000-2000");

        var result = PdfTextLayerRewriter.TryRewriteHoldingNumber(
            sourcePath,
            "1000-2000",
            "3000-4000");

        try
        {
            Assert.True(result.Success, result.Message);
            Assert.True(result.Corrected);
            Assert.NotEqual(sourcePath, result.OutputPdfPath);
            Assert.True(File.Exists(result.OutputPdfPath));
            Assert.Equal(1, result.MatchCount);
            Assert.Equal(1, result.PageCount);

            using var corrected = PdfDocument.Open(result.OutputPdfPath);
            var text = string.Join("\n", corrected.GetPages().Select(page => page.Text));
            Assert.Contains("3000-4000", text);
        }
        finally
        {
            if (File.Exists(result.OutputPdfPath)
                && !string.Equals(result.OutputPdfPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(result.OutputPdfPath);
            }
        }
    }

    private static void WritePdf(string path, string text)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(40, 780), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-pdf-text-rewrite-" + Guid.NewGuid().ToString("N"));
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
