using System;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Tests fuer den Restore-Point-Schritt des Ein-Knopf-Imports (AP-02).
/// Der Restore-Point muss die projekt.json finden, egal ob sie im Projekt-Root
/// (Alt-Projekte) oder im Unterordner "Projektdateien" (neue Projekte) liegt.
/// Der Restore-Point-Schritt laeuft vor der Formaterkennung, daher genuegt ein
/// leerer Quellordner — der Import bricht danach als Unknown ab, der Restore-Point
/// ist aber bereits angelegt.
/// </summary>
public sealed class ProjectImportOrchestratorRestorePointTests
{
    private static ProjectImportOrchestrator NewOrchestrator()
        => new(new XtfImportServiceAdapter(), new WinCanDbImportService());

    private static string[] RestorePointDateien(string projectDir)
    {
        var restoreRoot = Path.Combine(projectDir, ProjectStructure.RestorePoints, "projekt");
        return Directory.Exists(restoreRoot)
            ? Directory.GetFiles(restoreRoot, "projekt.json", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    [Fact]
    public void Import_ProjektImProjektdateienUnterordner_LegtRestorePointAn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-rp-sub-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(Path.Combine(projectDir, ProjectFileLocator.ProjektdateienDir));
        // Neue Projektstruktur: projekt.json liegt unter Projektdateien\
        var projektJson = Path.Combine(projectDir, ProjectFileLocator.ProjektdateienDir, "projekt.json");
        File.WriteAllText(projektJson, "{\"Version\":2}");

        try
        {
            var project = new Project();
            NewOrchestrator().Import(sourceDir, projectDir, project);

            var kopien = RestorePointDateien(projectDir);
            Assert.True(kopien.Length > 0,
                "Restore-Point muss auch fuer projekt.json unter Projektdateien\\ angelegt werden");
            Assert.Equal("{\"Version\":2}", File.ReadAllText(kopien[0]));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_ProjektImRoot_LegtRestorePointAn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-rp-root-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(projectDir);
        // Alt-Projektstruktur: projekt.json direkt im Root
        File.WriteAllText(Path.Combine(projectDir, "projekt.json"), "{\"Version\":2}");

        try
        {
            var project = new Project();
            NewOrchestrator().Import(sourceDir, projectDir, project);

            Assert.True(RestorePointDateien(projectDir).Length > 0,
                "Restore-Point muss fuer projekt.json im Root (Alt-Projekt) angelegt werden");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_OhneProjektDatei_MeldetUebersprungenenRestorePoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-rp-none-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(projectDir);
        // Keine projekt.json vorhanden (neues, noch nie gespeichertes Projekt)

        try
        {
            var project = new Project();
            var result = NewOrchestrator().Import(sourceDir, projectDir, project);

            Assert.Empty(RestorePointDateien(projectDir));
            Assert.Contains(result.Messages,
                m => m.Contains("Restore-Point", StringComparison.OrdinalIgnoreCase)
                     && m.Contains("keine projekt.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
