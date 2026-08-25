using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierAttachmentCollectorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_beilagen_" + Guid.NewGuid().ToString("N"));

    private readonly string _projectRoot;
    private readonly string _dossierFolder;

    public DossierAttachmentCollectorTests()
    {
        _projectRoot = Path.Combine(_root, "Projekt");
        _dossierFolder = Path.Combine(_projectRoot, "Dossiers", "Braemenhofstatt");
        Directory.CreateDirectory(_dossierFolder);
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
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Nimmt_das_Original_PDF_des_Kanalunternehmers()
    {
        var original = Path.Combine(_root, "36080-36086_Fretz.pdf");
        await File.WriteAllTextAsync(original, "Original");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>> { ["36080-36086"] = new() { original } });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.OriginalProtocol, attachment.Kind);
        Assert.True(File.Exists(attachment.SourcePath));
        Assert.Equal("Original", await File.ReadAllTextAsync(attachment.SourcePath));
        Assert.StartsWith("01_TV_", attachment.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Faellt_auf_das_eigene_Protokoll_zurueck_wenn_kein_Original_da_ist()
    {
        var (request, record) = BuildScenario(originals: new());
        record.Protocol = new ProtocolDocument();

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.GeneratedProtocol, attachment.Kind);
        Assert.True(File.Exists(attachment.SourcePath));
        Assert.StartsWith("01_Protokoll_", attachment.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Original_und_ohne_Protokoll_wird_die_Luecke_gemeldet()
    {
        var (request, record) = BuildScenario(originals: new());
        record.Protocol = null;

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.Missing, attachment.Kind);
        Assert.Equal(1, result.MissingCount);
        Assert.Contains(result.Warnings, w => w.Contains("fehlt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Von_Hand_hinzugelegte_Beilagen_bleiben_erhalten()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Original");

        var eigene = Path.Combine(
            _dossierFolder, DossierFolderPlanner.AttachmentFolderName, "00_QGIS_Plan.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(eigene)!);
        await File.WriteAllTextAsync(eigene, "Mein Plan");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>> { ["36080-36086"] = new() { original } });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        await collector.CollectAsync(request);

        Assert.True(File.Exists(eigene));
        Assert.Equal("Mein Plan", await File.ReadAllTextAsync(eigene));
    }

    [Fact]
    public async Task Vorschau_sammelt_aktuell_in_Temp_und_laesst_den_Dossierordner_unveraendert()
    {
        var original = Path.Combine(_root, "aktuelles_protokoll.pdf");
        await File.WriteAllTextAsync(original, "Aktuelles Original");

        var echteBeilagen = Path.Combine(
            _dossierFolder,
            DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(echteBeilagen);
        var alterStand = Path.Combine(echteBeilagen, "01_TV_36080-36086.pdf");
        var manuelleBeilage = Path.Combine(echteBeilagen, "00_QGIS_Plan.pdf");
        await File.WriteAllTextAsync(alterStand, "Alter Stand");
        await File.WriteAllTextAsync(manuelleBeilage, "Manuelle Beilage");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var temporaeresDossier = Path.Combine(_root, "Vorschau", "Dossier");

        var result = await collector.CollectIntoTemporaryAsync(
            request,
            temporaeresDossier);

        var attachment = Assert.Single(result.Attachments);
        Assert.StartsWith(
            Path.GetFullPath(temporaeresDossier),
            attachment.SourcePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Aktuelles Original", await File.ReadAllTextAsync(attachment.SourcePath));
        Assert.Equal("Alter Stand", await File.ReadAllTextAsync(alterStand));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(manuelleBeilage));
        Assert.Equal("Aktuelles Original", await File.ReadAllTextAsync(original));

        var temporaererPlan = Path.Combine(
            temporaeresDossier,
            DossierFolderPlanner.AttachmentFolderName,
            Path.GetFileName(manuelleBeilage));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(temporaererPlan));
    }

    [Fact]
    public async Task Vorschau_weist_ein_Schreibziel_ausserhalb_des_System_Temp_Ordners_ab()
    {
        var (request, _) = BuildScenario(originals: new());
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var laufwerkswurzel = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        var unsicheresZiel = Path.Combine(
            laufwerkswurzel,
            "SewerStudio_DossierPreview_Unsicher_" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            collector.CollectIntoTemporaryAsync(request, unsicheresZiel));

        Assert.False(Directory.Exists(unsicheresZiel));
    }

    [Fact]
    public async Task Mehrere_Treffer_melden_welches_PDF_verwendet_wurde()
    {
        var a = Path.Combine(_root, "a.pdf");
        var b = Path.Combine(_root, "b.pdf");
        await File.WriteAllTextAsync(a, "A");
        await File.WriteAllTextAsync(b, "B");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { a, b }
            });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        Assert.Contains(result.Warnings, w => w.Contains("2 Protokoll-PDFs", StringComparison.Ordinal));
    }

    private (DossierExportRequest Request, HaltungRecord Record) BuildScenario(
        Dictionary<string, List<string>> originals)
    {
        var record = new HaltungRecord();
        record.Fields[FieldKeys.HoldingName] = "36080-36086";
        record.Fields[FieldKeys.HoldingLengthMeters] = "41.70";

        var project = new Project();
        project.Data.Add(record);

        var dossier = new DossierDefinition
        {
            Name = "Brämenhofstatt",
            HoldingIds = { record.Id }
        };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        var request = new DossierExportRequest(
            project,
            _projectRoot,
            new DossierAreaSettings(),
            dossier,
            snapshot,
            _dossierFolder);

        _originals = originals;
        return (request, record);
    }

    private Dictionary<string, List<string>> _originals = new();

    private sealed class FakeLocator : IInspectionProtocolFileLocator
    {
        private readonly Dictionary<string, List<string>> _lookup;

        public FakeLocator(Dictionary<string, List<string>> lookup) => _lookup = lookup;

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
        {
            var name = record.GetFieldValue(FieldKeys.HoldingName) ?? "";
            return _lookup.TryGetValue(name, out var paths) ? paths : new List<string>();
        }

        public string? ResolveExistingPath(string? raw, string? projectPath) => null;

        public string? FindProtocolPath(
            HaltungRecord record,
            string? resolvedLink,
            string? initialFolder,
            string? projectPath,
            string? storedFilesRaw) => null;

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder) { }

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht, string projectFolder, List<string> paths) { }
    }

    private sealed class FakeProtocolPdf : IProtocolPdfExporter
    {
        public byte[] BuildPdf(string projectTitle, ProtocolDocument document, string projectRootAbs)
            => new byte[] { 1, 2, 3 };

        public byte[] BuildPdf(
            string projectTitle,
            ProtocolDocument document,
            string projectRootAbs,
            ProtocolPdfExportOptions options) => new byte[] { 1, 2, 3 };

        public byte[] BuildHaltungsprotokollPdf(
            Project project,
            HaltungRecord record,
            ProtocolDocument document,
            string projectRootAbs,
            HaltungsprotokollPdfOptions? options = null) => new byte[] { 1, 2, 3 };

        public byte[] BuildCsv(ProtocolDocument document, ProtocolPdfExportOptions? options = null)
            => Array.Empty<byte>();
    }
}
