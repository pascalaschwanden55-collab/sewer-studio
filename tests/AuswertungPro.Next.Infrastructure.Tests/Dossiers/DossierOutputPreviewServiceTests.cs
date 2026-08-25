using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierOutputPreviewServiceTests
{
    [Fact]
    public async Task CreateAsync_erzeugt_ausserhalb_des_Projekts_und_bereinigt_den_Arbeitsordner()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Kundenprojekt");
        var planFolder = Path.Combine(projectRoot, "Plaene");
        Directory.CreateDirectory(planFolder);
        var planPath = Path.Combine(planFolder, "Plan.png");
        await File.WriteAllBytesAsync(planPath, [1, 2, 3]);

        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");
        var exporter = new RecordingWordExporter();
        var service = new DossierOutputPreviewService(
            exporter,
            (wordPath, pdfPath) =>
            {
                Assert.StartsWith(previewRoot, wordPath, StringComparison.OrdinalIgnoreCase);
                Assert.NotNull(pdfPath);
                File.WriteAllBytes(pdfPath!, [7, 8, 9]);
                return true;
            },
            _ =>
            [
                new DossierOutputPreviewPage(
                    1,
                    595,
                    842,
                    "Testseite",
                    [new DossierOutputPreviewWord("Testseite", 10, 20, 80, 40)])
            ],
            () => previewRoot);

        var request = Request(
            projectRoot,
            Path.Combine(projectRoot, "Dossiers", "Liegenschaft"),
            Path.Combine("Plaene", "Plan.png"));

        var result = await service.CreateAsync(request);

        Assert.True(result.Success, result.Message);
        Assert.Equal([7, 8, 9], result.PdfBytes);
        Assert.Single(result.Pages);
        Assert.NotNull(exporter.Request);
        Assert.Equal(previewRoot, exporter.Request!.ProjectRoot);
        Assert.StartsWith(previewRoot, exporter.Request.TargetFolder, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(planPath, exporter.Request.Dossier.OverviewPlanPath);
        Assert.False(Directory.Exists(previewRoot));
        Assert.False(Directory.Exists(request.TargetFolder));
        Assert.Equal(Path.Combine("Plaene", "Plan.png"), request.Dossier.OverviewPlanPath);
    }

    [Fact]
    public async Task CreateAsync_meldet_einen_Wandlerfehler_und_hinterlaesst_keine_Arbeitsdatei()
    {
        using var temp = new TempDirectory();
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");
        var service = new DossierOutputPreviewService(
            new RecordingWordExporter(),
            (_, _) => false,
            _ => throw new InvalidOperationException("PDF darf nicht gelesen werden."),
            () => previewRoot);

        var result = await service.CreateAsync(Request(
            Path.Combine(temp.Path, "Projekt"),
            Path.Combine(temp.Path, "Projekt", "Dossiers", "Fall"),
            overviewPlanPath: ""));

        Assert.False(result.Success);
        Assert.Null(result.PdfBytes);
        Assert.Empty(result.Pages);
        Assert.Contains("PDF", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(previewRoot));
    }

    [Fact]
    public async Task CreateAsync_zeigt_vorhandene_Beilagen_ohne_den_Kundenordner_zu_veraendern()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Projekt");
        var targetFolder = Path.Combine(projectRoot, "Dossiers", "Fall");
        var attachmentFolder = Path.Combine(targetFolder, DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(attachmentFolder);
        var attachment = Path.Combine(attachmentFolder, "01_Original.pdf");
        await File.WriteAllBytesAsync(attachment, [1, 2, 3, 4]);
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");

        var service = new DossierOutputPreviewService(
            new RecordingWordExporter(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, [5, 6]);
                return true;
            },
            (generated, attachments) =>
            {
                Assert.Equal([5, 6], generated);
                Assert.Equal([attachment], attachments);
                return [7, 8, 9];
            },
            pdfPath => pdfPath.EndsWith("-komplett.pdf", StringComparison.Ordinal)
                ?
                [
                    new DossierOutputPreviewPage(1, 595, 842, "Dossier", []),
                    new DossierOutputPreviewPage(2, 595, 842, "Original", [])
                ]
                :
                [
                    new DossierOutputPreviewPage(1, 595, 842, "Dossier", [])
                ],
            () => previewRoot);

        var result = await service.CreateAsync(Request(projectRoot, targetFolder, ""));

        Assert.True(result.Success, result.Message);
        Assert.Equal([7, 8, 9], result.PdfBytes);
        Assert.Contains("1 Beilage", result.Message, StringComparison.Ordinal);
        Assert.False(result.Pages[0].IsAttachment);
        Assert.True(result.Pages[1].IsAttachment);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(attachment));
        Assert.False(Directory.Exists(previewRoot));
    }

    [Fact]
    public async Task CreateAsync_verwendet_frisch_gesammelte_Beilagen_aus_dem_Temp_Ordner()
    {
        using var temp = new TempDirectory();
        var projectRoot = Path.Combine(temp.Path, "Projekt");
        var targetFolder = Path.Combine(projectRoot, "Dossiers", "Fall");
        var realAttachmentFolder = Path.Combine(
            targetFolder,
            DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(realAttachmentFolder);
        var staleAttachment = Path.Combine(realAttachmentFolder, "01_TV_Alt.pdf");
        await File.WriteAllTextAsync(staleAttachment, "Alter Stand");
        var previewRoot = Path.Combine(temp.Path, "Vorschauarbeit");

        var service = new DossierOutputPreviewService(
            new RecordingWordExporter(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, [5, 6]);
                return true;
            },
            (generated, attachments) =>
            {
                Assert.Equal([5, 6], generated);
                var attachment = Assert.Single(attachments);
                Assert.StartsWith(previewRoot, attachment, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Aktuelles Original", File.ReadAllText(attachment));
                Assert.NotEqual(staleAttachment, attachment);
                return [7, 8, 9];
            },
            pdfPath => pdfPath.EndsWith("-komplett.pdf", StringComparison.Ordinal)
                ?
                [
                    new DossierOutputPreviewPage(1, 595, 842, "Dossier", []),
                    new DossierOutputPreviewPage(2, 595, 842, "Aktuelles Original", [])
                ]
                :
                [
                    new DossierOutputPreviewPage(1, 595, 842, "Dossier", [])
                ],
            () => previewRoot,
            async (_, temporaryDossierFolder, ct) =>
            {
                var folder = Path.Combine(
                    temporaryDossierFolder,
                    DossierFolderPlanner.AttachmentFolderName);
                Directory.CreateDirectory(folder);
                var fresh = Path.Combine(folder, "01_TV_Aktuell.pdf");
                await File.WriteAllTextAsync(fresh, "Aktuelles Original", ct);
                return new DossierAttachmentResult(
                    [
                        new DossierAttachment(
                            Path.GetFileName(fresh),
                            fresh,
                            DossierAttachmentKind.OriginalProtocol,
                            "100-200")
                    ],
                    []);
            });

        var result = await service.CreateAsync(Request(projectRoot, targetFolder, ""));

        Assert.True(result.Success, result.Message);
        Assert.Equal([7, 8, 9], result.PdfBytes);
        Assert.True(result.Pages[1].IsAttachment);
        Assert.Equal("Alter Stand", await File.ReadAllTextAsync(staleAttachment));
        Assert.False(Directory.Exists(previewRoot));
    }

    private static DossierExportRequest Request(
        string projectRoot,
        string targetFolder,
        string overviewPlanPath)
        => new(
            new Project(),
            projectRoot,
            new DossierAreaSettings(),
            new DossierDefinition { OverviewPlanPath = overviewPlanPath },
            DossierSnapshotBuilder.Build(new DossierDefinition(), new Project(), null),
            targetFolder);

    private sealed class RecordingWordExporter : IDossierWordExportService
    {
        public DossierExportRequest? Request { get; private set; }

        public Task<DossierWordExportResult> ExportAsync(
            DossierExportRequest request,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Request = request;
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
                "SewerStudio_DossierPreviewTests_" + Guid.NewGuid().ToString("N"));
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
