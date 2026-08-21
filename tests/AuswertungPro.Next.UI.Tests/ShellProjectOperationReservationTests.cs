using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellProjectOperationReservationTests
{
    [Fact]
    public async Task Laufender_import_sperrt_verdeckten_schachtimport_und_export()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.ImportDialogs.InfoEntered = entered;
        harness.ImportDialogs.ReleaseInfo = release;

        var running = Task.Run(
            () => harness.Import.MakeProjectPortableCommand.ExecuteAsync(null));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Import wurde nicht gestartet.");

        try
        {
            await ExecuteSchachtImportAsync(harness.Schaechte);
            await harness.Export.DistributeShaftsNormalCommand.ExecuteAsync(null);

            Assert.Equal(0, harness.SchachtDialogs.ConfirmCancelCalls);
            Assert.Equal(0, harness.ExportDialogs.ConfirmCancelCalls);
            Assert.False(harness.Shell.TrySaveProject());
        }
        finally
        {
            release.Set();
            await running;
        }

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Laufender_schachtimport_sperrt_verdeckten_import_und_export()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.SchachtDialogs.ConfirmCancelEntered = entered;
        harness.SchachtDialogs.ReleaseConfirmCancel = release;

        var running = Task.Run(
            () => ExecuteSchachtImportAsync(harness.Schaechte));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Schachtimport wurde nicht gestartet.");

        try
        {
            await harness.Import.MakeProjectPortableCommand.ExecuteAsync(null);
            await harness.Export.DistributeShaftsNormalCommand.ExecuteAsync(null);

            Assert.Equal(0, harness.ImportDialogs.InfoCalls);
            Assert.Equal(0, harness.ExportDialogs.ConfirmCancelCalls);
            Assert.False(harness.Shell.TrySaveProjectAs());
        }
        finally
        {
            release.Set();
            await running;
        }

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Laufender_export_sperrt_verdeckten_import_und_schachtimport()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        harness.ExportDialogs.ConfirmCancelEntered = entered;
        harness.ExportDialogs.ReleaseConfirmCancel = release;

        var running = Task.Run(
            () => harness.Export.DistributeShaftsNormalCommand.ExecuteAsync(null));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Export wurde nicht gestartet.");

        try
        {
            await harness.Import.MakeProjectPortableCommand.ExecuteAsync(null);
            await ExecuteSchachtImportAsync(harness.Schaechte);

            Assert.Equal(0, harness.ImportDialogs.InfoCalls);
            Assert.Equal(0, harness.SchachtDialogs.ConfirmCancelCalls);
            Assert.False(harness.Shell.TrySaveProject());
        }
        finally
        {
            release.Set();
            await running;
        }

        Assert.True(harness.Shell.SaveAsProjectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Fremder_interner_save_bleibt_waehrend_eines_imports_gesperrt()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var harness = new Harness();
        var foreignGuard = new AlwaysSavingGuard();
        harness.Shell.RegisterShellOperationGuard(foreignGuard);
        var foreignSave = harness.Shell.CreateActiveProjectOperationSaveDelegate(foreignGuard);
        harness.ImportDialogs.InfoEntered = entered;
        harness.ImportDialogs.ReleaseInfo = release;

        var running = Task.Run(
            () => harness.Import.MakeProjectPortableCommand.ExecuteAsync(null));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "Import wurde nicht gestartet.");

        try
        {
            Assert.False(foreignSave());
            Assert.False(File.Exists(harness.ProjectPath));
        }
        finally
        {
            release.Set();
            await running;
            harness.Shell.UnregisterShellOperationGuard(foreignGuard);
        }
    }

    private static Task ExecuteSchachtImportAsync(SchaechtePageViewModel viewModel)
        => Assert.IsAssignableFrom<IAsyncRelayCommand>(viewModel.ImportProtocolCommand)
            .ExecuteAsync(null);

    private sealed class Harness : IDisposable
    {
        private readonly DirectoryInfo _tempDirectory;
        private readonly ILoggerFactory _loggerFactory;

        internal Harness()
        {
            _tempDirectory = Directory.CreateTempSubdirectory("shell-project-operation-");
            var projectRoot = Path.Combine(_tempDirectory.FullName, "Projekt");
            Directory.CreateDirectory(projectRoot);
            ProjectPath = ProjectFileLocator.TargetPath(projectRoot);

            Settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = ProjectPath,
                SchachtDistribution = new DistributionTargetConfig
                {
                    Root = Path.Combine(projectRoot, ProjectStructure.SchaechteVerteilt)
                }
            };
            ImportDialogs = new GateDialog();
            SchachtDialogs = new GateDialog();
            ExportDialogs = new GateDialog();
            _loggerFactory = LoggerFactory.Create(_ => { });
            Services = new ServiceProvider(
                Settings,
                new DiagnosticsOptions(),
                _loggerFactory.CreateLogger("test"),
                _loggerFactory)
            {
                Dialogs = ImportDialogs
            };
            Shell = new ShellViewModel(
                Services,
                new SystemMonitorService(enableHardwareSensorInit: false));
            Shell.ReplaceProject(new Project { Name = "Reservierungs-Test" });
            Shell.HasPersistedProject = true;
            Shell.EnterWorkspaceOn("Uebersicht");

            Import = new ImportPageViewModel(Shell, Services);
            Services.Dialogs = SchachtDialogs;
            Schaechte = new SchaechtePageViewModel(Shell, Services);
            Services.Dialogs = ExportDialogs;
            Export = new ExportPageViewModel(Shell, Services);
        }

        internal AppSettings Settings { get; }
        internal ServiceProvider Services { get; }
        internal ShellViewModel Shell { get; }
        internal ImportPageViewModel Import { get; }
        internal SchaechtePageViewModel Schaechte { get; }
        internal ExportPageViewModel Export { get; }
        internal GateDialog ImportDialogs { get; }
        internal GateDialog SchachtDialogs { get; }
        internal GateDialog ExportDialogs { get; }
        internal string ProjectPath { get; }

        public void Dispose()
        {
            Export.Dispose();
            Schaechte.Dispose();
            Shell.Dispose();
            _loggerFactory.Dispose();
            try
            {
                _tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf das Ergebnis nicht verdecken.
            }
        }
    }

    private sealed class GateDialog : IDialogService
    {
        private int _infoCalls;
        private int _confirmCancelCalls;

        internal ManualResetEventSlim? InfoEntered { get; set; }
        internal ManualResetEventSlim? ReleaseInfo { get; set; }
        internal ManualResetEventSlim? ConfirmCancelEntered { get; set; }
        internal ManualResetEventSlim? ReleaseConfirmCancel { get; set; }
        internal int InfoCalls => Volatile.Read(ref _infoCalls);
        internal int ConfirmCancelCalls => Volatile.Read(ref _confirmCancelCalls);

        public string? OpenFile(string title, string filter, string? initialDirectory = null)
            => null;

        public string[] OpenFiles(string title, string filter) => [];

        public string? SaveFile(
            string title,
            string filter,
            string? defaultExt = null,
            string? defaultFileName = null)
            => null;

        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis")
        {
            Interlocked.Increment(ref _infoCalls);
            InfoEntered?.Set();
            if (ReleaseInfo is not null
                && !ReleaseInfo.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Testfreigabe fuer Hinweis fehlt.");
            }
        }

        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;

        public bool ConfirmWarn(
            string message,
            string title = "Bestaetigung",
            bool defaultNo = true)
            => false;

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung")
        {
            Interlocked.Increment(ref _confirmCancelCalls);
            ConfirmCancelEntered?.Set();
            if (ReleaseConfirmCancel is not null
                && !ReleaseConfirmCancel.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Testfreigabe fuer Auswahl fehlt.");
            }

            return DialogConfirm.Cancel;
        }
    }

    private sealed class AlwaysSavingGuard : IShellOperationGuard
    {
        public bool CanSaveProjectFromShell => true;
        public string ProjectSaveBlockedMessage => "";
        public bool AllowsInternalProjectSave => true;
        public bool CanLeaveShellContext => true;
        public string LeaveBlockedMessage => "";
        public event EventHandler? OperationAvailabilityChanged
        {
            add { }
            remove { }
        }
    }
}
