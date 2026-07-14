using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfImportNegativeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportPdf_LeereOderAbgeschnitteneDatei_WirdAlsEinzelfehlerZurueckgegeben(
        bool truncatedHeader)
    {
        var root = Path.Combine(Path.GetTempPath(), $"pdf-negative-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var pdfPath = Path.Combine(root, truncatedHeader ? "abgeschnitten.pdf" : "leer.pdf");
        var missingPdfToText = Path.Combine(root, "nicht-vorhanden", "pdftotext.exe");
        File.WriteAllBytes(
            pdfPath,
            truncatedHeader ? "%PDF-1.7\n1 0 obj\n<<"u8.ToArray() : []);
        var project = new Project();

        try
        {
            var result = new PdfImportServiceAdapter().ImportPdf(
                pdfPath,
                project,
                missingPdfToText);

            Assert.True(result.Ok, result.ErrorMessage);
            var stats = Assert.IsType<AuswertungPro.Next.Application.Import.ImportStats>(result.Value);
            Assert.Equal(1, stats.Errors);
            Assert.Equal(0, stats.Found);
            Assert.Equal(0, stats.Created);
            Assert.Equal(0, stats.Updated);
            Assert.Contains(stats.Messages, message =>
                message.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)
                && message.Contains("PDF", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(project.Data);
            Assert.Empty(project.ImportHistory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
