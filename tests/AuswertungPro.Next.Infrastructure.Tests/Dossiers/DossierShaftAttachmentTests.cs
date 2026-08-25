using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

/// <summary>
/// Die Schachtprotokolle als Beilage.
///
/// Gehoert ein Kontrollschacht zur Liegenschaft und wird er saniert, dann
/// fehlte dem Eigentuemer bisher genau sein Protokoll: gesammelt wurden nur
/// die Haltungen.
/// </summary>
public sealed class DossierShaftAttachmentTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_schacht_" + Guid.NewGuid().ToString("N"));

    private readonly string _projectRoot;
    private readonly string _dossierFolder;

    public DossierShaftAttachmentTests()
    {
        _projectRoot = Path.Combine(_root, "Projekt");
        _dossierFolder = Path.Combine(_projectRoot, "Dossiers", "Musterweg");
        Directory.CreateDirectory(_dossierFolder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private DossierExportRequest Szenario(params string[] schachtnummern)
    {
        var haltung = new HaltungRecord();
        haltung.Fields[FieldKeys.HoldingName] = "100-200";

        var project = new Project();
        project.Data.Add(haltung);

        var dossier = new DossierDefinition { Name = "Musterweg", HoldingIds = { haltung.Id } };

        foreach (var nummer in schachtnummern)
        {
            var schacht = new SchachtRecord();
            schacht.SetFieldValue("Schachtnummer", nummer);
            project.SchaechteData.Add(schacht);
            dossier.ShaftNumbers.Add(nummer);
        }

        return new DossierExportRequest(
            project,
            _projectRoot,
            new DossierAreaSettings(),
            dossier,
            DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore()),
            _dossierFolder);
    }

    private DossierAttachmentCollector Sammler(Dictionary<string, string> schachtPdfs)
        => new(new LeerLocator(schachtPdfs), new LeerProtokoll());

    [Fact]
    public async Task Das_Schachtprotokoll_wird_mitgenommen()
    {
        var pdf = Path.Combine(_root, "80551.pdf");
        await File.WriteAllTextAsync(pdf, "Schachtprotokoll");

        var ergebnis = await Sammler(new() { ["80551"] = pdf })
            .CollectAsync(Szenario("80551"));

        var beilage = Assert.Single(
            ergebnis.Attachments.Where(a => a.HoldingName == "80551"));

        Assert.Equal(DossierAttachmentKind.OriginalProtocol, beilage.Kind);
        Assert.True(File.Exists(beilage.SourcePath));
        Assert.Equal("Schachtprotokoll", await File.ReadAllTextAsync(beilage.SourcePath));
    }

    [Fact]
    public async Task Die_Vorschau_sammelt_das_aktuelle_Schachtprotokoll_nur_in_Temp()
    {
        var pdf = Path.Combine(_root, "80551.pdf");
        await File.WriteAllTextAsync(pdf, "Aktuelles Schachtprotokoll");
        var request = Szenario("80551");
        var temporaeresDossier = Path.Combine(_root, "Vorschau", "Dossier");

        var ergebnis = await Sammler(new() { ["80551"] = pdf })
            .CollectIntoTemporaryAsync(request, temporaeresDossier);

        var beilage = ergebnis.Attachments.Single(a => a.HoldingName == "80551");
        Assert.StartsWith(
            Path.GetFullPath(temporaeresDossier),
            beilage.SourcePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "Aktuelles Schachtprotokoll",
            await File.ReadAllTextAsync(beilage.SourcePath));
        Assert.Equal("Aktuelles Schachtprotokoll", await File.ReadAllTextAsync(pdf));
    }

    [Fact]
    public async Task Die_Schaechte_stehen_hinter_den_Leitungen()
    {
        var pdf = Path.Combine(_root, "80551.pdf");
        await File.WriteAllTextAsync(pdf, "x");

        var ergebnis = await Sammler(new() { ["80551"] = pdf })
            .CollectAsync(Szenario("80551"));

        // Die Nummerierung laeuft durch: erst die Leitung, dann der Schacht.
        var schacht = ergebnis.Attachments.Single(a => a.HoldingName == "80551");
        Assert.StartsWith("02_", schacht.FileName, StringComparison.Ordinal);
        Assert.Contains("Schacht", schacht.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ein_fehlendes_Schachtprotokoll_wird_ehrlich_gemeldet()
    {
        // Kein stilles Weglassen: sonst merkt niemand, dass die Beilage fehlt.
        var ergebnis = await Sammler(new()).CollectAsync(Szenario("80551"));

        var beilage = ergebnis.Attachments.Single(a => a.HoldingName == "80551");

        Assert.Equal(DossierAttachmentKind.Missing, beilage.Kind);
        Assert.Contains(
            ergebnis.Warnings,
            w => w.Contains("80551", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ohne_Schaechte_bleibt_alles_wie_bisher()
    {
        var ergebnis = await Sammler(new()).CollectAsync(Szenario());

        Assert.Single(ergebnis.Attachments);
        Assert.Equal("100-200", ergebnis.Attachments[0].HoldingName);
    }

    /// <summary>Kennt nur Schachtprotokolle — Haltungen bleiben absichtlich leer.</summary>
    private sealed class LeerLocator : IInspectionProtocolFileLocator
    {
        private readonly Dictionary<string, string> _schachtPdfs;

        public LeerLocator(Dictionary<string, string> schachtPdfs) => _schachtPdfs = schachtPdfs;

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
            => new();

        public string? ResolveExistingPath(string? raw, string? projectPath) => null;

        public string? FindProtocolPath(
            HaltungRecord record, string? resolvedLink, string? initialFolder,
            string? projectPath, string? storedFilesRaw) => null;

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder) { }

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht, string projectFolder, List<string> paths)
        {
            var nummer = schacht.GetFieldValue("Schachtnummer") ?? "";
            if (_schachtPdfs.TryGetValue(nummer, out var pfad))
                paths.Add(pfad);
        }
    }

    /// <summary>Kein eigenes Protokoll — nur die echten Beilagen sollen zaehlen.</summary>
    private sealed class LeerProtokoll : IProtocolPdfExporter
    {
        public byte[] BuildPdf(string projectTitle, ProtocolDocument document, string projectRootAbs)
            => Array.Empty<byte>();

        public byte[] BuildPdf(
            string projectTitle, ProtocolDocument document, string projectRootAbs,
            ProtocolPdfExportOptions options)
            => Array.Empty<byte>();

        public byte[] BuildHaltungsprotokollPdf(
            Project project, HaltungRecord record, ProtocolDocument document,
            string projectRootAbs, HaltungsprotokollPdfOptions? options = null)
            => Array.Empty<byte>();

        public byte[] BuildCsv(ProtocolDocument document, ProtocolPdfExportOptions? options = null)
            => Array.Empty<byte>();
    }
}
