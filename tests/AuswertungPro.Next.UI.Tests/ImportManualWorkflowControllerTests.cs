using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ImportManualWorkflowControllerTests
{
    [Fact]
    public async Task Empty_selections_do_not_start_an_import_run()
    {
        var dialogs = new DialogFake
        {
            OpenFilesResult = [],
            SelectedFolder = ""
        };
        var pdf = new PdfImportFake();
        var xtf = new XtfImportFake();
        var folders = new FolderImportFake();
        var storedFiles = new StoredImportFileFake();
        var state = new WorkflowState();
        var controller = CreateController(dialogs, pdf, xtf, folders, storedFiles);
        var context = CreateContext(state, new Project());

        await controller.ImportPdfAsync(context);
        await controller.ImportXtfAsync(context);
        await controller.ImportWinCanAsync(context);
        await controller.ImportIbakAsync(context);
        await controller.ImportKinsAsync(context);

        Assert.Empty(pdf.Calls);
        Assert.Empty(xtf.Calls);
        Assert.Empty(folders.Calls);
        Assert.Empty(storedFiles.Calls);
        Assert.Empty(state.RestoreLabels);
        Assert.Equal(0, state.SaveCount);
        Assert.Null(state.ReplacedProject);
    }

    [Fact]
    public async Task Pdf_import_keeps_batch_totals_continues_after_file_error_and_stores_sources()
    {
        var firstPath = @"C:\Import\defekt.pdf";
        var secondPath = @"C:\Import\ok.pdf";
        var dialogs = new DialogFake { OpenFilesResult = [firstPath, secondPath] };
        var pdf = new PdfImportFake(call =>
            call.Path == firstPath
                ? Result<ImportStats>.Fail("PDF_TEST", "nicht lesbar")
                : Result<ImportStats>.Success(new ImportStats(
                    Found: 4,
                    Created: 1,
                    Updated: 2,
                    Errors: 1,
                    Uncertain: 3,
                    Messages: ["Hinweis aus Datei"])));
        var storedFiles = new StoredImportFileFake();
        var state = new WorkflowState();
        var controller = CreateController(
            dialogs,
            pdf,
            new XtfImportFake(),
            new FolderImportFake(),
            storedFiles,
            pdfToTextPath: @"C:\Tools\pdftotext.exe");

        await controller.ImportPdfAsync(CreateContext(
            state,
            new Project(),
            fillMissingOnly: true,
            projectPath: @"C:\Projekt\projekt.json"));

        Assert.Equal("PDF importieren", dialogs.LastOpenFilesTitle);
        Assert.Equal("PDF (*.pdf)|*.pdf", dialogs.LastOpenFilesFilter);
        Assert.Equal(new[] { firstPath, secondPath }, pdf.Calls.Select(call => call.Path));
        Assert.All(pdf.Calls, call => Assert.True(call.FillMissingOnly));
        Assert.All(pdf.Calls, call => Assert.Equal(@"C:\Tools\pdftotext.exe", call.PdfToTextPath));
        Assert.Contains("Haltungen: 4 gefunden, 1 neu, 2 aktualisiert", state.Summary);
        Assert.Contains("Fehler: 2, Unklar: 3", state.Summary);
        Assert.Contains("Error: defekt.pdf: nicht lesbar", state.Details);
        Assert.Contains("ok.pdf: Hinweis aus Datei", state.Details);
        var stored = Assert.Single(storedFiles.Calls);
        Assert.Equal("PDF", stored.ImportKind);
        Assert.Equal(@"C:\Projekt\projekt.json", stored.ProjectPath);
        Assert.Equal(new[] { firstPath, secondPath }, stored.Paths);
        Assert.Equal("PDF", state.ReplacedProject!.Metadata["ImportQuellTyp"]);
        Assert.Equal(@"C:\Import", state.ReplacedProject.Metadata["ImportQuelle"]);
        Assert.Equal(firstPath, state.LastLog!.SourcePath);
        Assert.Equal(new[] { "PDF" }, state.RestoreLabels);
        Assert.Equal(1, state.SaveCount);
    }

    [Fact]
    public async Task Pdf_exception_stops_the_batch_and_keeps_live_project_unchanged()
    {
        var paths = new[] { @"C:\Import\defekt.pdf", @"C:\Import\nicht-mehr.pdf" };
        var dialogs = new DialogFake { OpenFilesResult = paths };
        var pdf = new PdfImportFake(_ => throw new IOException("Testfehler"));
        var storedFiles = new StoredImportFileFake();
        var state = new WorkflowState();
        var controller = CreateController(
            dialogs,
            pdf,
            new XtfImportFake(),
            new FolderImportFake(),
            storedFiles);

        await controller.ImportPdfAsync(CreateContext(state, new Project()));

        Assert.Single(pdf.Calls);
        Assert.Contains("PDF Import fehlgeschlagen - Projektdaten wurden nicht uebernommen", state.Summary);
        Assert.Empty(storedFiles.Calls);
        Assert.Equal(0, state.SaveCount);
        Assert.Null(state.ReplacedProject);
    }

    [Fact]
    public async Task Xtf_import_uses_exact_dialog_and_stores_sources_after_commit()
    {
        var paths = new[] { @"C:\Import\daten.xtf", @"C:\Import\mehr.m150" };
        var dialogs = new DialogFake { OpenFilesResult = paths };
        var xtf = new XtfImportFake();
        var storedFiles = new StoredImportFileFake
        {
            ResultToReturn = new StoredImportFilesResult(true, [])
            {
                Errors = [new StoredImportFileError(paths[1], "Testfehler")]
            }
        };
        var state = new WorkflowState();
        var controller = CreateController(
            dialogs,
            new PdfImportFake(),
            xtf,
            new FolderImportFake(),
            storedFiles);

        await controller.ImportXtfAsync(CreateContext(
            state,
            new Project(),
            projectPath: @"C:\Projekt\projekt.json"));

        Assert.Equal("Daten importieren (XTF/M150/MDB)", dialogs.LastOpenFilesTitle);
        Assert.Equal(
            "Daten (*.xtf;*.m150;*.mdb;*.xml)|*.xtf;*.m150;*.mdb;*.xml|XTF (*.xtf)|*.xtf|M150/XML (*.m150;*.xml)|*.m150;*.xml|MDB (*.mdb)|*.mdb|Alle Dateien|*.*",
            dialogs.LastOpenFilesFilter);
        var import = Assert.Single(xtf.Calls);
        Assert.Equal(paths, import.Paths);
        Assert.False(import.Context.DryRun);
        var stored = Assert.Single(storedFiles.Calls);
        Assert.Equal("XTF", stored.ImportKind);
        Assert.Equal(paths, stored.Paths);
        Assert.Equal("XTF", state.ReplacedProject!.Metadata["ImportQuellTyp"]);
        Assert.Equal(@"C:\Import", state.ReplacedProject.Metadata["ImportQuelle"]);
        Assert.Equal(paths[0], state.LastLog!.SourcePath);
        Assert.Contains("XTF Import:", state.Summary);
        Assert.Contains("Haltungen: 1 gefunden", state.Summary);
        Assert.Contains("Projekt bitte speichern", state.Summary);
        Assert.Contains("1 XTF-Dateien konnten nicht", state.Summary);
        Assert.Equal("", state.LastResult);
        Assert.Equal(1, state.SaveCount);
    }

    [Theory]
    [InlineData("WinCan", "WinCan-Projektordner waehlen")]
    [InlineData("IBAK", "IBAK-Projektordner waehlen")]
    [InlineData("KINS", "KINS-Projektordner waehlen")]
    public async Task Folder_imports_share_the_same_post_processing(
        string importKind,
        string expectedDialogTitle)
    {
        var sourceFolder = $@"C:\Import\{importKind}";
        var dialogs = new DialogFake { SelectedFolder = sourceFolder };
        var folderImports = new FolderImportFake();
        var state = new WorkflowState();
        var controller = CreateController(
            dialogs,
            new PdfImportFake(),
            new XtfImportFake(),
            folderImports,
            new StoredImportFileFake());
        var context = CreateContext(state, new Project());

        await RunFolderImportAsync(controller, importKind, context);

        Assert.Equal(expectedDialogTitle, dialogs.LastSelectFolderTitle);
        var import = Assert.Single(folderImports.Calls);
        Assert.Equal(importKind, import.ImportKind);
        Assert.Equal(sourceFolder, import.SourceFolder);
        Assert.Equal(importKind, state.ReplacedProject!.Metadata["ImportQuellTyp"]);
        Assert.Equal(sourceFolder, state.ReplacedProject.Metadata["ImportQuelle"]);
        Assert.Equal(importKind, state.LastLog!.ImportType);
        Assert.Equal(sourceFolder, state.LastLog.SourcePath);
        Assert.Equal(new[] { importKind }, state.RestoreLabels);
        Assert.Equal(1, state.SaveCount);
    }

    [Fact]
    public async Task Rejected_preview_does_not_commit_store_or_save()
    {
        var dialogs = new DialogFake { OpenFilesResult = [@"C:\Import\vorschau.pdf"] };
        var pdf = new PdfImportFake();
        var storedFiles = new StoredImportFileFake();
        var state = new WorkflowState { PreviewDecision = false };
        var controller = CreateController(
            dialogs,
            pdf,
            new XtfImportFake(),
            new FolderImportFake(),
            storedFiles);

        await controller.ImportPdfAsync(CreateContext(
            state,
            new Project(),
            showPreviewFirst: true));

        var call = Assert.Single(pdf.Calls);
        Assert.True(call.Context.DryRun);
        Assert.True(state.PreviewWasShown);
        Assert.Empty(state.RestoreLabels);
        Assert.Empty(storedFiles.Calls);
        Assert.Equal(0, state.SaveCount);
        Assert.Null(state.ReplacedProject);
    }

    private static ImportManualWorkflowController CreateController(
        IDialogService dialogs,
        IPdfImportService pdfImport,
        IXtfImportService xtfImport,
        FolderImportFake folderImports,
        IStoredImportFileService storedImportFiles,
        string? pdfToTextPath = null)
        => new(
            dialogs,
            pdfImport,
            xtfImport,
            folderImports,
            folderImports,
            folderImports,
            new SchachtProImportFake(),
            storedImportFiles,
            new FileStagingServiceFake(),
            new MediaDistributionServiceFake(),
            pdfToTextPath);

    private static ImportManualWorkflowContext CreateContext(
        WorkflowState state,
        Project project,
        bool showPreviewFirst = false,
        bool fillMissingOnly = false,
        string? projectPath = null)
        => new(
            ShowPreviewFirst: showPreviewFirst,
            FillMissingOnly: fillMissingOnly,
            ProjectPath: projectPath,
            ProjectFolder: null,
            WorkflowActions: CreateActions(state, project, projectPath),
            CancellationToken: CancellationToken.None);

    private static ImportRunWorkflowActions CreateActions(
        WorkflowState state,
        Project project,
        string? projectPath)
        => new(
            GetProject: () => project,
            GetProjectPath: () => projectPath,
            DeepCopyProject: source => new Project
            {
                Name = source.Name,
                Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal)
            },
            ReplaceProject: replacement => state.ReplacedProject = replacement,
            CreateRestorePoint: state.RestoreLabels.Add,
            GetReportDir: () => "reports",
            ExportReport: (log, _) =>
            {
                state.LastLog = log;
                return "report.txt";
            },
            ShowPreview: (_, _) =>
            {
                state.PreviewWasShown = true;
                return state.PreviewDecision;
            },
            ValidatePlausibility: _ => [],
            DeduplicateAllPrimaryDamages: _ => null,
            RunAfterImportAsync: (_, _) => Task.CompletedTask,
            SaveProject: () =>
            {
                state.SaveCount++;
                return true;
            },
            SetStatus: value => state.Status = value,
            SetCanCancel: value => state.CanCancel = value,
            SetIsImportInProgress: value => state.IsImportInProgress = value,
            SetProgressPercent: value => state.ProgressPercent = value,
            SetPhase: value => state.Phase = value,
            SetProgressText: value => state.Progress = value,
            GetSummaryText: () => state.Summary,
            SetSummaryText: value => state.Summary = value,
            GetDetailsText: () => state.Details,
            SetDetailsText: value => state.Details = value,
            SetLastReportPath: value => state.LastReportPath = value,
            CollectionLock: new object());

    private static Task RunFolderImportAsync(
        ImportManualWorkflowController controller,
        string importKind,
        ImportManualWorkflowContext context)
        => importKind switch
        {
            "WinCan" => controller.ImportWinCanAsync(context),
            "IBAK" => controller.ImportIbakAsync(context),
            "KINS" => controller.ImportKinsAsync(context),
            _ => throw new ArgumentOutOfRangeException(nameof(importKind), importKind, null)
        };

    private sealed class DialogFake : IDialogService
    {
        public string[] OpenFilesResult { get; init; } = [];
        public string? SelectedFolder { get; init; }
        public string LastOpenFilesTitle { get; private set; } = "";
        public string LastOpenFilesFilter { get; private set; } = "";
        public string LastSelectFolderTitle { get; private set; } = "";

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter)
        {
            LastOpenFilesTitle = title;
            LastOpenFilesFilter = filter;
            return OpenFilesResult;
        }

        public string? SelectFolder(string title, string? initialPath = null)
        {
            LastSelectFolderTitle = title;
            return SelectedFolder;
        }

        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }

    private sealed class PdfImportFake(
        Func<PdfCall, Result<ImportStats>>? import = null) : IPdfImportService
    {
        public List<PdfCall> Calls { get; } = [];

        public Result<ImportStats> ImportPdf(
            string pdfPath,
            Project project,
            string? pdfToTextPath,
            bool fillMissingOnly = false,
            ImportRunContext? ctx = null)
        {
            var call = new PdfCall(
                pdfPath,
                pdfToTextPath,
                fillMissingOnly,
                ctx ?? throw new InvalidOperationException("ImportRunContext fehlt."));
            Calls.Add(call);
            return import?.Invoke(call) ?? SuccessStats();
        }
    }

    private sealed class XtfImportFake : IXtfImportService
    {
        public List<XtfCall> Calls { get; } = [];

        public Result<ImportStats> ImportXtfFiles(
            IEnumerable<string> xtfPaths,
            Project project,
            ImportRunContext? ctx = null)
        {
            Calls.Add(new XtfCall(
                xtfPaths.ToArray(),
                ctx ?? throw new InvalidOperationException("ImportRunContext fehlt.")));
            return SuccessStats();
        }
    }

    private sealed class FolderImportFake :
        IWinCanDbImportService,
        IIbakImportService,
        IKinsImportService
    {
        public List<FolderCall> Calls { get; } = [];

        public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null)
            => Record("WinCan", exportRoot, ctx);

        public Result<ImportStats> ImportIbakExport(string exportRoot, Project project, ImportRunContext? ctx = null)
            => Record("IBAK", exportRoot, ctx);

        public Result<ImportStats> ImportKinsExport(string exportRoot, Project project, ImportRunContext? ctx = null)
            => Record("KINS", exportRoot, ctx);

        private Result<ImportStats> Record(string importKind, string sourceFolder, ImportRunContext? context)
        {
            Calls.Add(new FolderCall(
                importKind,
                sourceFolder,
                context ?? throw new InvalidOperationException("ImportRunContext fehlt.")));
            return SuccessStats();
        }
    }

    private sealed class StoredImportFileFake : IStoredImportFileService
    {
        public List<StoredFileCall> Calls { get; } = [];
        public StoredImportFilesResult ResultToReturn { get; init; } =
            new(false, []);

        public StoredImportFilesResult Store(
            string? projectPath,
            IDictionary<string, string> metadata,
            string importKind,
            IReadOnlyCollection<string> paths,
            Func<DateTime>? now = null)
        {
            Calls.Add(new StoredFileCall(projectPath, importKind, paths.ToArray()));
            return ResultToReturn;
        }
    }

    private sealed class SchachtProImportFake : ISchachtProImportService
    {
        public Result<ImportStats> ImportSchachtProArchive(string sproPath, Project project, ImportRunContext? ctx = null)
            => SuccessStats();
    }

    private sealed class FileStagingServiceFake : IImportFileStagingService
    {
        public IImportFileStagingSession? Begin(string? projectPath) => null;
    }

    private sealed class MediaDistributionServiceFake : IImportMediaDistributionService
    {
        public ImportMediaDistributionResult Distribute(ImportMediaDistributionRequest request)
            => new(0, 0, 0, []);
    }

    private static Result<ImportStats> SuccessStats()
        => Result<ImportStats>.Success(new ImportStats(1, 1, 0, 0, 0, []));

    private sealed record PdfCall(
        string Path,
        string? PdfToTextPath,
        bool FillMissingOnly,
        ImportRunContext Context);

    private sealed record XtfCall(string[] Paths, ImportRunContext Context);
    private sealed record FolderCall(string ImportKind, string SourceFolder, ImportRunContext Context);
    private sealed record StoredFileCall(string? ProjectPath, string ImportKind, string[] Paths);

    private sealed class WorkflowState
    {
        public bool PreviewDecision { get; init; }
        public bool PreviewWasShown { get; set; }
        public bool CanCancel { get; set; }
        public bool IsImportInProgress { get; set; }
        public double ProgressPercent { get; set; }
        public int SaveCount { get; set; }
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public string Phase { get; set; } = "";
        public string Progress { get; set; } = "";
        public string Status { get; set; } = "";
        public string LastResult { get; set; } = "";
        public string LastReportPath { get; set; } = "";
        public Project? ReplacedProject { get; set; }
        public ImportRunLog? LastLog { get; set; }
        public List<string> RestoreLabels { get; } = [];
    }
}
