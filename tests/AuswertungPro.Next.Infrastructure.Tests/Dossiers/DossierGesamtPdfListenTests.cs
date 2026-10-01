using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Media;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// „Alles zu einem PDF" heisst wirklich alles: Haltungs- und Schachtliste
/// gehoeren fest dazu und werden dabei frisch aus dem aktuellen Dossierstand
/// erzeugt — nicht aus einer moeglicherweise veralteten Datei im Ordner.
/// </summary>
public sealed class DossierGesamtPdfListenTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "dossier_gesamt_" + Guid.NewGuid().ToString("N"));

    public DossierGesamtPdfListenTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Gesamt_PDF_reiht_Erklaerblatt_Haltungsliste_und_Schachtliste_vor_die_Beilagen()
    {
        await WriteWordPlaceholderAsync();
        var beilagen = Path.Combine(_folder, DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(beilagen);
        await File.WriteAllBytesAsync(
            Path.Combine(beilagen, "01_Protokoll.pdf"),
            CreatePdf("ORIGINALPROTOKOLL"));

        var service = CreateService();

        var result = await service.AssembleAsync(
            CreateRequest(holdings: 2, shafts: 1));

        Assert.True(result.Success, result.Message);
        using var document = PdfDocument.Open(result.FilePath!);
        Assert.Equal(5, document.NumberOfPages);
        Assert.Contains("WORD-DOSSIER", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Zustandsklassen", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("Haltungsliste", document.GetPage(3).Text, StringComparison.Ordinal);
        Assert.Contains("Schachtliste", document.GetPage(4).Text, StringComparison.Ordinal);
        Assert.Contains("ORIGINALPROTOKOLL", document.GetPage(5).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Listen_bleiben_auch_bei_Abwahl_aller_Blaetter_erhalten()
    {
        await WriteWordPlaceholderAsync();
        var service = CreateService();

        var result = await service.AssembleAsync(
            CreateRequest(holdings: 1, shafts: 1),
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(new HashSet<int> { 1, 2, 3, 4 }));

        Assert.True(result.Success, result.Message);
        using var document = PdfDocument.Open(result.FilePath!);
        Assert.Equal(3, document.NumberOfPages);
        Assert.Contains("Zustandsklassen", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Haltungsliste", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("Schachtliste", document.GetPage(3).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dossier_ohne_Schaechte_bekommt_keine_leere_Schachtliste()
    {
        await WriteWordPlaceholderAsync();
        var service = CreateService();

        var result = await service.AssembleAsync(CreateRequest(holdings: 1, shafts: 0));

        Assert.True(result.Success, result.Message);
        using var document = PdfDocument.Open(result.FilePath!);
        Assert.Equal(3, document.NumberOfPages);
        Assert.DoesNotContain(
            "Schachtliste",
            string.Join(" ", document.GetPages().Select(page => page.Text)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Gesamt_PDF_legt_keine_Listendatei_im_Kundenordner_ab()
    {
        await WriteWordPlaceholderAsync();
        var service = CreateService();

        var result = await service.AssembleAsync(CreateRequest(holdings: 2, shafts: 2));

        Assert.True(result.Success, result.Message);
        var dateien = Directory
            .EnumerateFiles(_folder, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToList();
        Assert.DoesNotContain(DossierFolderPlanner.HoldingListPdfFileName, dateien);
        Assert.DoesNotContain(DossierFolderPlanner.ShaftListPdfFileName, dateien);
    }

    [Fact]
    public async Task Die_Erfolgsmeldung_nennt_die_enthaltenen_Listen()
    {
        await WriteWordPlaceholderAsync();
        var service = CreateService();

        var beide = await service.AssembleAsync(CreateRequest(holdings: 1, shafts: 1));
        var nurHaltungen = await service.AssembleAsync(CreateRequest(holdings: 1, shafts: 0));
        var ohneListen = await service.AssembleAsync(CreateRequest(holdings: 0, shafts: 0));

        Assert.Contains("Haltungsliste", beide.Message, StringComparison.Ordinal);
        Assert.Contains("Schachtliste", beide.Message, StringComparison.Ordinal);
        Assert.Contains("Haltungsliste", nurHaltungen.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Schachtliste", nurHaltungen.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Haltungsliste", ohneListen.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Schachtliste", ohneListen.Message, StringComparison.Ordinal);
    }

    private DossierPdfAssemblyService CreateService()
        => new(
            new PdfMergeService(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, CreatePdf("WORD-DOSSIER"));
                return true;
            });

    private Task WriteWordPlaceholderAsync()
        => File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

    private DossierExportRequest CreateRequest(int holdings, int shafts)
        => new(
            new Project(),
            _folder,
            new DossierAreaSettings(),
            new DossierDefinition
            {
                Name = "Testliegenschaft",
                OwnerName = "Muster AG",
                Address = "Musterweg 1",
                PostalCode = "6460",
                Town = "Altdorf"
            },
            CreateSnapshot(holdings, shafts),
            _folder);

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

    private static byte[] CreatePdf(string text)
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
}
