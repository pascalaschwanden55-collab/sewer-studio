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
    public void Fehlende_schachtvorlage_erscheint_einmal_als_dialog()
    {
        using var temp = new TempDir();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var dialogs = new DialogFake();
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
        var toasts = new ToastFake();
        var vm = new ExportPageViewModel(
            shell,
            settings,
            dialogs,
            new ExcelExportFake(),
            toasts,
            new CostSyncFake(),
            new CostStoreFake(error: null),
            new StoredImportFilesFake(),
            patternResolver: null,
            directoryTreeResolver: null,
            katasterXtfPaths: null,
            haltungCadastreIndexes: null);
        var missingTemplate = Path.Combine(temp.Path, "Schächte.xlsx");

        var valid = vm.TryValidateExcelTemplate(missingTemplate, "Schacht");

        Assert.False(valid);
        Assert.Contains(missingTemplate, vm.LastResult, StringComparison.Ordinal);
        Assert.Empty(toasts.LastError);
        Assert.Contains(missingTemplate, dialogs.LastError, StringComparison.Ordinal);
        Assert.Equal("Schacht-Export", dialogs.LastErrorTitle);
        Assert.Equal("Export fehlgeschlagen", shell.Subtitle);
    }

    [Fact]
    public async Task Schacht_excel_export_uebergibt_den_aktuellen_projektpfad()
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
            SaveFileResult = Path.Combine(temp.Path, "Schächte.xlsx")
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
        var vm = new ExportPageViewModel(
            shell,
            settings,
            dialogs,
            excel,
            new ToastFake(),
            new CostSyncFake(),
            new CostStoreFake(error: null),
            new StoredImportFilesFake(),
            patternResolver: null,
            directoryTreeResolver: null,
            katasterXtfPaths: null,
            haltungCadastreIndexes: null);

        await vm.ExportSchaechteCommand.ExecuteAsync(null);

        Assert.Same(shell.Project, excel.LastShaftProject);
        Assert.Equal(settings.LastProjectPath, excel.LastShaftProjectPath);
    }

    [Fact]
    public async Task Haltungs_excel_export_berechnet_kosten_nur_in_einer_arbeitskopie()
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

        var sourceRecord = new HaltungRecord();
        sourceRecord.SetFieldValue(FieldKeys.HoldingName, "H-1", FieldSource.Manual, userEdited: true);
        sourceRecord.SetFieldValue(FieldKeys.Cost, "alter Projektwert", FieldSource.Manual, userEdited: true);
        sourceRecord.FieldMeta[FieldKeys.Cost].LastUpdatedUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalModifiedAt = sourceRecord.ModifiedAtUtc;
        var originalCostMeta = sourceRecord.FieldMeta[FieldKeys.Cost];
        var originalCostUpdatedAt = originalCostMeta.LastUpdatedUtc;
        shell.Project.Data.Add(sourceRecord);
        shell.Project.Dirty = false;

        var excel = new ExcelExportFake();
        var sync = new MutatingCostSyncFake();
        var vm = new ExportPageViewModel(
            shell,
            settings,
            dialogs,
            excel,
            new ToastFake(),
            sync,
            new CostStoreFake(error: null),
            new StoredImportFilesFake(),
            patternResolver: null,
            directoryTreeResolver: null,
            katasterXtfPaths: null,
            haltungCadastreIndexes: null);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(sync.Project);
        Assert.NotSame(shell.Project, sync.Project);
        Assert.Same(sync.Project, excel.LastHoldingProject);
        Assert.Equal(settings.LastProjectPath, excel.LastHoldingProjectPath);
        Assert.Equal("nur fuer Excel", excel.LastHoldingProject!.Data.Single().GetFieldValue(FieldKeys.Cost));
        Assert.Same(sourceRecord, shell.Project.Data.Single());
        Assert.Equal("alter Projektwert", sourceRecord.GetFieldValue(FieldKeys.Cost));
        Assert.Same(originalCostMeta, sourceRecord.FieldMeta[FieldKeys.Cost]);
        Assert.Equal(originalCostUpdatedAt, originalCostMeta.LastUpdatedUtc);
        Assert.Equal(originalModifiedAt, sourceRecord.ModifiedAtUtc);
        Assert.False(shell.Project.Dirty);
    }

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
        Assert.Empty(toasts.LastError);
        Assert.Contains("Kostendaten", dialogs.LastError, StringComparison.Ordinal);
        Assert.False(File.Exists(dialogs.SaveFileResult));
    }

    private sealed class ExcelExportFake : IExcelExportService
    {
        public int HoldingExportCalls { get; private set; }
        public Project? LastHoldingProject { get; private set; }
        public string? LastHoldingProjectPath { get; private set; }
        public Project? LastShaftProject { get; private set; }
        public string? LastShaftProjectPath { get; private set; }

        public Result ExportToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
        {
            HoldingExportCalls++;
            LastHoldingProject = project;
            return Result.Success();
        }

        public Result ExportToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow,
            string? projectFilePath)
        {
            LastHoldingProjectPath = projectFilePath;
            return ExportToTemplate(project, templatePath, outputPath, headerRow, startRow);
        }

        public Result ExportSchaechteToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow)
        {
            LastShaftProject = project;
            return Result.Success();
        }

        public Result ExportSchaechteToTemplate(
            Project project,
            string templatePath,
            string outputPath,
            int headerRow,
            int startRow,
            string? projectFilePath)
        {
            LastShaftProjectPath = projectFilePath;
            return ExportSchaechteToTemplate(project, templatePath, outputPath, headerRow, startRow);
        }
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

    private sealed class MutatingCostSyncFake : IDerivedCostFieldSynchronizer
    {
        public Project? Project { get; private set; }

        public int Sync(Project project, ProjectCostStore store)
        {
            Project = project;
            project.Data.Single().SetFieldValue(
                FieldKeys.Cost,
                "nur fuer Excel",
                FieldSource.Manual,
                userEdited: true);
            return 1;
        }
    }

    private sealed class CostStoreFake(string? error) : IProjectCostStoreRepository
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
        public string LastError { get; private set; } = "";
        public string LastErrorTitle { get; private set; } = "";
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null)
            => SaveFileResult;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler")
        {
            LastError = message;
            LastErrorTitle = title;
        }
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
