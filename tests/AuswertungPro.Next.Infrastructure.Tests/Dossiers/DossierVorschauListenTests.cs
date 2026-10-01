using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Media;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die Vorschau zeigt genau das, was die Ausgabe schreibt — also auch die
/// beiden Bauteillisten an derselben Stelle.
/// </summary>
public sealed class DossierVorschauListenTests
{
    [Fact]
    public async Task Vorschau_zeigt_Haltungs_und_Schachtliste_hinter_dem_Erklaerblatt()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Projekt");
        var targetFolder = Path.Combine(projectRoot, "Dossiers", "Fall");
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");

        var service = CreateService(temp.Path, previewRoot);

        var result = await service.CreateAsync(
            Request(projectRoot, targetFolder, holdings: 2, shafts: 1));

        Assert.True(result.Success, result.Message);
        Assert.Equal(4, result.Pages.Count);
        Assert.Contains("WORD-DOSSIER", result.Pages[0].Text, StringComparison.Ordinal);
        Assert.Contains("Zustandsklassen", result.Pages[1].Text, StringComparison.Ordinal);
        Assert.Contains("Haltungsliste", result.Pages[2].Text, StringComparison.Ordinal);
        Assert.Contains("Schachtliste", result.Pages[3].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vorschau_beschriftet_jedes_automatisch_erzeugte_Blatt()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Projekt");
        var targetFolder = Path.Combine(projectRoot, "Dossiers", "Fall");
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");

        var service = CreateService(temp.Path, previewRoot);

        var result = await service.CreateAsync(
            Request(projectRoot, targetFolder, holdings: 1, shafts: 1));

        Assert.Equal(
            [null, "Dossier-Erklärung", "Haltungsliste", "Schachtliste"],
            result.Pages.Select(page => page.GeneratedPageLabel));
        Assert.True(result.Pages[1].IsConditionClassExplanation);
        Assert.False(result.Pages[2].IsConditionClassExplanation);
    }

    [Fact]
    public async Task Vorschau_ohne_Bauteile_bleibt_beim_bisherigen_Umfang()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Projekt");
        var targetFolder = Path.Combine(projectRoot, "Dossiers", "Fall");
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");

        var service = CreateService(temp.Path, previewRoot);

        var result = await service.CreateAsync(
            Request(projectRoot, targetFolder, holdings: 0, shafts: 0));

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Pages.Count);
        Assert.Equal([null, "Dossier-Erklärung"],
            result.Pages.Select(page => page.GeneratedPageLabel));
    }

    private static DossierOutputPreviewService CreateService(
        string assetFolder,
        string previewRoot)
    {
        var composer = new DossierPdfPackageComposer(
            new PdfMergeService(),
            new DossierConditionClassPdfService(templateAssetFolder: assetFolder));
        var lists = new DossierComponentListPdfRenderer(
            new DossierHoldingListPdfService(templateAssetFolder: assetFolder),
            new DossierShaftListPdfService(templateAssetFolder: assetFolder),
            () => new DateTime(2026, 8, 31, 9, 0, 0, DateTimeKind.Local));

        return new DossierOutputPreviewService(
            new StubWordExporter(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, CreateQuestPdf("WORD-DOSSIER"));
                return true;
            },
            composer,
            lists,
            () => previewRoot);
    }

    private static DossierExportRequest Request(
        string projectRoot,
        string targetFolder,
        int holdings,
        int shafts)
        => new(
            new Project(),
            projectRoot,
            new DossierAreaSettings(),
            new DossierDefinition { Name = "Testliegenschaft", OwnerName = "Muster AG" },
            CreateSnapshot(holdings, shafts),
            targetFolder);

    private static DossierSnapshot CreateSnapshot(int holdings, int shafts)
    {
        var verteilung = new ZustandVerteilung(Array.Empty<ZustandBucket>());
        var statistik = new DashboardStatistics(
            0, 0, 0, 0, verteilung, verteilung,
            Array.Empty<DashboardBucket>(), Array.Empty<DashboardCostBucket>(), 0, 0, 0, 0, 0);

        return new DossierSnapshot(
            Guid.NewGuid(),
            "Testliegenschaft",
            Enumerable.Range(1, holdings)
                .Select(index => new DossierHoldingLine(
                    Guid.NewGuid(), "H" + index, "Musterweg", 12.5, "2", 0m, ""))
                .ToList(),
            [],
            statistik,
            Enumerable.Range(1, shafts)
                .Select(index => new DossierShaftLine(
                    Guid.NewGuid(), "S" + index, "Musterweg", "3", 0m))
                .ToList(),
            []);
    }

    private static byte[] CreateQuestPdf(string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text(text);
            });
        }).GeneratePdf();
    }

    private sealed class StubWordExporter : IDossierWordExportService
    {
        public Task<DossierWordExportResult> ExportAsync(
            DossierExportRequest request,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(request.TargetFolder);
            var path = Path.Combine(request.TargetFolder, "Eigentuemerdossier.docx");
            File.WriteAllBytes(path, [4, 5, 6]);
            return Task.FromResult(new DossierWordExportResult(true, path, "erstellt"));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio_DossierVorschauListen_" + Guid.NewGuid().ToString("N"));
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
