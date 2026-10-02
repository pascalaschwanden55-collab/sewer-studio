using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed partial class SettingsFullBackupWorkflowTests
{
    [Theory]
    [InlineData(@"G:\Systemschutz\SewerStudio_Datensicherung")]
    [InlineData(@"G:\Systemschutz\SewerStudio_Datensicherung\")]
    [InlineData(@"G:\Systemschutz\sewerstudio_datensicherung")]
    public async Task RunAsync_gewaehlter_Sicherungsordner_wird_nicht_nochmals_angehaengt(string selectedFolder)
    {
        var settings = new AppSettings();
        var backup = new FullBackupFake();
        var dialogs = new DialogFake { SelectedFolder = selectedFolder, ConfirmResult = true };

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Equal(@"G:\Systemschutz", backup.RunTargetFolder);
        Assert.Equal(@"G:\Systemschutz", settings.LastFullBackupPath);
        Assert.Contains(@"Ziel: G:\Systemschutz\SewerStudio_Datensicherung", dialogs.ConfirmText);
        Assert.DoesNotContain(@"SewerStudio_Datensicherung\SewerStudio_Datensicherung", dialogs.ConfirmText);
    }

    [Theory]
    [InlineData(@"G:\")]
    [InlineData(@"G:\Systemschutz")]
    [InlineData(@"G:\SewerStudio_Datensicherung-alt")]
    [InlineData(@"\\server\freigabe\")]
    [InlineData(@"\\server\SewerStudio_Datensicherung\")]
    public async Task RunAsync_gewaehlter_Elternordner_behaelt_den_bisherigen_Zielaufbau(string selectedFolder)
    {
        var backup = new FullBackupFake();
        var dialogs = new DialogFake { SelectedFolder = selectedFolder, ConfirmResult = true };

        await SettingsFullBackupWorkflow.RunAsync(
            Request(new AppSettings(), backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Equal(selectedFolder, backup.RunTargetFolder);
        Assert.Contains($"Ziel: {Path.Combine(selectedFolder, BackupPlanBuilder.TargetFolderName)}", dialogs.ConfirmText);
    }

    [Theory]
    [InlineData(@"G:\SewerStudio_Datensicherung", @"G:\")]
    [InlineData(@"\\server\freigabe\SewerStudio_Datensicherung", @"\\server\freigabe")]
    [InlineData(@"\\server\freigabe\SewerStudio_Datensicherung\", @"\\server\freigabe")]
    public async Task RunAsync_gewaehlter_Sicherungsordner_direkt_in_der_Wurzel_behaelt_die_Wurzel(
        string selectedFolder, string expectedParent)
    {
        var backup = new FullBackupFake();
        var dialogs = new DialogFake { SelectedFolder = selectedFolder, ConfirmResult = true };
        var settings = new AppSettings();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Equal(expectedParent, backup.RunTargetFolder);
        Assert.Equal(expectedParent, settings.LastFullBackupPath);
        Assert.Contains($"Ziel: {Path.Combine(expectedParent, BackupPlanBuilder.TargetFolderName)}", dialogs.ConfirmText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(@"D:\alte-sicherung")]
    public async Task RunAsync_ungueltiger_Zielpfad_meldet_Fehler_ohne_Sicherung_oder_Einstellungsspeicherung(
        string? previousParent)
    {
        var backup = new FullBackupFake();
        var dialogs = new DialogFake { SelectedFolder = "C:\\ung\0ueltig", ConfirmResult = true };
        var settings = new AppSettings { LastFullBackupPath = previousParent };
        var state = new FullBackupOperationState();
        var calls = new List<string>();
        var toasts = new ToastFake();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, toasts, state, calls,
                new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Single(dialogs.Errors);
        Assert.Equal(0, backup.AnalyzeCalls);
        Assert.Equal(0, backup.RunCalls);
        Assert.Empty(calls);
        Assert.Equal(previousParent, settings.LastFullBackupPath);
        Assert.False(state.IsRunning);
        Assert.Equal(["error:Datensicherung fehlgeschlagen."], toasts.Messages);
    }

    [Theory]
    [InlineData(@"G:\Systemschutz\SewerStudio_Datensicherung")]
    [InlineData("C:\\ung\0ueltig")]
    public async Task RunAsync_vorab_abgebrochen_startet_keine_Analyse_und_behaelt_die_Einstellungen(string selectedFolder)
    {
        var backup = new FullBackupFake();
        var dialogs = new DialogFake { SelectedFolder = selectedFolder, ConfirmResult = true };
        var settings = new AppSettings { LastFullBackupPath = @"D:\alte-sicherung" };
        var state = new FullBackupOperationState();
        var calls = new List<string>();
        var toasts = new ToastFake();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, backup, dialogs, toasts, state, calls,
                new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            cancellation.Token);

        Assert.Equal(0, backup.AnalyzeCalls);
        Assert.Equal(0, backup.RunCalls);
        Assert.Empty(calls);
        Assert.Empty(dialogs.Errors);
        Assert.Null(dialogs.ConfirmText);
        Assert.Equal(@"D:\alte-sicherung", settings.LastFullBackupPath);
        Assert.False(state.IsRunning);
        Assert.Equal(["info:Datensicherung abgebrochen."], toasts.Messages);
    }

    [Theory]
    [InlineData(false, "Marker-Datei fehlt", null)]
    [InlineData(true, "ungültig", null)]
    [InlineData(false, "Marker-Datei fehlt", @"D:\alte-sicherung")]
    [InlineData(true, "ungültig", @"D:\alte-sicherung")]
    public async Task RunAsync_gewaehlter_fremder_Sicherungsordner_bleibt_gesperrt(
        bool invalidMarker, string expectedError, string? previousParent)
    {
        using var folders = new BackupTargetTestFolders();
        Directory.CreateDirectory(folders.BackupRoot);
        var original = Path.Combine(folders.BackupRoot, "privat.txt");
        File.WriteAllText(original, "unverändert erhalten");
        var marker = Path.Combine(folders.BackupRoot, BackupPlanBuilder.MarkerFileName);
        if (invalidMarker)
            File.WriteAllText(marker, "fremder Marker");
        var settings = new AppSettings { LastFullBackupPath = previousParent };
        var dialogs = new DialogFake { SelectedFolder = folders.BackupRoot, ConfirmResult = true };

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, folders.CreateService(), dialogs, new ToastFake(), new FullBackupOperationState(),
                new List<string>(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Contains(expectedError, Assert.Single(dialogs.Errors));
        Assert.Equal("unverändert erhalten", File.ReadAllText(original));
        Assert.False(Directory.Exists(Path.Combine(folders.BackupRoot, BackupPlanBuilder.TargetFolderName)));
        Assert.Equal(previousParent, settings.LastFullBackupPath);
        if (invalidMarker)
            Assert.Equal("fremder Marker", File.ReadAllText(marker));
        else
            Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task RunAsync_gewaehlte_Sicherung_mit_gueltigem_Marker_wird_direkt_aktualisiert()
    {
        using var folders = new BackupTargetTestFolders();
        Assert.Null(new BackupTargetMarkerGuardService().ValidateAndCreateMarker(folders.BackupRoot));
        var existingFile = Path.Combine(folders.BackupRoot, "KI_BRAIN", "wissen.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existingFile)!);
        File.WriteAllText(existingFile, "vorhandenes Wissen");
        File.WriteAllText(Path.Combine(folders.KnowledgeRoot, "wissen.txt"), "vorhandenes Wissen");
        var settings = new AppSettings();
        var dialogs = new DialogFake { SelectedFolder = folders.BackupRoot, ConfirmResult = true };
        var toasts = new ToastFake();

        await SettingsFullBackupWorkflow.RunAsync(
            Request(settings, folders.CreateService(), dialogs, toasts, new FullBackupOperationState(),
                new List<string>(), new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        Assert.Empty(dialogs.Errors);
        Assert.Equal(["success:Datensicherung abgeschlossen."], toasts.Messages);
        Assert.Equal(folders.TargetParent, settings.LastFullBackupPath);
        Assert.Equal("vorhandenes Wissen", File.ReadAllText(existingFile));
        Assert.True(File.Exists(Path.Combine(folders.BackupRoot, "manifest.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folders.BackupRoot, "manifest.json")));
        Assert.Equal(0, manifest.RootElement.GetProperty("Totals").GetProperty("Copied").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("Totals").GetProperty("Unchanged").GetInt32());
        Assert.False(Directory.Exists(Path.Combine(folders.BackupRoot, BackupPlanBuilder.TargetFolderName)));
        Assert.Contains($"Ziel: {folders.BackupRoot}", dialogs.ConfirmText);
    }

    private sealed class BackupTargetTestFolders : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-settings-backup-" + Guid.NewGuid());
        public string TargetParent => Path.Combine(_root, "target");
        public string BackupRoot => Path.Combine(TargetParent, BackupPlanBuilder.TargetFolderName);
        public string KnowledgeRoot => Path.Combine(_root, "source");

        public BackupTargetTestFolders() => Directory.CreateDirectory(KnowledgeRoot);

        public IFullBackupService CreateService()
        {
            var optional = Path.Combine(_root, "optional-missing");
            var sources = new FullBackupSources(
                RepoRoot: null,
                KnowledgeRoot: KnowledgeRoot,
                LocalSewerStudioDir: optional,
                RoamingSewerStudioDir: optional,
                RoamingAuswertungProDir: optional,
                DesktopDir: optional,
                AppVersion: "test",
                EnvironmentVariables: new Dictionary<string, string>());
            return new FullBackupService(() => sources, new BackupTargetMarkerGuardService());
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Assert.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "sewerstudio-settings-backup-"),
                    Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase);
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
