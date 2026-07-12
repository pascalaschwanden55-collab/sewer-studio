using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

/// <summary>
/// Tests fuer ProjectRecovery (AP-01): rettet ein beschaedigtes Projekt aus .bak
/// oder Restore-Points und legt die kaputte Datei in Quarantaene, statt sie zu verlieren.
/// </summary>
public sealed class ProjectRecoveryTests
{
    private const string Muell = "{ das ist kein gueltiges json";

    private static string NeuerProjektOrdner()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"proj-recover-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void TryRecover_BakIntakt_LaedtAusBakUndQuarantaeniert()
    {
        var dir = NeuerProjektOrdner();
        var path = Path.Combine(dir, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            // Zweimal speichern -> beim zweiten Save entsteht projekt.json.bak mit gueltigem Inhalt.
            repo.Save(new Project(), path);
            repo.Save(new Project(), path);
            Assert.True(File.Exists(path + ".bak"), "Testvoraussetzung: .bak muss existieren");

            // Hauptdatei zerstoeren.
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.NotNull(result.Project);
            Assert.EndsWith(".bak", result.RecoveredFromPath);
            // Kaputte Datei ist in Quarantaene verschoben (nicht geloescht), Original weg.
            Assert.NotNull(result.QuarantinedPath);
            Assert.True(File.Exists(result.QuarantinedPath!), "Quarantaene-Datei muss existieren");
            Assert.Equal(Muell, File.ReadAllText(result.QuarantinedPath!));
            var quarantaene = Directory.GetFiles(dir, "projekt.corrupt-*.json");
            Assert.Single(quarantaene);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_AllesKaputt_KeineRettung_KeineAenderung()
    {
        var dir = NeuerProjektOrdner();
        var path = Path.Combine(dir, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            File.WriteAllText(path, Muell);
            File.WriteAllText(path + ".bak", Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.False(result.Recovered);
            Assert.Null(result.Project);
            Assert.Null(result.QuarantinedPath);
            // Nichts wurde veraendert: Hauptdatei liegt unveraendert da.
            Assert.Equal(Muell, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(dir, "projekt.corrupt-*.json"));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_KeinBakAberRestorePoint_LaedtAusRestorePoint()
    {
        var dir = NeuerProjektOrdner();
        var path = Path.Combine(dir, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            // Gueltigen Restore-Point anlegen (Struktur wie ProjectImportOrchestrator, AP-02).
            var rpDir = Path.Combine(dir, ProjectStructure.RestorePoints, "projekt", "20260712_120000");
            Directory.CreateDirectory(rpDir);
            repo.Save(new Project(), Path.Combine(rpDir, "projekt.json"));

            // Hauptdatei kaputt, kein .bak.
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.NotNull(result.Project);
            Assert.Contains(ProjectStructure.RestorePoints, result.RecoveredFromPath);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_BevorzugtBakVorRestorePoint()
    {
        var dir = NeuerProjektOrdner();
        var path = Path.Combine(dir, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            repo.Save(new Project(), path);
            repo.Save(new Project(), path); // erzeugt .bak
            var rpDir = Path.Combine(dir, ProjectStructure.RestorePoints, "projekt", "20260712_120000");
            Directory.CreateDirectory(rpDir);
            repo.Save(new Project(), Path.Combine(rpDir, "projekt.json"));
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.EndsWith(".bak", result.RecoveredFromPath);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_BevorzugtNeuerenRestorePointVorAelteremBak()
    {
        var dir = NeuerProjektOrdner();
        var path = Path.Combine(dir, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            repo.Save(new Project { Name = "Altes Bak" }, path + ".bak");
            File.SetLastWriteTimeUtc(
                path + ".bak",
                new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc));

            var rpDir = Path.Combine(dir, ProjectStructure.RestorePoints, "projekt");
            Directory.CreateDirectory(rpDir);
            repo.Save(
                new Project { Name = "Neuer Restore-Point" },
                Path.Combine(rpDir, "20260712-130000000_projekt.json"));
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.Equal("Neuer Restore-Point", result.Project!.Name);
            Assert.Contains("20260712-130000000_projekt.json", result.RecoveredFromPath);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_FlacherRestorePointAusSpeicherworkflow_WirdGefunden()
    {
        var dir = NeuerProjektOrdner();
        var projectFiles = Path.Combine(dir, ProjectStructure.Projektdateien);
        Directory.CreateDirectory(projectFiles);
        var path = Path.Combine(projectFiles, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            var rpDir = Path.Combine(dir, ProjectStructure.RestorePoints, "projekt");
            Directory.CreateDirectory(rpDir);
            repo.Save(new Project { Name = "Rettung" }, Path.Combine(rpDir, "20260712-120000000_projekt.json"));
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.Equal("Rettung", result.Project!.Name);
            Assert.Contains("20260712-120000000_projekt.json", result.RecoveredFromPath);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void TryRecover_WaehltNeuestenStandUeberAltesUndNeuesFormatHinweg()
    {
        var dir = NeuerProjektOrdner();
        var projectFiles = Path.Combine(dir, ProjectStructure.Projektdateien);
        Directory.CreateDirectory(projectFiles);
        var path = Path.Combine(projectFiles, "projekt.json");
        var repo = new JsonProjectRepository();
        try
        {
            var rpBase = Path.Combine(dir, ProjectStructure.RestorePoints, "projekt");
            var oldFolder = Path.Combine(rpBase, "20260712_120000");
            Directory.CreateDirectory(oldFolder);
            repo.Save(new Project { Name = "Alt" }, Path.Combine(oldFolder, "projekt.json"));

            Directory.CreateDirectory(rpBase);
            repo.Save(
                new Project { Name = "Neu" },
                Path.Combine(rpBase, "20260712-130000000_projekt.json"));
            File.WriteAllText(path, Muell);

            var result = ProjectRecovery.TryRecover(path, repo);

            Assert.True(result.Recovered);
            Assert.Equal("Neu", result.Project!.Name);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
