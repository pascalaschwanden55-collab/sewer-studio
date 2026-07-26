using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExportPageCostLoadErrorTests
{
    [Fact]
    public async Task Beschaedigte_kostendatei_sperrt_haltungs_excel_export()
    {
        using var temp = new TempDir();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings
        {
            EnableRestorePoints = false,
            LastProjectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json")
        };
        var dialogs = new DialogFake
        {
            SaveFileResult = Path.Combine(temp.Path, "Haltungen.xlsx")
        };
        var services = new ServiceProvider(
            settings,
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory)
        {
            Dialogs = dialogs
        };
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var excel = new ExcelExportFake();
        var toasts = new ToastFake();
        var sync = new CostSyncFake();
        var costs = new CostStoreFake("costs.json ist beschaedigt");
        var vm = new ExportPageViewModel(
            shell,
            settings,
            dialogs,
            excel,
            toasts,
            sync,
            costs,
            new StoredImportFilesFake(),
            patternResolver: null,
            directoryTreeResolver: null,
            katasterXtfPaths: null,
            haltungCadastreIndexes: null);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal(0, excel.HoldingExportCalls);
        Assert.Equal(0, sync.Calls);
        Assert.Contains("Kostendaten", vm.LastResult, StringComparison.Ordinal);
        Assert.Contains("beschaedigt", vm.LastResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kostendaten", toasts.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(dialogs.SaveFileResult));
    }

    private sealed class ExcelExportFake : IExcelExportService
    {
        public int HoldingExportCalls { get; private set; }

        public Result ExportToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
        {
            HoldingExportCalls++;
            return Result.Success();
        }

        public Result ExportSchaechteToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
            => Result.Success();
    }

    private sealed class CostSyncFake : IDerivedCostFieldSynchronizer
    {
        public int Calls { get; private set; }

        public int Sync(Project project, ProjectCostStore store)
        {
            Calls++;
            return 0;
        }
    }

    private sealed class CostStoreFake(string error) : IProjectCostStoreRepository
    {
        public ProjectCostStore Load(string? projectPath) => new();

        public ProjectCostStore Load(string? projectPath, out string? loadError)
        {
            loadError = error;
            return new ProjectCostStore();
        }

        public bool Save(string? projectPath, ProjectCostStore store, out string? saveError)
        {
            saveError = null;
            return true;
        }

        public string GetStorePath(string projectDirectory)
            => Path.Combine(projectDirectory, "costs", "costs.json");
    }

    private sealed class StoredImportFilesFake : IStoredImportFileService
    {
        public StoredImportFilesResult Store(
            string? projectPath,
            IDictionary<string, string> metadata,
            string importKind,
            IReadOnlyCollection<string> paths,
            Func<DateTime>? now = null)
            => new(false, []);
    }

    private sealed class ToastFake : IToastService
    {
        public string LastError { get; private set; } = "";
        public void Success(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) => LastError = message;
    }

    private sealed class DialogFake : IDialogService
    {
        public string? SaveFileResult { get; init; }
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => SaveFileResult;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => true;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => true;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Yes;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ExportPageCostLoadErrorTests_" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumen.
            }
        }
    }
}
