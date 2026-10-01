using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierComponentListExportServiceTests
{
    [Fact]
    public async Task CreateHoldingListAsync_schreibt_neue_Datei_im_Dossierordner()
    {
        using var temp = new TempDirectory();
        var targetFolder = Path.Combine(
            temp.Path,
            DossierFolderPlanner.DossierRootFolderName,
            "Musterweg-7");
        var holdingPdf = new FixedHoldingListPdfService([1, 2, 3, 4]);
        var shaftPdf = new FixedShaftListPdfService([5]);
        var stand = new DateTime(2026, 8, 28, 10, 30, 0);
        var service = new DossierComponentListExportService(
            holdingPdf,
            shaftPdf,
            () => stand);

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, targetFolder));

        Assert.True(result.Success, result.Message);
        Assert.Equal(
            Path.Combine(targetFolder, DossierFolderPlanner.HoldingListPdfFileName),
            result.FilePath);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(result.FilePath!));
        Assert.Equal(stand, Assert.Single(holdingPdf.Models).Stand);
        Assert.Empty(shaftPdf.Models);
        Assert.Empty(Directory.EnumerateFiles(targetFolder, ".dossier-bauteilliste-*.tmp"));
    }

    [Fact]
    public async Task CreateHoldingListAsync_ueberschreibt_vorhandene_Datei_nie()
    {
        using var temp = new TempDirectory();
        var targetFolder = CreateDossierFolder(temp.Path);
        var existingPath = Path.Combine(
            targetFolder,
            DossierFolderPlanner.HoldingListPdfFileName);
        await File.WriteAllBytesAsync(existingPath, [9, 9, 9]);
        var service = CreateService(holdingBytes: [1, 2, 3]);

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, targetFolder));

        Assert.True(result.Success, result.Message);
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(existingPath));
        Assert.Equal(
            Path.Combine(targetFolder, "Haltungsliste_Eigentuemer_Dossier-2.pdf"),
            result.FilePath);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(result.FilePath!));
    }

    [Fact]
    public async Task CreateShaftListAsync_nutzt_nur_den_Schacht_Renderer()
    {
        using var temp = new TempDirectory();
        var targetFolder = CreateDossierFolder(temp.Path);
        var holdingPdf = new FixedHoldingListPdfService([1]);
        var shaftPdf = new FixedShaftListPdfService([6, 7, 8]);
        var service = new DossierComponentListExportService(
            holdingPdf,
            shaftPdf,
            () => new DateTime(2026, 8, 28));

        var result = await service.CreateShaftListAsync(
            CreateRequest(temp.Path, targetFolder));

        Assert.True(result.Success, result.Message);
        Assert.Equal(
            Path.Combine(targetFolder, DossierFolderPlanner.ShaftListPdfFileName),
            result.FilePath);
        Assert.Equal([6, 7, 8], await File.ReadAllBytesAsync(result.FilePath!));
        Assert.Empty(holdingPdf.Models);
        Assert.Single(shaftPdf.Models);
    }

    [Fact]
    public async Task Leere_Pdf_liefert_klaren_Fehler_und_schreibt_nichts()
    {
        using var temp = new TempDirectory();
        var targetFolder = Path.Combine(
            temp.Path,
            DossierFolderPlanner.DossierRootFolderName,
            "Musterweg-7");
        var service = CreateService(holdingBytes: []);

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, targetFolder));

        Assert.False(result.Success);
        Assert.Null(result.FilePath);
        Assert.Contains("Haltungsliste", result.Message, StringComparison.Ordinal);
        Assert.Contains("leer", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(targetFolder));
    }

    [Fact]
    public async Task Ziel_ausserhalb_des_Dossierordners_wird_abgelehnt()
    {
        using var temp = new TempDirectory();
        var unsafeTarget = Path.Combine(temp.Path, "Nicht-Dossiers", "Musterweg-7");
        var holdingPdf = new FixedHoldingListPdfService([1, 2, 3]);
        var service = new DossierComponentListExportService(
            holdingPdf,
            new FixedShaftListPdfService([4]));

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, unsafeTarget));

        Assert.False(result.Success);
        Assert.Null(result.FilePath);
        Assert.Contains("Dossierordner", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(holdingPdf.Models);
        Assert.False(Directory.Exists(unsafeTarget));
    }

    [Fact]
    public async Task Ziel_eines_anderen_Eigentuemer_Dossiers_wird_abgelehnt()
    {
        using var temp = new TempDirectory();
        var otherDossierFolder = Path.Combine(
            temp.Path,
            DossierFolderPlanner.DossierRootFolderName,
            "Fremdes-Dossier");
        var holdingPdf = new FixedHoldingListPdfService([1, 2, 3]);
        var service = new DossierComponentListExportService(
            holdingPdf,
            new FixedShaftListPdfService([4]));

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, otherDossierFolder));

        Assert.False(result.Success);
        Assert.Null(result.FilePath);
        Assert.Contains("ausgewählten Eigentümerdossier", result.Message, StringComparison.Ordinal);
        Assert.Empty(holdingPdf.Models);
        Assert.False(Directory.Exists(otherDossierFolder));
    }

    [Fact]
    public async Task Abbruch_wird_weitergegeben_und_schreibt_nichts()
    {
        using var temp = new TempDirectory();
        var targetFolder = Path.Combine(
            temp.Path,
            DossierFolderPlanner.DossierRootFolderName,
            "Musterweg-7");
        var holdingPdf = new FixedHoldingListPdfService([1, 2, 3]);
        var service = new DossierComponentListExportService(
            holdingPdf,
            new FixedShaftListPdfService([4]));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateHoldingListAsync(
                CreateRequest(temp.Path, targetFolder),
                cts.Token));

        Assert.Empty(holdingPdf.Models);
        Assert.False(Directory.Exists(targetFolder));
    }

    [Fact]
    public async Task Rendererfehler_steht_im_Resultat_und_hinterlaesst_keine_Temporaerdatei()
    {
        using var temp = new TempDirectory();
        var targetFolder = CreateDossierFolder(temp.Path);
        var service = new DossierComponentListExportService(
            new ThrowingHoldingListPdfService(),
            new FixedShaftListPdfService([4]));

        var result = await service.CreateHoldingListAsync(
            CreateRequest(temp.Path, targetFolder));

        Assert.False(result.Success);
        Assert.Null(result.FilePath);
        Assert.Contains("Testfehler", result.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(targetFolder));
    }

    private static DossierComponentListExportService CreateService(
        byte[]? holdingBytes = null,
        byte[]? shaftBytes = null)
        => new(
            new FixedHoldingListPdfService(holdingBytes ?? [1]),
            new FixedShaftListPdfService(shaftBytes ?? [2]),
            () => new DateTime(2026, 8, 28));

    private static DossierExportRequest CreateRequest(
        string projectRoot,
        string targetFolder)
    {
        var project = new Project();
        var dossier = new DossierDefinition
        {
            Name = "Musterweg 7",
            FolderName = "Musterweg-7",
            OwnerName = "Muster Eigentümer"
        };
        var snapshot = DossierSnapshotBuilder.Build(dossier, project, null, null);

        return new DossierExportRequest(
            project,
            projectRoot,
            new DossierAreaSettings(),
            dossier,
            snapshot,
            targetFolder);
    }

    private static string CreateDossierFolder(string projectRoot)
    {
        var path = Path.Combine(
            projectRoot,
            DossierFolderPlanner.DossierRootFolderName,
            "Musterweg-7");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedHoldingListPdfService(byte[] content)
        : IDossierHoldingListPdfService
    {
        public List<DossierHoldingListPdfModel> Models { get; } = new();

        public byte[] CreatePdf(DossierHoldingListPdfModel model)
        {
            Models.Add(model);
            return content;
        }
    }

    private sealed class ThrowingHoldingListPdfService : IDossierHoldingListPdfService
    {
        public byte[] CreatePdf(DossierHoldingListPdfModel model)
            => throw new InvalidOperationException("Testfehler des PDF-Renderers.");
    }

    private sealed class FixedShaftListPdfService(byte[] content)
        : IDossierShaftListPdfService
    {
        public List<DossierShaftListPdfModel> Models { get; } = new();

        public byte[] CreatePdf(DossierShaftListPdfModel model)
        {
            Models.Add(model);
            return content;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DossierComponentListExportTests_" + Guid.NewGuid().ToString("N"));
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
