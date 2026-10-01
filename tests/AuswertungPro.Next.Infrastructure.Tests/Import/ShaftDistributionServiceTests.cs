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
    public void GemischterBericht_TvSeitenTrennenZweiSchachtteile()
    {
        var root = Path.Combine(Path.GetTempPath(), "shaft-mixed-distribution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "gemischt.pdf");
            using (var builder = new PdfDocumentBuilder())
            {
                var font = builder.AddStandard14Font(Standard14Font.Helvetica);
                void Schacht(string nummer)
                {
                    var page = builder.AddPage(PageSize.A4);
                    page.AddText("Projekt: Test Datum: 18.06.2026", 12, new PdfPoint(40, 780), font);
                    page.AddText("Schachtprotokoll Schacht Nr. " + nummer, 12, new PdfPoint(40, 740), font);
                }
                Schacht("22152");
                for (var i = 0; i < 8; i++)
                    builder.AddPage(PageSize.A4).AddText("Haltungsinspektion - 18.06.2026 - 1000-2000", 12, new PdfPoint(40, 780), font);
                Schacht("22333");
                File.WriteAllBytes(source, builder.Build());
            }
            var result = new ShaftDistributionService().Distribute(new ShaftDistributionRequest(
                new Project(), Path.Combine(root, "Schaechte"), PdfFiles: [source]));
            Assert.Equal(2, result.Items.Count);
            Assert.All(result.Items, item =>
            {
                Assert.True(item.Success, item.Message);
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(item.ReadPdfPath!);
                Assert.Equal(1, pdf.NumberOfPages);
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReinesHaltungsprotokoll_WirdOhneSchachtversuchUebersprungen(bool vorbereitet)
    {
        var root = Path.Combine(Path.GetTempPath(), "shaft-tv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "tv.pdf");
            using (var builder = new PdfDocumentBuilder())
            {
                var font = builder.AddStandard14Font(Standard14Font.Helvetica);
                builder.AddPage(PageSize.A4).AddText("Projekt Test Kunde Abwasser Unternehmer Test", 12, new PdfPoint(40, 780), font);
                builder.AddPage(PageSize.A4).AddText("Haltungsinspektion - 18.06.2026 - 1000-2000", 12, new PdfPoint(40, 780), font);
                File.WriteAllBytes(source, builder.Build());
            }
            var projectPath = Path.Combine(root, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
            File.WriteAllText(projectPath, "{}");
            using var staging = vorbereitet ? new ImportFileStagingService().Begin(projectPath) : null;
            var result = new ShaftDistributionService().Distribute(new ShaftDistributionRequest(
                new Project(), Path.Combine(root, "Schaechte_Verteilt"), PdfFiles: [source], FileStaging: staging));
            Assert.Empty(result.Items);
        }
        finally { Directory.Delete(root, true); }
    }

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

    [Fact]
    public void ArchivImStaging_WirdVorPublishGelesen()
    {
        var root = Path.Combine(Path.GetTempPath(), "shaft-source-staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var projectPath = Path.Combine(root, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
            File.WriteAllText(projectPath, "{}");
            var source = Path.Combine(root, "quelle.pdf");
            WritePdf(source);
            using var staging = new ImportFileStagingService().Begin(projectPath)!;
            var archive = Path.Combine(root, "Importdateien", "PDF");
            staging.StageCopyAs(source, archive, "sammel.pdf");
            Assert.False(Directory.Exists(archive));
            var result = new ShaftDistributionService().Distribute(new ShaftDistributionRequest(
                new Project(), Path.Combine(root, ProjectStructure.SchaechteVerteilt),
                PdfSourceFolder: archive, FileStaging: staging));
            var item = Assert.Single(result.Items);
            Assert.True(item.Success, item.Message);
            Assert.True(File.Exists(item.ReadPdfPath));
            Assert.False(File.Exists(item.TargetPdfPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Staging_Fortschritt_zaehlt_alle_Quellen_auch_Lesefehler()
    {
        var root = Path.Combine(Path.GetTempPath(), "shaft-progress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var projectPath = Path.Combine(root, "projekt.json");
            File.WriteAllText(projectPath, "{}");
            var source = Path.Combine(root, "Schacht.pdf");
            WritePdf(source);
            var missing = Path.Combine(root, "fehlt.pdf");
            var messages = new List<ShaftDistributionProgress>();
            using var staging = new ImportFileStagingService().Begin(projectPath)!;
            var result = new ShaftDistributionService().Distribute(new ShaftDistributionRequest(
                new Project(), Path.Combine(root, ProjectStructure.SchaechteVerteilt),
                PdfFiles: [source, missing], Progress: new SofortFortschritt(messages.Add), FileStaging: staging));

            Assert.Equal(new[] { 0, 1, 1, 2 }, messages.Where(p => p.Total == 2).Select(p => p.Processed));
            Assert.Contains(messages, p => p.CurrentFile == missing && p.Processed == 2);
            Assert.Equal(0, messages[^1].Total); // Nacharbeit ist keine weitere Quelldatei.
            Assert.Contains(result.Items, i => !i.Success && i.SourcePdfPath == missing);
            Assert.All(result.Items.Where(i => i.Success), i => Assert.False(File.Exists(i.TargetPdfPath)));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class SofortFortschritt(Action<ShaftDistributionProgress> melden) : IProgress<ShaftDistributionProgress>
    {
        public void Report(ShaftDistributionProgress value) => melden(value);
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
