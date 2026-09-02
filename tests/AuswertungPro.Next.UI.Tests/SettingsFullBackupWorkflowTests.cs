using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsFullBackupWorkflowTests
{
    [Fact]
    public async Task RunAsync_success_updates_settings_progress_toast_and_skipped_warning()
    {
        var nowUtc = new DateTime(2026, 7, 3, 12, 30, 0, DateTimeKind.Utc);
        var settings = new AppSettings { LastFullBackupPath = @"D:\Alt" };
        var backup = new FullBackupFake
        {
            AnalyzeReport = Report(),
            RunResult = new FullBackupResult(
                Success: true,
                Error: null,
                TargetRoot: @"E:\Backup\SewerStudio_Datensicherung",
                TotalBytes: 200,
                FilesCopied: 2,
                FilesUnchanged: 3,
                FilesDeleted: 1,
                SkippedFiles: [@"C:\locked.txt"],
                Duration: TimeSpan.FromSeconds(1),
                FilesVerified: 2,
                DatabasesSnapshotted: 1)
        };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };
        var toasts = new ToastFake();
        var state = new FullBackupOperationState();
        var calls = new List<string>();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, toasts, state, calls, nowUtc),
            CancellationToken.None);

        Assert.Equal(@"D:\Alt", dialogs.SelectInitialPath);
        Assert.Equal(@"E:\Backup", backup.RunTargetFolder);
        Assert.Equal(nowUtc, settings.LastFullBackupUtc);
        Assert.Equal(@"E:\Backup", settings.LastFullBackupPath);
        Assert.Equal(200, settings.LastFullBackupSizeBytes);
        Assert.Equal(["flush", "save"], calls);
        Assert.Equal(100, state.Percent);
        Assert.Equal("", state.CurrentFile);
        Assert.Equal(
            "Fertig: 2 kopiert, 2 vollstaendig geprueft, 1 Datenbank-Schnappschuss, 3 unveraendert, 1 nach _Versionen verschoben.",
            state.StatusText);
        Assert.Contains(@"E:\Backup", state.LastBackupInfo);
        Assert.Equal(["success:Datensicherung abgeschlossen."], toasts.Messages);
        Assert.Single(dialogs.Warnings);
        Assert.Contains("locked.txt", dialogs.Warnings[0]);
        Assert.False(state.IsRunning);
    }

    [Fact]
    public async Task RunAsync_cancelled_folder_selection_does_not_start_analysis()
    {
        var backup = new FullBackupFake { AnalyzeReport = Report() };
        var state = new FullBackupOperationState();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(new AppSettings(), backup, new DialogFake(), new ToastFake(), state, new List<string>(), DateTime.UtcNow),
            CancellationToken.None);

        Assert.Equal(0, backup.AnalyzeCalls);
        Assert.Equal(0, backup.RunCalls);
        Assert.False(state.IsRunning);
        Assert.Equal("", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_failed_result_reports_error_and_does_not_save_settings()
    {
        var settings = new AppSettings();
        var backup = new FullBackupFake
        {
            AnalyzeReport = Report(),
            RunResult = new FullBackupResult(
                Success: false,
                Error: "Ziel ungültig",
                TargetRoot: "",
                TotalBytes: 0,
                FilesCopied: 0,
                FilesUnchanged: 0,
                FilesDeleted: 0,
                SkippedFiles: [],
                Duration: TimeSpan.Zero)
        };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };
        var toasts = new ToastFake();
        var state = new FullBackupOperationState();
        var calls = new List<string>();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, toasts, state, calls, DateTime.UtcNow),
            CancellationToken.None);

        Assert.Equal("Fehler: Ziel ungültig", state.StatusText);
        Assert.Equal(["error:Datensicherung fehlgeschlagen."], toasts.Messages);
        Assert.Equal(["Ziel ungültig"], dialogs.Errors);
        Assert.Equal(["flush"], calls);
        Assert.Null(settings.LastFullBackupPath);
        Assert.False(state.IsRunning);
    }

    [Fact]
    public async Task RunAsync_running_backup_blocks_second_start()
    {
        var backup = new FullBackupFake { AnalyzeReport = Report() };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };
        var toasts = new ToastFake();
        var state = new FullBackupOperationState();
        Assert.True(state.TryBegin(CancellationToken.None, out _));

        try
        {
            await SettingsFullBackupWorkflow.RunAsync(
                Request(new AppSettings(), backup, dialogs, toasts, state, new List<string>(), DateTime.UtcNow),
                CancellationToken.None);

            Assert.Equal(0, backup.AnalyzeCalls);
            Assert.Equal(0, backup.RunCalls);
            Assert.Equal(["info:Datensicherung laeuft bereits."], toasts.Messages);
        }
        finally
        {
            state.Finish();
        }
    }

    [Fact]
    public async Task RunAsync_schreibt_den_Fehlergrund_ins_Programmlog()
    {
        var backup = new FullBackupFake
        {
            AnalyzeReport = Report(),
            RunResult = new FullBackupResult(
                Success: false,
                Error: "Zielordner enthaelt bereits Daten",
                TargetRoot: "",
                TotalBytes: 0,
                FilesCopied: 0,
                FilesUnchanged: 0,
                FilesDeleted: 0,
                SkippedFiles: [],
                Duration: TimeSpan.Zero)
        };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };
        var log = new List<string>();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(new AppSettings(), backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), DateTime.UtcNow, log),
            CancellationToken.None);

        // Ohne diese Zeile war der Grund nur im weggeklickten Dialog sichtbar.
        Assert.Contains(log, eintrag =>
            eintrag.Contains("Zielordner enthaelt bereits Daten", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_schreibt_uebersprungene_Dateien_ins_Programmlog()
    {
        var backup = new FullBackupFake
        {
            AnalyzeReport = Report(),
            RunResult = new FullBackupResult(
                Success: true,
                Error: null,
                TargetRoot: @"E:\Backup",
                TotalBytes: 10,
                FilesCopied: 1,
                FilesUnchanged: 0,
                FilesDeleted: 0,
                SkippedFiles: [@"C:\gesperrt.txt"],
                Duration: TimeSpan.FromSeconds(1),
                SkippedFileTotal: 1)
        };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };
        var log = new List<string>();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(new AppSettings(), backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), DateTime.UtcNow, log),
            CancellationToken.None);

        Assert.Contains(log, eintrag => eintrag.Contains("gesperrt.txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_nennt_die_echte_Zahl_uebersprungener_Dateien()
    {
        // Die Liste ist auf 200 Beispiele gedeckelt; gemeldet werden muss die
        // tatsaechliche Zahl, sonst wirkt eine grosse Luecke harmlos.
        var beispiele = Enumerable.Range(1, 200).Select(i => $@"C:\datei{i}.txt").ToArray();
        var backup = new FullBackupFake
        {
            AnalyzeReport = Report(),
            RunResult = new FullBackupResult(
                Success: true,
                Error: null,
                TargetRoot: @"E:\Backup",
                TotalBytes: 10,
                FilesCopied: 1,
                FilesUnchanged: 0,
                FilesDeleted: 0,
                SkippedFiles: beispiele,
                Duration: TimeSpan.FromSeconds(1),
                SkippedFileTotal: 517)
        };
        var dialogs = new DialogFake { SelectedFolder = @"E:\Backup", ConfirmResult = true };

        await SettingsFullBackupWorkflow.RunAsync(
            Request(new AppSettings(), backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), DateTime.UtcNow),
            CancellationToken.None);

        Assert.Single(dialogs.Warnings);
        Assert.Contains("517", dialogs.Warnings[0]);
    }

    private static SettingsFullBackupWorkflowRequest Request(
        AppSettings settings,
        IFullBackupService backup,
        IDialogService dialogs,
        IToastService toasts,
        FullBackupOperationState state,
        List<string> calls,
        DateTime nowUtc,
        List<string>? log = null)
        => new(
            Settings: settings,
            FullBackup: backup,
            Dialogs: dialogs,
            Toasts: toasts,
            Operation: state,
            FlushPendingSave: () => calls.Add("flush"),
            SaveSettingsImmediate: () => calls.Add("save"),
            UtcNow: () => nowUtc,
            Log: log is null ? null : log.Add);

    private static FullBackupSizeReport Report()
        => new(
            [new ComponentSize("Programm", "Code", 100, 1, SourceFound: true)],
            TotalBytes: 100,
            TotalFiles: 1);

    private sealed class FullBackupFake : IFullBackupService
    {
        public FullBackupSizeReport AnalyzeReport { get; set; } = Report();
        public FullBackupResult RunResult { get; set; } = new(
            true,
            null,
            "",
            0,
            0,
            0,
            0,
            [],
            TimeSpan.Zero);

        public int AnalyzeCalls { get; private set; }
        public int RunCalls { get; private set; }
        public string? RunTargetFolder { get; private set; }

        public Task<FullBackupSizeReport> AnalyzeAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            AnalyzeCalls++;
            return Task.FromResult(AnalyzeReport);
        }

        public Task<FullBackupResult> RunAsync(
            string targetFolder,
            IProgress<FullBackupProgress>? progress = null,
            CancellationToken ct = default)
        {
            RunCalls++;
            RunTargetFolder = targetFolder;
            progress?.Report(new FullBackupProgress("Programm", @"C:\Quelle\a.txt", 50, 100, 1, 2));
            return Task.FromResult(RunResult);
        }
    }

    private sealed class DialogFake : IDialogService
    {
        public string? SelectedFolder { get; set; }
        public bool ConfirmResult { get; set; }
        public string? SelectInitialPath { get; private set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null)
        {
            SelectInitialPath = initialPath;
            return SelectedFolder;
        }

        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => Warnings.Add(message);
        public void Error(string message, string title = "Fehler") => Errors.Add(message);
        public bool Confirm(string message, string title = "Bestaetigung") => ConfirmResult;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }

    private sealed class ToastFake : IToastService
    {
        public List<string> Messages { get; } = new();

        public void Success(string message) => Messages.Add("success:" + message);
        public void Info(string message) => Messages.Add("info:" + message);
        public void Warning(string message) => Messages.Add("warning:" + message);
        public void Error(string message) => Messages.Add("error:" + message);
    }
}
