using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ShaftDistributionServiceTests
{
    [Fact]
    public void Projektziel_wird_vorbereitet_und_bleibt_bis_Publish_unsichtbar()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "shaft-distribution-service-" + Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(root, "Projekt");
        var projectPath = Path.Combine(projectRoot, "Projektdateien", "projekt.json");
        var sourcePdf = Path.Combine(root, "Quelle", "Schacht.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePdf)!);
        File.WriteAllText(projectPath, "{}");
        WritePdf(sourcePdf);

        try
        {
            using var staging = new ImportFileStagingService().Begin(projectPath)!;
            IShaftDistributionService service = new ShaftDistributionService();
            var destination = Path.Combine(projectRoot, ProjectStructure.SchaechteVerteilt);

            var result = service.Distribute(new ShaftDistributionRequest(
                Project: new Project(),
                DestinationFolder: destination,
                PdfFiles: [sourcePdf],
                FileStaging: staging));

            Assert.True(result.UsesPersistentProjectTransaction);
            var item = Assert.Single(result.Items);
            Assert.True(item.Success, item.Message);
            Assert.False(File.Exists(item.TargetPdfPath));
            Assert.True(File.Exists(item.ReadPdfPath));
            Assert.Equal(item.ReadPdfPath, staging.ResolveReadPath(item.TargetPdfPath!));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WritePdf(string path)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Projekt: Test Datum: 18.06.2026", 12, new PdfPoint(40, 780), font);
        page.AddText("Schachtprotokoll Schacht Nr. 22152", 18, new PdfPoint(40, 740), font);
        page.AddText("STAMMDATEN & SKIZZE", 12, new PdfPoint(40, 700), font);
        File.WriteAllBytes(path, builder.Build());
    }
}
