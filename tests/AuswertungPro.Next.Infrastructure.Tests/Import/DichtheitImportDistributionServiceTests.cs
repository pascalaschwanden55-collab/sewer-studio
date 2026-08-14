using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class DichtheitImportDistributionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"dichtheit-distribution-instance-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_EmptySourceReturnsEmptyResultThroughContract()
    {
        var sourceFolder = Path.Combine(_root, "Quelle");
        var projectFolder = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);
        IDichtheitImportDistributor service = new DichtheitImportDistributionService();

        var result = service.Distribute(new Project(), projectFolder, sourceFolder);

        Assert.Equal(0, result.Verteilt);
        Assert.Equal(0, result.NichtZugeordnet);
        Assert.Equal(0, result.Uebersprungen);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void InstanceService_bereitet_DichtheitsPdf_mit_Staging_nur_vor()
    {
        var sourceFolder = Path.Combine(_root, "Quelle", "DP");
        var projectFolder = Path.Combine(_root, "Projekt");
        var projectPath = Path.Combine(projectFolder, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "{}");
        var sourcePdf = Path.Combine(sourceFolder, "pruefung.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText("Dichtheitspruefung nach SIA 190", 12, new PdfPoint(40, 780), font);
            page.AddText("oberer Schacht: 100", 12, new PdfPoint(40, 750), font);
            page.AddText("unterer Schacht: 200", 12, new PdfPoint(40, 720), font);
            page.AddText("14.08.2026", 12, new PdfPoint(40, 690), font);
            File.WriteAllBytes(sourcePdf, builder.Build());
        }
        using var staging = new ImportFileStagingService().Begin(projectPath)!;
        IDichtheitImportDistributor service = new DichtheitImportDistributionService();

        var result = service.Distribute(
            new Project(),
            projectFolder,
            Path.GetDirectoryName(sourceFolder)!,
            ki: null,
            fileStaging: staging);

        Assert.Equal(1, result.Verteilt);
        var prepared = Assert.Single(staging.PreparedFiles);
        var target = Path.Combine(projectFolder, prepared.RelativePath);
        Assert.False(File.Exists(target));
        Assert.True(File.Exists(staging.ResolveReadPath(target)));
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
            // Temp-Aufraeumen darf das Testergebnis nicht verdecken.
        }
    }
}
