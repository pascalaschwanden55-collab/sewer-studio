using System.Reflection;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsPageViewModelDependencyTests
{
    [Fact]
    public void ViewModel_speichert_keinen_ServiceProvider_und_bekommt_Bereinigung_zentral()
    {
        var fields = typeof(SettingsPageViewModel).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field => field.FieldType == typeof(ServiceProvider));
        Assert.Equal(
            typeof(ProgramCleanupService),
            typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.ProgramCleanup))?.PropertyType);
    }

    [Fact]
    public void Konstruktor_uebernimmt_Einstellungen_und_geteilten_Backupstatus()
    {
        var settings = new AppSettings
        {
            EnableDiagnostics = true,
            LastProjectPath = "C:/Projekte/Test/projekt.json",
            LastVideoSourceFolder = "D:/Videos",
            FullBackupIncludeProjectVideos = true
        };
        var operation = new FullBackupOperationState();
        using var vm = new SettingsPageViewModel(
            settings,
            new DiagnosticsOptions(),
            new DialogFake(),
            new FullBackupFake(),
            new ToastService(),
            operation,
            new ProgramCleanupService());

        Assert.True(vm.EnableDiagnostics);
        Assert.Equal(settings.LastProjectPath, vm.ProjectPath);
        Assert.Equal(settings.LastVideoSourceFolder, vm.VideoFolder);
        Assert.True(vm.IncludeProjectVideosInFullBackup);
        Assert.Same(operation, vm.FullBackupOperation);
    }

    private sealed class FullBackupFake : IFullBackupService
    {
        public Task<FullBackupSizeReport> AnalyzeAsync(
            IProgress<string>? progress = null,
            CancellationToken ct = default)
            => Task.FromResult(new FullBackupSizeReport(Array.Empty<ComponentSize>(), 0, 0));

        public Task<FullBackupResult> RunAsync(
            string targetFolder,
            IProgress<FullBackupProgress>? progress = null,
            CancellationToken ct = default)
            => Task.FromResult(new FullBackupResult(
                true,
                null,
                targetFolder,
                0,
                0,
                0,
                0,
                Array.Empty<string>(),
                TimeSpan.Zero));
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
