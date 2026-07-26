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

    [Fact]
    public void TryOpenProject_sperrt_beschaedigten_Marker_ohne_aktuelles_Projekt_zu_ersetzen()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        Assert.True(new JsonProjectRepository()
            .Save(new Project { Name = "Darf nicht geladen werden" }, projectFile)
            .Ok);
        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFile)!;
        var markerPath = Path.Combine(projectRoot, FileImportTransactionJournal.MarkerFileName);
        File.WriteAllText(markerPath, "{ kaputt");

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var dialogs = new DialogFake();
        services.Dialogs = dialogs;
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        shell.Project.Name = "Aktuelles Projekt";

        var opened = shell.TryOpenProject(projectFile);

        Assert.False(opened);
        Assert.Equal("Aktuelles Projekt", shell.Project.Name);
        Assert.Contains("nicht sicher gelesen", dialogs.LastErrorMessage, StringComparison.Ordinal);
        Assert.Equal("{ kaputt", File.ReadAllText(markerPath));
    }

    private sealed class DialogFake : IDialogService
    {
        public string LastErrorMessage { get; private set; } = string.Empty;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => LastErrorMessage = message;
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.No;
    }
}
