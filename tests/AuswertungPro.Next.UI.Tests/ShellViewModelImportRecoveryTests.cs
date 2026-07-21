using System;
using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Integrationstest der Recovery-Verdrahtung: Ein durch Prozess-Absturz unterbrochener
/// Import (Marker vorhanden, projekt.json ohne passende Commit-TxId) wird beim naechsten
/// Projekt-Laden ueber ShellViewModel.TryOpenProject alles-oder-nichts zurueckgenommen.
/// </summary>
public sealed class ShellViewModelImportRecoveryTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shell-rec-" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    [Fact]
    public void TryOpenProject_nimmt_unvollstaendigen_Import_zurueck_und_entfernt_Marker()
    {
        using var temp = new TempDir();
        var projectFile = System.IO.Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(projectFile)!);

        var repo = new JsonProjectRepository();
        // projekt.json OHNE Commit-TxId -> der offene Marker traegt eine andere -> Rollback.
        Assert.True(repo.Save(new Project { Name = "Recovery" }, projectFile).Ok);

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFile)!;
        Assert.False(string.IsNullOrWhiteSpace(projectRoot));

        // Eine "veroeffentlichte" Importdatei + Marker einer unterbrochenen Transaktion.
        var published = System.IO.Path.Combine(projectRoot, "importiert.txt");
        File.WriteAllText(published, "importierter-inhalt");
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(published)));

        var journal = new FileImportTransactionJournal();
        journal.Begin(projectRoot, new ImportTransactionMarker(
            TxId: "tx-offen",
            StartedUtc: DateTime.UtcNow,
            Label: "PDF",
            StagingRoot: System.IO.Path.Combine(projectRoot, ".import-staging"),
            PublishedTargets: [new PublishedFileInfo("importiert.txt", sha)],
            RestorePointPath: null));

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));

        var opened = shell.TryOpenProject(projectFile);

        Assert.True(opened);
        Assert.False(File.Exists(published));            // veroeffentlichte Datei zurueckgerollt
        Assert.Null(journal.TryRead(projectRoot));       // Marker entfernt
    }
}
