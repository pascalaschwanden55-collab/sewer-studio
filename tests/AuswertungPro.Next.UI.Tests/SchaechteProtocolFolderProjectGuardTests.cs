using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteProtocolFolderProjectGuardTests
{
    [Fact]
    public async Task Folder_import_stops_same_folder_replacement_before_confirmation_and_distribution()
    {
        using var harness = new Harness(sourceIsProjectDistribution: false);
        var expected = harness.ProjectContext;
        var replacement = NewProject();
        harness.Shell.ReplaceProject(replacement);

        await harness.InvokeFolderImportAsync(expected);

        Assert.Equal(0, harness.Dialogs.ConfirmWarnCalls);
        Assert.Equal(0, harness.Protocol.ParseCalls);
        Assert.Equal(0, harness.Protocol.FindCalls);
        Assert.Equal(0, harness.Protocol.ApplyCalls);
        Assert.Equal(0, harness.Dialogs.SaveFileCalls);
        Assert.False(Directory.Exists(harness.DistributionFolder));
        AssertStoppedWithoutProjectMutation(harness, replacement);
    }

    [Fact]
    public async Task Folder_import_stops_save_as_path_change_inside_same_project_folder()
    {
        using var harness = new Harness(sourceIsProjectDistribution: false);
        var expected = harness.ProjectContext;
        harness.Settings.LastProjectPath = Path.Combine(
            harness.ProjectRoot,
            "Projektdateien",
            "anderes-projekt.json");

        await harness.InvokeFolderImportAsync(expected);

        Assert.Equal(0, harness.Dialogs.ConfirmWarnCalls);
        Assert.Equal(0, harness.Protocol.ParseCalls);
        Assert.Equal(0, harness.Protocol.FindCalls);
        Assert.Equal(0, harness.Protocol.ApplyCalls);
        Assert.Equal(0, harness.Dialogs.SaveFileCalls);
        Assert.False(Directory.Exists(harness.DistributionFolder));
        Assert.Empty(harness.ExpectedProject.SchaechteData);
        Assert.False(harness.ExpectedProject.Dirty);
        Assert.Equal(DateTime.UnixEpoch, harness.ExpectedProject.ModifiedAtUtc);
        Assert.Null(harness.ViewModel.Selected);
        Assert.Equal("Vorgang abgebrochen: Projekt wurde gewechselt.", harness.ViewModel.LastResult);
    }

    [Fact]
    public async Task Folder_import_stops_project_switch_during_parse_before_record_commit()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        var expected = harness.ProjectContext;
        var replacement = NewProject();
        harness.Protocol.OnParse = () => harness.Shell.ReplaceProject(replacement);

        await harness.InvokeFolderImportAsync(expected);

        Assert.Equal(1, harness.Dialogs.ConfirmWarnCalls);
        Assert.Equal(1, harness.Protocol.ParseCalls);
        Assert.Equal(0, harness.Protocol.FindCalls);
        Assert.Equal(0, harness.Protocol.ApplyCalls);
        Assert.Equal(0, harness.Dialogs.SaveFileCalls);
        AssertStoppedWithoutProjectMutation(harness, replacement);
    }

    [Fact]
    public async Task Folder_import_project_switch_after_selection_keeps_dirty_commit_but_stops_save()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        var replacement = NewProject();
        harness.ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SchaechtePageViewModel.Selected))
                harness.Shell.ReplaceProject(replacement);
        };

        await harness.InvokeFolderImportAsync(harness.ProjectContext);

        Assert.Equal(1, harness.Protocol.ParseCalls);
        Assert.Equal(1, harness.Protocol.FindCalls);
        Assert.Equal(1, harness.Protocol.ApplyCalls);
        Assert.Single(harness.ExpectedProject.SchaechteData);
        Assert.True(harness.ExpectedProject.Dirty);
        Assert.NotEqual(DateTime.UnixEpoch, harness.ExpectedProject.ModifiedAtUtc);
        Assert.Empty(replacement.SchaechteData);
        Assert.False(replacement.Dirty);
        Assert.Equal(0, harness.Dialogs.SaveFileCalls);
        Assert.Null(harness.ViewModel.Selected);
        Assert.Equal(
            "Projekt wurde gewechselt: Aenderungen wurden uebernommen, aber nicht gespeichert.",
            harness.ViewModel.LastResult);
        Assert.Contains(
            harness.Dialogs.Warnings,
            warning => warning.Message.Contains(
                "Aenderungen im zuvor gestarteten Projekt wurden nicht gespeichert",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Folder_import_keeps_replacement_project_selection_after_late_switch()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        var replacement = NewProject();
        var replacementSelection = new SchachtRecord();
        replacement.SchaechteData.Add(replacementSelection);
        var switched = false;
        harness.ViewModel.PropertyChanged += (_, args) =>
        {
            if (switched || args.PropertyName != nameof(SchaechtePageViewModel.Selected))
                return;

            switched = true;
            harness.Shell.ReplaceProject(replacement);
            harness.ViewModel.Selected = replacementSelection;
        };

        await harness.InvokeFolderImportAsync(harness.ProjectContext);

        Assert.Same(replacementSelection, harness.ViewModel.Selected);
        Assert.True(harness.ExpectedProject.Dirty);
        Assert.False(replacement.Dirty);
        Assert.Equal(0, harness.Dialogs.SaveFileCalls);
    }

    [Fact]
    public async Task Folder_import_adds_new_shaft_while_holding_collection_lock()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        var checkedRecords = new LockCheckingSchachtCollection(harness.Shell.CollectionLock);
        harness.ExpectedProject.SchaechteData = checkedRecords;

        await harness.InvokeFolderImportAsync(harness.ProjectContext);

        Assert.Equal(1, checkedRecords.CheckedInserts);
        Assert.Single(checkedRecords);
    }

    [Fact]
    public void Project_guard_reports_files_written_without_claiming_project_data_changed()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        harness.Shell.ReplaceProject(NewProject());

        var isCurrent = harness.InvokeProjectGuard(
            harness.ProjectContext,
            ProjectOperationImpact.ProjectFilesWritten);

        Assert.False(isCurrent);
        Assert.Equal(
            "Projekt wurde gewechselt: PDF-Verteilung abgeschlossen; Projektdaten wurden nicht uebernommen.",
            harness.ViewModel.LastResult);
        var warning = Assert.Single(harness.Dialogs.Warnings);
        Assert.Contains("Mindestens eine PDF-Datei wurde bereits", warning.Message, StringComparison.Ordinal);
        Assert.Contains("nicht in dessen Projektdaten uebernommen", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Aenderungen wurden uebernommen", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_guard_reports_written_files_and_unsaved_project_data_together()
    {
        using var harness = new Harness(sourceIsProjectDistribution: true);
        harness.Shell.ReplaceProject(NewProject());

        var isCurrent = harness.InvokeProjectGuard(
            harness.ProjectContext,
            ProjectOperationImpact.ProjectFilesWritten |
            ProjectOperationImpact.ProjectDataChanged);

        Assert.False(isCurrent);
        Assert.Equal(
            "Projekt wurde gewechselt: PDF-Verteilung abgeschlossen; Projektdaten uebernommen, aber nicht gespeichert.",
            harness.ViewModel.LastResult);
        var warning = Assert.Single(harness.Dialogs.Warnings);
        Assert.Contains("Mindestens eine PDF-Datei", warning.Message, StringComparison.Ordinal);
        Assert.Contains("Projektdaten wurden uebernommen", warning.Message, StringComparison.Ordinal);
        Assert.Contains("nicht gespeichert", warning.Message, StringComparison.Ordinal);
    }

    private static void AssertStoppedWithoutProjectMutation(
        Harness harness,
        Project replacement)
    {
        Assert.Empty(harness.ExpectedProject.SchaechteData);
        Assert.False(harness.ExpectedProject.Dirty);
        Assert.Equal(DateTime.UnixEpoch, harness.ExpectedProject.ModifiedAtUtc);
        Assert.Empty(replacement.SchaechteData);
        Assert.False(replacement.Dirty);
        Assert.Equal(DateTime.UnixEpoch, replacement.ModifiedAtUtc);
        Assert.Null(harness.ViewModel.Selected);
        Assert.Equal("Vorgang abgebrochen: Projekt wurde gewechselt.", harness.ViewModel.LastResult);
        Assert.Contains(
            harness.Dialogs.Warnings,
            warning => warning.Message.Contains(
                "Projekt wurde waehrend des Einlesens gewechselt",
                StringComparison.Ordinal));
    }

    private static Project NewProject()
        => new() { ModifiedAtUtc = DateTime.UnixEpoch };

    private sealed class Harness : IDisposable
    {
        private readonly DirectoryInfo _tempDirectory;
        private readonly ILoggerFactory _loggerFactory;

        internal Harness(bool sourceIsProjectDistribution)
        {
            _tempDirectory = Directory.CreateTempSubdirectory("schacht-folder-guard-");
            ProjectRoot = Path.Combine(_tempDirectory.FullName, "Projekt");
            DistributionFolder = Path.Combine(ProjectRoot, ProjectStructure.SchaechteVerteilt);
            SourceFolder = sourceIsProjectDistribution
                ? DistributionFolder
                : Path.Combine(_tempDirectory.FullName, "Quelle");
            Directory.CreateDirectory(SourceFolder);
            File.WriteAllText(Path.Combine(SourceFolder, "S-1.pdf"), "Test-PDF");

            Settings = new AppSettings
            {
                EnableRestorePoints = false,
                LastProjectPath = ProjectFileLocator.TargetPath(ProjectRoot)
            };
            Dialogs = new DialogFake();
            _loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(
                Settings,
                new DiagnosticsOptions(),
                _loggerFactory.CreateLogger("test"),
                _loggerFactory)
            {
                Dialogs = Dialogs
            };
            Shell = new ShellViewModel(
                services,
                new SystemMonitorService(enableHardwareSensorInit: false));
            ExpectedProject = NewProject();
            Shell.ReplaceProject(ExpectedProject);
            Protocol = new ProtocolImportFake();
            ViewModel = new SchaechtePageViewModel(
                Shell,
                Settings,
                Dialogs,
                Protocol,
                services.SchachtStammdatenErgaenzung,
                services.SchachtMassnahmenKatalog,
                services.CostStores.CreateProjectCostStore("schacht_empfehlungen.json"),
                services.DropdownOptions,
                services.PdfTextLayerRewrite,
                services.ShellOpen,
                services.ShaftRename,
                services.ExplorerReveal,
                services.SchaechteTemplateColumns,
                services.SchachtFileTargets);
            ProjectContext = new ProjectOperationContext(
                ExpectedProject,
                Settings.LastProjectPath);
        }

        internal string ProjectRoot { get; }
        internal string SourceFolder { get; }
        internal string DistributionFolder { get; }
        internal AppSettings Settings { get; }
        internal DialogFake Dialogs { get; }
        internal ProtocolImportFake Protocol { get; }
        internal ShellViewModel Shell { get; }
        internal Project ExpectedProject { get; }
        internal ProjectOperationContext ProjectContext { get; }
        internal SchaechtePageViewModel ViewModel { get; }

        internal async Task InvokeFolderImportAsync(ProjectOperationContext projectContext)
        {
            var method = typeof(SchaechtePageViewModel).GetMethod(
                "ImportProtocolFolderAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Ordnerimport-Methode fehlt.");
            var task = method.Invoke(
                ViewModel,
                new object[] { projectContext, ProjectRoot, SourceFolder }) as Task
                ?? throw new InvalidOperationException("Ordnerimport lieferte keinen Task.");
            await task;
        }

        internal bool InvokeProjectGuard(
            ProjectOperationContext projectContext,
            ProjectOperationImpact impact)
        {
            var method = typeof(SchaechtePageViewModel).GetMethod(
                "ProjectIsStillOpen",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Projektpruefung fehlt.");
            return (bool)(method.Invoke(
                ViewModel,
                new object[] { projectContext, "Protokoll importieren", impact })
                ?? throw new InvalidOperationException("Projektpruefung lieferte kein Ergebnis."));
        }

        public void Dispose()
        {
            Shell.Dispose();
            _loggerFactory.Dispose();
            try
            {
                _tempDirectory.Delete(recursive: true);
            }
            catch
            {
                // Test-Aufraeumen darf einen inhaltlich erfolgreichen Lauf nicht verdecken.
            }
        }
    }

    private sealed class ProtocolImportFake : ISchachtProtocolImportService
    {
        internal Action? OnParse { get; set; }
        internal int ParseCalls { get; private set; }
        internal int FindCalls { get; private set; }
        internal int ApplyCalls { get; private set; }

        public SchachtProtocolParseResult Parse(string pdfPfad)
        {
            ParseCalls++;
            OnParse?.Invoke();
            return new SchachtProtocolParseResult(
                IstSchachtprotokoll: true,
                Schachtnummer: "S-1",
                Datum: null,
                Funktion: null,
                Schachtform: null,
                Dimension: null,
                Schachttiefe: null,
                PrimaereSchaeden: null,
                Bemerkungen: null,
                Status: null,
                Link: null,
                Schaeden: Array.Empty<(string Bauteil, string Schaden)>());
        }

        public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
        {
            FindCalls++;
            return null;
        }

        public void Apply(
            SchachtRecord ziel,
            SchachtProtocolParseResult ergebnis,
            string pdfPfadFuerFeld)
            => ApplyCalls++;

        public string DistributePdf(
            string projektOrdner,
            string schachtnummer,
            string pdfQuelle)
            => throw new InvalidOperationException(
                "Der Ordnerimport darf den Einzeldatei-Verteiler nicht verwenden.");
    }

    private sealed class LockCheckingSchachtCollection(object collectionLock)
        : ObservableCollection<SchachtRecord>
    {
        internal int CheckedInserts { get; private set; }

        protected override void InsertItem(int index, SchachtRecord item)
        {
            Assert.True(
                Monitor.IsEntered(collectionLock),
                "Der Ordnerimport hat einen Schacht ohne CollectionLock eingefuegt.");
            CheckedInserts++;
            base.InsertItem(index, item);
        }
    }

    private sealed class DialogFake : IDialogService
    {
        internal int ConfirmWarnCalls { get; private set; }
        internal int SaveFileCalls { get; private set; }
        internal List<(string Message, string Title)> Warnings { get; } = new();

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(
            string title,
            string filter,
            string? defaultExt = null,
            string? defaultFileName = null)
        {
            SaveFileCalls++;
            return null;
        }

        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") => Warnings.Add((message, title));
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;

        public bool ConfirmWarn(
            string message,
            string title = "Bestaetigung",
            bool defaultNo = true)
        {
            ConfirmWarnCalls++;
            return true;
        }

        public DialogConfirm ConfirmCancel(
            string message,
            string title = "Bestaetigung")
            => DialogConfirm.Cancel;
    }
}
