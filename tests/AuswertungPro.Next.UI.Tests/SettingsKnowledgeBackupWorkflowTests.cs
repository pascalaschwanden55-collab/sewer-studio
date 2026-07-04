using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsKnowledgeBackupWorkflowTests
{
    [Fact]
    public async Task ExportAsync_success_uses_default_name_updates_status_and_shows_info()
    {
        var dialogs = new DialogFake { SavePath = @"D:\Backup\ki.zip" };
        var state = new UiState();
        var calls = new List<string>();

        await SettingsKnowledgeBackupWorkflow.ExportAsync(
            Request(
                dialogs,
                state,
                calls,
                now: new DateTime(2026, 7, 3),
                export: (path, _, _) =>
                {
                    calls.Add("export:" + path);
                    return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 7, 1024 * 1024));
                }),
            CancellationToken.None);

        Assert.Equal("SewerStudio_KI_Backup_2026-07-03", dialogs.DefaultFileName);
        Assert.Equal(["export:D:\\Backup\\ki.zip"], calls);
        Assert.Equal("Export OK: 7 Dateien, 1.0 MB", state.Status);
        Assert.Single(dialogs.Infos);
        Assert.Contains("KI-Wissen erfolgreich exportiert.", dialogs.Infos[0]);
        Assert.Contains(@"D:\Backup\ki.zip", dialogs.Infos[0]);
    }

    [Fact]
    public async Task ExportAsync_cancelled_save_dialog_does_not_call_export()
    {
        var calls = new List<string>();

        await SettingsKnowledgeBackupWorkflow.ExportAsync(
            Request(
                new DialogFake(),
                new UiState(),
                calls,
                DateTime.Today,
                export: (_, _, _) =>
                {
                    calls.Add("export");
                    return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 1, 1));
                }),
            CancellationToken.None);

        Assert.Empty(calls);
    }

    [Fact]
    public async Task ImportAsync_success_requires_confirmation_and_shows_restart_info()
    {
        var dialogs = new DialogFake { OpenPath = @"D:\Backup\ki.zip", ConfirmResult = true };
        var state = new UiState();
        var calls = new List<string>();

        await SettingsKnowledgeBackupWorkflow.ImportAsync(
            Request(
                dialogs,
                state,
                calls,
                DateTime.Today,
                import: (path, _, _) =>
                {
                    calls.Add("import:" + path);
                    return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 3, 0));
                }),
            CancellationToken.None);

        Assert.True(dialogs.ConfirmCalled);
        Assert.Equal(["import:D:\\Backup\\ki.zip"], calls);
        Assert.Equal("Import OK: 3 Dateien", state.Status);
        Assert.Single(dialogs.Infos);
        Assert.Contains("Bitte starten Sie die Anwendung neu.", dialogs.Infos[0]);
    }

    [Fact]
    public async Task ImportAsync_cancelled_confirmation_does_not_call_import()
    {
        var dialogs = new DialogFake { OpenPath = @"D:\Backup\ki.zip", ConfirmResult = false };
        var calls = new List<string>();

        await SettingsKnowledgeBackupWorkflow.ImportAsync(
            Request(
                dialogs,
                new UiState(),
                calls,
                DateTime.Today,
                import: (_, _, _) =>
                {
                    calls.Add("import");
                    return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 1, 0));
                }),
            CancellationToken.None);

        Assert.True(dialogs.ConfirmCalled);
        Assert.Empty(calls);
    }

    private static SettingsKnowledgeBackupWorkflowRequest Request(
        IDialogService dialogs,
        UiState state,
        List<string> calls,
        DateTime now,
        Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>>? export = null,
        Func<string, IProgress<string>?, CancellationToken, Task<KnowledgeBackupService.BackupResult>>? import = null)
        => new(
            Dialogs: dialogs,
            SetStatusText: value => state.Status = value,
            ExportAsync: export ?? ((_, _, _) =>
            {
                calls.Add("export");
                return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 1, 1));
            }),
            ImportAsync: import ?? ((_, _, _) =>
            {
                calls.Add("import");
                return Task.FromResult(new KnowledgeBackupService.BackupResult(true, null, 1, 1));
            }),
            Now: () => now);

    private sealed class UiState
    {
        public string Status { get; set; } = "";
    }

    private sealed class DialogFake : IDialogService
    {
        public string? SavePath { get; set; }
        public string? OpenPath { get; set; }
        public bool ConfirmResult { get; set; }
        public bool ConfirmCalled { get; private set; }
        public string? DefaultFileName { get; private set; }
        public List<string> Infos { get; } = new();
        public List<string> Errors { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => OpenPath;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
        {
            DefaultFileName = defaultFileName;
            return SavePath;
        }

        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") => Infos.Add(message);
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") => Errors.Add(message);
        public bool Confirm(string message, string title = "Bestaetigung")
        {
            ConfirmCalled = true;
            return ConfirmResult;
        }

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
