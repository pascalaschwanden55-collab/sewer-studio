using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierFileStoreTests : IDisposable
{
    private readonly string _projectRoot = Path.Combine(
        Path.GetTempPath(), "dossier_store_" + Guid.NewGuid().ToString("N"));

    public DossierFileStoreTests() => Directory.CreateDirectory(_projectRoot);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectRoot))
                Directory.Delete(_projectRoot, recursive: true);
        }
        catch
        {
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Erstlauf_ohne_Datei_ergibt_ein_leeres_Dokument()
    {
        var store = new DossierFileStore();

        var document = await store.LoadAsync(_projectRoot);

        Assert.Empty(document.Dossiers);
        Assert.Equal(1, document.SchemaVersion);
    }

    [Fact]
    public async Task Speichern_und_Laden_erhaelt_die_Auswahl()
    {
        var store = new DossierFileStore();
        var id = Guid.NewGuid();

        var document = new DossierDocument
        {
            Area = new DossierAreaSettings { AreaTitle = "Erstfeld West" },
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Brämenhofstatt 3+4",
                    ParcelNumbers = "762+756",
                    HoldingIds = { id },
                    Status = DossierStatus.Versendet
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);
        var geladen = await store.LoadAsync(_projectRoot);

        var dossier = Assert.Single(geladen.Dossiers);
        Assert.Equal("Brämenhofstatt 3+4", dossier.Name);
        Assert.Equal("762+756", dossier.ParcelNumbers);
        Assert.Equal(id, Assert.Single(dossier.HoldingIds));
        Assert.Equal(DossierStatus.Versendet, dossier.Status);
        Assert.Equal("Erstfeld West", geladen.Area.AreaTitle);
    }

    [Fact]
    public async Task Zweites_Speichern_legt_ein_Backup_an()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument();

        await store.SaveAsync(_projectRoot, document);
        document.Dossiers.Add(new DossierDefinition { Name = "Zweiter Stand" });
        await store.SaveAsync(_projectRoot, document);

        var backup = DossierFolderPlanner.ResolveDocumentPath(_projectRoot) + ".bak";
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public async Task Kaputte_Datei_wird_aus_dem_Backup_gerettet()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument
        {
            Dossiers = { new DossierDefinition { Name = "Guter Stand" } }
        };

        await store.SaveAsync(_projectRoot, document);

        var path = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        File.Copy(path, path + ".bak", overwrite: true);
        await File.WriteAllTextAsync(path, "{ das ist kein JSON");

        var geladen = await store.LoadAsync(_projectRoot);

        Assert.Equal("Guter Stand", Assert.Single(geladen.Dossiers).Name);
    }

    [Fact]
    public async Task Kaputte_Datei_ohne_Backup_bricht_ab_statt_leer_weiterzumachen()
    {
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        var path = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        await File.WriteAllTextAsync(path, "{ kaputt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.LoadAsync(_projectRoot));

        // Nichts wurde ueberschrieben — die Originaldatei liegt unveraendert da.
        Assert.Equal("{ kaputt", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Neuere_Formatversion_wird_nicht_erraten()
    {
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            DossierFolderPlanner.ResolveDocumentPath(_projectRoot),
            """{ "SchemaVersion": 99, "Dossiers": [] }""");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.LoadAsync(_projectRoot));
    }
}

public sealed class DossierPdfAssemblyServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "dossier_pdf_" + Guid.NewGuid().ToString("N"));

    public DossierPdfAssemblyServiceTests() => Directory.CreateDirectory(_folder);

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
    public async Task Ohne_Word_Datei_wird_klar_gemeldet_statt_ein_Teil_PDF_zu_bauen()
    {
        var service = new DossierPdfAssemblyService(
            new FakeMerge(), (_, _) => true);

        var result = await service.AssembleAsync(_folder);

        Assert.False(result.Success);
        Assert.Contains("Word erzeugen", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_installiertes_Word_entsteht_kein_unvollstaendiges_PDF()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var service = new DossierPdfAssemblyService(
            new FakeMerge(), (_, _) => false);

        var result = await service.AssembleAsync(_folder);

        Assert.False(result.Success);
        Assert.Contains("Microsoft Word", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(
            Path.Combine(_folder, DossierFolderPlanner.CombinedPdfFileName)));
    }

    [Fact]
    public async Task Fuehrt_Word_PDF_und_Beilagen_in_Dateinamen_Reihenfolge_zusammen()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var beilagen = Path.Combine(_folder, DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(beilagen);
        await File.WriteAllTextAsync(Path.Combine(beilagen, "02_TV_zweite.pdf"), "b");
        await File.WriteAllTextAsync(Path.Combine(beilagen, "01_Uebersichtsplan.pdf"), "a");

        var merge = new FakeMerge();
        var service = new DossierPdfAssemblyService(
            merge,
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, new byte[] { 1, 2, 3 });
                return true;
            });

        var result = await service.AssembleAsync(_folder);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(
            Path.Combine(_folder, DossierFolderPlanner.CombinedPdfFileName)));

        Assert.Equal(2, merge.LastOriginals.Count);
        Assert.EndsWith("01_Uebersichtsplan.pdf", merge.LastOriginals[0], StringComparison.Ordinal);
        Assert.EndsWith("02_TV_zweite.pdf", merge.LastOriginals[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nimmt_die_zuletzt_bearbeitete_Word_Datei()
    {
        var alt = Path.Combine(_folder, "Eigentuemerdossier.docx");
        var neu = Path.Combine(_folder, "Eigentuemerdossier-2.docx");
        await File.WriteAllTextAsync(alt, "alt");
        await File.WriteAllTextAsync(neu, "neu");
        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(neu, DateTime.UtcNow);

        string? verwendet = null;
        var service = new DossierPdfAssemblyService(
            new FakeMerge(),
            (wordPath, pdfPath) =>
            {
                verwendet = wordPath;
                File.WriteAllBytes(pdfPath!, new byte[] { 1 });
                return true;
            });

        await service.AssembleAsync(_folder);

        Assert.Equal(neu, verwendet);
    }

    private sealed class FakeMerge : IPdfMergeService
    {
        public List<string> LastOriginals { get; private set; } = new();

        public byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
        {
            LastOriginals = new List<string>(originalPdfPaths);
            return generatedPdf;
        }

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
        {
            LastOriginals = new List<string>(originalPdfPaths);
            return Array.Empty<byte>();
        }
    }
}
