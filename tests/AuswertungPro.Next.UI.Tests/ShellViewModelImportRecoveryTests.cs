using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
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

    [Fact]
    public void TryOpenProject_stellt_gueltige_sicherung_trotz_importblock_fuer_naechsten_versuch_bereit()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        var repo = new JsonProjectRepository();
        Assert.True(repo.Save(new Project { Name = "Gueltige Sicherung" }, projectFile).Ok);
        Assert.True(repo.Save(new Project { Name = "Spaeterer Stand" }, projectFile).Ok);
        Assert.True(File.Exists(projectFile + ".bak"));
        File.WriteAllText(projectFile, "{ kaputte projektdatei");

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFile)!;
        var blockedTarget = Path.Combine(projectRoot, "importiert.txt");
        File.WriteAllText(blockedTarget, "importierter-inhalt");
        var journal = new FileImportTransactionJournal();
        journal.Begin(projectRoot, new ImportTransactionMarker(
            TxId: "tx-blockiert-nach-projektrettung",
            StartedUtc: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: Path.Combine(projectRoot, ".import-staging"),
            PublishedTargets:
            [
                new PublishedFileInfo(
                    "importiert.txt",
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(blockedTarget))))
            ],
            RestorePointPath: null));
        File.WriteAllText(blockedTarget, "nach-import-bearbeitet");

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

        var opened = shell.TryOpenProject(projectFile);

        Assert.False(opened);
        var quarantine = Assert.Single(Directory.GetFiles(
            Path.GetDirectoryName(projectFile)!,
            "projekt.corrupt-*.json"));
        Assert.Equal("{ kaputte projektdatei", File.ReadAllText(quarantine));
        var restored = repo.Load(projectFile);
        Assert.True(restored.Ok, restored.ErrorMessage);
        Assert.Equal("Gueltige Sicherung", restored.Value!.Name);
        Assert.Equal("nach-import-bearbeitet", File.ReadAllText(blockedTarget));
        Assert.NotNull(journal.TryRead(projectRoot));
        Assert.Contains(
            "Im Projektordner wurde bereits etwas veraendert",
            dialogs.LastErrorMessage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Das Projekt wurde nicht geoeffnet und nicht veraendert.",
            dialogs.LastErrorMessage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "nichts veraendert",
            dialogs.LastErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "nichts verändert",
            dialogs.LastErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "nicht verändert",
            dialogs.LastErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, dialogs.WarningCount);

        Assert.True(journal.ClearIfOwned(projectRoot, "tx-blockiert-nach-projektrettung"));
        Assert.True(shell.TryOpenProject(projectFile));
        Assert.Equal("Gueltige Sicherung", shell.Project.Name);
    }

    /// <summary>
    /// Der beruhigende Nachsatz "nicht veraendert" darf nur stehen, wenn die
    /// Wiederherstellung wirklich nichts angefasst hat. Frueher hing er pauschal an
    /// jeder Sperrmeldung - auch neben "3 Datei(en) zurueckgenommen". Genau der Leser,
    /// der nach fehlenden Dateien suchen muesste, hoerte dann auf zu suchen.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gesperrte_import_recovery_meldet_nur_dann_unveraendert_wenn_nichts_veraendert_wurde(
        bool projektOrdnerVeraendert)
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        Assert.True(new JsonProjectRepository()
            .Save(new Project { Name = "Bleibt geschlossen" }, projectFile).Ok);

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var dialogs = new DialogFake();
        services.Dialogs = dialogs;
        var recovery = new FestesErgebnisRecovery(projektOrdnerVeraendert);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false),
            recovery);

        var opened = shell.TryOpenProject(projectFile);

        Assert.False(opened);
        if (projektOrdnerVeraendert)
        {
            Assert.Contains(
                "Im Projektordner wurde bereits etwas veraendert",
                dialogs.LastErrorMessage,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "und nicht veraendert",
                dialogs.LastErrorMessage,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(
                "Das Projekt wurde nicht geoeffnet und nicht veraendert.",
                dialogs.LastErrorMessage,
                StringComparison.Ordinal);
        }
    }

    private sealed class FestesErgebnisRecovery(bool projektOrdnerVeraendert)
        : IImportTransactionRecoveryService
    {
        public ImportRecoveryResult RecoverIfNeeded(
            string projectRoot,
            string? committedImportTxId)
            => new(
                ImportRecoveryOutcome.Blocked,
                "Import-Wiederherstellung testweise gesperrt.",
                ProjectFolderModified: projektOrdnerVeraendert);
    }

    [Fact]
    public void TryOpenProject_ueberschreibt_keine_neu_erschienene_projektdatei_und_nennt_sicherung()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        var repo = new JsonProjectRepository();
        Assert.True(repo.Save(new Project { Name = "Gueltige Sicherung" }, projectFile).Ok);
        Assert.True(repo.Save(new Project { Name = "Spaeterer Stand" }, projectFile).Ok);
        var backupPath = projectFile + ".bak";
        Assert.True(File.Exists(backupPath));
        File.WriteAllText(projectFile, "{ kaputte projektdatei");

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var dialogs = new DialogFake();
        services.Dialogs = dialogs;
        var blockingRecovery = new CallbackImportRecovery(() =>
            File.WriteAllText(projectFile, "zwischenzeitlich-neu-erschienen"));
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false),
            blockingRecovery);

        var opened = shell.TryOpenProject(projectFile);

        Assert.False(opened);
        Assert.Equal("zwischenzeitlich-neu-erschienen", File.ReadAllText(projectFile));
        Assert.Contains(backupPath, dialogs.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manuell", dialogs.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.GetFiles(
            Path.GetDirectoryName(projectFile)!,
            "projekt.corrupt-*.json"));
    }

    [Fact]
    public void TryOpenProjectAsync_prueft_import_im_hintergrund_und_uebernimmt_projekt_auf_ui_kontext()
    {
        using var temp = new TempDir();
        var projectFile = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        Assert.True(new JsonProjectRepository()
            .Save(new Project { Name = "Async Recovery" }, projectFile)
            .Ok);

        var uiKontext = new PumpenKontext();
        var vorherigerKontext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(uiKontext);
        try
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(
                new AppSettings { EnableRestorePoints = false },
                new DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);
            var recovery = new ThreadRecordingImportRecovery();
            using var shell = new ShellViewModel(
                services,
                new SystemMonitorService(enableHardwareSensorInit: false),
                recovery);
            SynchronizationContext? applyKontext = null;
            shell.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ShellViewModel.Project))
                    applyKontext = SynchronizationContext.Current;
            };
            var uiThread = Environment.CurrentManagedThreadId;

            var lauf = shell.TryOpenProjectAsync(projectFile);
            var opened = uiKontext.PumpeBis(lauf, TimeSpan.FromSeconds(10));

            Assert.True(opened);
            Assert.NotEqual(uiThread, recovery.ThreadId);
            Assert.Null(recovery.Context);
            Assert.Same(uiKontext, applyKontext);
            Assert.Equal("Async Recovery", shell.Project.Name);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(vorherigerKontext);
        }
    }

    private sealed class ThreadRecordingImportRecovery : IImportTransactionRecoveryService
    {
        public int ThreadId { get; private set; }
        public SynchronizationContext? Context { get; private set; }

        public ImportRecoveryResult RecoverIfNeeded(
            string projectRoot,
            string? committedImportTxId)
        {
            ThreadId = Environment.CurrentManagedThreadId;
            Context = SynchronizationContext.Current;
            Thread.Sleep(25);
            return new ImportRecoveryResult(ImportRecoveryOutcome.None, null);
        }
    }

    private sealed class CallbackImportRecovery(Action beforeResult)
        : IImportTransactionRecoveryService
    {
        public ImportRecoveryResult RecoverIfNeeded(
            string projectRoot,
            string? committedImportTxId)
        {
            beforeResult();
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                "Import-Wiederherstellung testweise gesperrt.");
        }
    }

    private sealed class PumpenKontext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public T PumpeBis<T>(Task<T> task, TimeSpan timeout)
        {
            var ende = DateTime.UtcNow + timeout;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > ende)
                    throw new TimeoutException("Projektladen wurde nicht fertig.");

                if (_queue.TryTake(out var eintrag, 25))
                {
                    SetSynchronizationContext(this);
                    eintrag.Callback(eintrag.State);
                }
            }

            return task.GetAwaiter().GetResult();
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public string LastErrorMessage { get; private set; } = string.Empty;
        public int WarningCount { get; private set; }

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => WarningCount++;
        public void Error(string message, string title = "Fehler") => LastErrorMessage = message;
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.No;
    }
}
