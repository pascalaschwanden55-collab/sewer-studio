using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests.DataPage;

public sealed class SchachtFileTargetPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SchachtFileTargetPathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolvePdfPath_loest_relativen_Pfad_gegen_den_Projektroot_auf()
    {
        var projectFile = Path.Combine(_root, "Projektdateien", "projekt.json");
        var pdfPath = Path.Combine(_root, "Schaechte", "80454.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
        File.WriteAllText(pdfPath, "pdf");
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.PdfPath, Path.GetRelativePath(_root, pdfPath));
        ISchachtFileTargetResolver resolver = new SchachtFileTargetPathResolver();

        var result = resolver.ResolvePdfPath(record, projectFile);

        Assert.Equal(Path.GetFullPath(pdfPath), result);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
