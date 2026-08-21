using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class NameBasedProtocolDistributorTests
{
    [Fact]
    public void Distribute_liest_vorbereitetes_Archiv_und_veroeffentlicht_noch_nichts()
    {
        var root = NewTempDir();
        var source = NewTempDir();
        try
        {
            var projectFolder = Path.Combine(root, "Projekt");
            var projectPath = Path.Combine(projectFolder, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
            File.WriteAllText(projectPath, "{}");
            var sourcePdf = Path.Combine(source, "H_33390-36268.pdf");
            File.WriteAllText(sourcePdf, "x");
            var archive = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            using var staging = new ImportFileStagingService().Begin(projectPath)!;
            staging.StageCopy(sourcePdf, archive);
            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(
                project,
                projectFolder,
                archive,
                collectionLock: null,
                fileStaging: staging);

            Assert.Equal(1, report.HaltungProtokolle);
            var relative = project.Data[0].GetFieldValue("PDF_Path");
            Assert.False(string.IsNullOrWhiteSpace(relative));
            var logicalTarget = Path.Combine(projectFolder, relative!);
            Assert.False(File.Exists(logicalTarget));
            Assert.True(File.Exists(staging.ResolveReadPath(logicalTarget)));
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(source, true);
        }
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbpd_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static HaltungRecord Haltung(string name)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void Distribute_verteilt_haltung_und_legt_schacht_an()
    {
        var projectFolder = NewTempDir();
        var source = NewTempDir();
        try
        {
            // Quelle: 1 Haltungs-PDF (vertauschte Reihenfolge!) + 1 Schacht-PDF + 1 Nicht-Protokoll.
            File.WriteAllText(Path.Combine(source, "H_36268-33390.pdf"), "x"); // Projekt hat 33390-36268
            File.WriteAllText(Path.Combine(source, "S_27581.pdf"), "x");
            File.WriteAllText(Path.Combine(source, "Haltungsliste.pdf"), "x");

            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);

            // Haltung: PDF verteilt (trotz vertauschter Reihenfolge) + PDF_Path gesetzt.
            Assert.Equal(1, report.HaltungProtokolle);
            Assert.False(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
            Assert.True(Directory.EnumerateFiles(
                Path.Combine(projectFolder, "Haltungen_Verteilt"), "*.pdf", SearchOption.AllDirectories).Any());

            // Schacht: neu angelegt + verteilt.
            Assert.Equal(1, report.SchachtProtokolle);
            Assert.Equal(1, report.SchaechteAngelegt);
            var schacht = project.SchaechteData.Single();
            Assert.Equal("27581", schacht.GetFieldValue("Schachtnummer"));
            Assert.False(string.IsNullOrWhiteSpace(schacht.GetFieldValue("PDF_Path")));

            // Nicht-Protokoll ignoriert, keine „nicht zugeordnet".
            Assert.Empty(report.NichtZugeordnet);

            // Idempotent: zweiter Lauf legt keinen zweiten Schacht an.
            var report2 = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);
            Assert.Equal(0, report2.SchaechteAngelegt);
            Assert.Single(project.SchaechteData);
        }
        finally
        {
            Directory.Delete(projectFolder, true);
            Directory.Delete(source, true);
        }
    }

    [Fact]
    public void Distribute_meldet_nicht_zuordenbare_haltung()
    {
        var projectFolder = NewTempDir();
        var source = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(source, "H_99999-88888.pdf"), "x"); // kein passender Record
            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);

            Assert.Equal(0, report.HaltungProtokolle);
            Assert.Contains(report.NichtZugeordnet, s => s.Contains("H_99999-88888"));
        }
        finally
        {
            Directory.Delete(projectFolder, true);
            Directory.Delete(source, true);
        }
    }

    [Fact]
    public void Distribute_matcht_haltung_trotz_ibak_prefix_im_projektnamen()
    {
        var projectFolder = NewTempDir();
        var source = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(source, "33390-36268.pdf"), "x"); // bare Name
            var project = new Project();
            project.Data.Add(Haltung("H_33390-36268")); // Projektname mit IBAK-Prefix

            var report = new NameBasedProtocolDistributor().Distribute(project, projectFolder, source);

            Assert.Equal(1, report.HaltungProtokolle);        // NormalizeIbak entfernt das H_ -> Treffer
            Assert.Empty(report.NichtZugeordnet);
            Assert.False(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
        }
        finally
        {
            Directory.Delete(projectFolder, true);
            Directory.Delete(source, true);
        }
    }

    [JunctionFact]
    public void Distribute_BetrittKeineVerzeichnisverknuepfungImQuellbaum()
    {
        var root = NewTempDir();
        var projectFolder = Path.Combine(root, "Projekt");
        var source = Path.Combine(root, "Quelle");
        var external = Path.Combine(root, "Fremd");
        var link = Path.Combine(source, "verknuepft");
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(external);
        var externalPdf = Path.Combine(external, "H_33390-36268.pdf");
        File.WriteAllText(externalPdf, "fremdes protokoll");
        JunctionTestSupport.CreateDirectoryLink(link, external);

        try
        {
            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(
                project,
                projectFolder,
                source);

            Assert.Equal(0, report.HaltungProtokolle);
            Assert.True(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
            Assert.Equal("fremdes protokoll", File.ReadAllText(externalPdf));
        }
        finally
        {
            try
            {
                if (Directory.Exists(link))
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }

    [JunctionFact]
    public void Distribute_SchreibtNichtDurchVerknuepftenHaltungsZielordner()
    {
        var root = NewTempDir();
        var projectFolder = Path.Combine(root, "Projekt");
        var source = Path.Combine(root, "Quelle");
        var external = Path.Combine(root, "Fremdziel");
        var holdingRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
        var holdingLink = Path.Combine(holdingRoot, "33390-36268");
        Directory.CreateDirectory(holdingRoot);
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(source, "H_33390-36268.pdf"), "kundenprotokoll");
        JunctionTestSupport.CreateDirectoryLink(holdingLink, external);

        try
        {
            var project = new Project();
            project.Data.Add(Haltung("33390-36268"));

            var report = new NameBasedProtocolDistributor().Distribute(
                project,
                projectFolder,
                source);

            Assert.Equal(0, report.HaltungProtokolle);
            Assert.NotEmpty(report.Meldungen);
            Assert.Empty(Directory.EnumerateFiles(external));
            Assert.True(string.IsNullOrWhiteSpace(project.Data[0].GetFieldValue("PDF_Path")));
        }
        finally
        {
            try
            {
                if (Directory.Exists(holdingLink))
                    Directory.Delete(holdingLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }
}
