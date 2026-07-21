using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ImportManualWorkflowContext(
    bool ShowPreviewFirst,
    bool FillMissingOnly,
    string? ProjectPath,
    string? ProjectFolder,
    ImportRunWorkflowActions WorkflowActions,
    CancellationToken CancellationToken);

internal sealed class ImportManualWorkflowController
{
    private const string XtfDialogFilter =
        "Daten (*.xtf;*.m150;*.mdb;*.xml)|*.xtf;*.m150;*.mdb;*.xml|XTF (*.xtf)|*.xtf|M150/XML (*.m150;*.xml)|*.m150;*.xml|MDB (*.mdb)|*.mdb|Alle Dateien|*.*";

    private readonly IDialogService _dialogs;
    private readonly IPdfImportService _pdfImport;
    private readonly IXtfImportService _xtfImport;
    private readonly IWinCanDbImportService _winCanImport;
    private readonly IIbakImportService _ibakImport;
    private readonly IKinsImportService _kinsImport;
    private readonly IStoredImportFileService _storedImportFiles;
    private readonly IImportFileStagingService _fileStaging;
    private readonly IImportMediaDistributionService _mediaDistribution;
    private readonly string? _pdfToTextPath;

    internal ImportManualWorkflowController(
        IDialogService dialogs,
        IPdfImportService pdfImport,
        IXtfImportService xtfImport,
        IWinCanDbImportService winCanImport,
        IIbakImportService ibakImport,
        IKinsImportService kinsImport,
        IStoredImportFileService storedImportFiles,
        IImportFileStagingService fileStaging,
        IImportMediaDistributionService mediaDistribution,
        string? pdfToTextPath)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _pdfImport = pdfImport ?? throw new ArgumentNullException(nameof(pdfImport));
        _xtfImport = xtfImport ?? throw new ArgumentNullException(nameof(xtfImport));
        _winCanImport = winCanImport ?? throw new ArgumentNullException(nameof(winCanImport));
        _ibakImport = ibakImport ?? throw new ArgumentNullException(nameof(ibakImport));
        _kinsImport = kinsImport ?? throw new ArgumentNullException(nameof(kinsImport));
        _storedImportFiles = storedImportFiles ?? throw new ArgumentNullException(nameof(storedImportFiles));
        _fileStaging = fileStaging ?? throw new ArgumentNullException(nameof(fileStaging));
        _mediaDistribution = mediaDistribution ?? throw new ArgumentNullException(nameof(mediaDistribution));
        _pdfToTextPath = pdfToTextPath;
    }

    internal Task ImportPdfAsync(ImportManualWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var paths = _dialogs.OpenFiles("PDF importieren", "PDF (*.pdf)|*.pdf");
        if (paths.Length == 0)
            return Task.CompletedTask;

        return RunAsync(
            "PDF",
            paths,
            (source, project, runContext) => ImportPdfBatch(
                source,
                project,
                runContext,
                context),
            (source, project, runContext) => PostImportFilesAsync(
                source,
                project,
                runContext,
                context,
                "PDF",
                "PDF-Dateien"),
            context);
    }

    internal Task ImportXtfAsync(ImportManualWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var paths = _dialogs.OpenFiles("Daten importieren (XTF/M150/MDB)", XtfDialogFilter);
        if (paths.Length == 0)
            return Task.CompletedTask;

        return RunAsync(
            "XTF",
            paths,
            (source, project, runContext) => _xtfImport.ImportXtfFiles(source, project, runContext),
            (source, project, runContext) => PostImportFilesAsync(
                source,
                project,
                runContext,
                context,
                "XTF",
                "XTF-Dateien"),
            context);
    }

    internal Task ImportWinCanAsync(ImportManualWorkflowContext context)
        => ImportFolderAsync(
            "WinCan",
            "WinCan-Projektordner waehlen",
            _winCanImport.ImportWinCanExport,
            context);

    internal Task ImportIbakAsync(ImportManualWorkflowContext context)
        => ImportFolderAsync(
            "IBAK",
            "IBAK-Projektordner waehlen",
            _ibakImport.ImportIbakExport,
            context);

    internal Task ImportKinsAsync(ImportManualWorkflowContext context)
        => ImportFolderAsync(
            "KINS",
            "KINS-Projektordner waehlen",
            _kinsImport.ImportKinsExport,
            context);

    private Task ImportFolderAsync(
        string label,
        string dialogTitle,
        Func<string, Project, ImportRunContext?, Result<ImportStats>> import,
        ImportManualWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var folder = _dialogs.SelectFolder(dialogTitle);
        if (string.IsNullOrWhiteSpace(folder))
            return Task.CompletedTask;

        return RunAsync(
            label,
            folder,
            (source, project, runContext) => import(source, project, runContext),
            (source, project, runContext) => PostImportFolderAsync(
                source,
                project,
                runContext,
                context),
            context);
    }

    private Task RunAsync<TSource>(
        string label,
        TSource source,
        Func<TSource, Project, ImportRunContext, Result<ImportStats>> import,
        Func<TSource, Project, ImportRunContext, Task> postImportAsync,
        ImportManualWorkflowContext context)
        => ImportRunWorkflowController.RunAsync(
            new ImportRunWorkflowRequest<TSource>(
                Label: label,
                Source: source,
                Import: import,
                DryRun: context.ShowPreviewFirst,
                PostImportAsync: postImportAsync,
                SaveProjectAfterCommit: true,
                BeginFileStaging: _fileStaging.Begin),
            context.WorkflowActions,
            context.CancellationToken);

    private Result<ImportStats> ImportPdfBatch(
        string[] paths,
        Project project,
        ImportRunContext runContext,
        ImportManualWorkflowContext context)
    {
        var totalFound = 0;
        var totalCreated = 0;
        var totalUpdated = 0;
        var totalUncertain = 0;
        var totalErrors = 0;
        var messages = new List<string>();

        for (var index = 0; index < paths.Length; index++)
        {
            runContext.CancellationToken.ThrowIfCancellationRequested();
            var path = paths[index];
            runContext.Progress?.Report(new ImportProgress(
                "PDF lesen",
                index + 1,
                paths.Length,
                $"PDF {index + 1}/{paths.Length}: {Path.GetFileName(path)}",
                Path.GetFileName(path)));

            var result = _pdfImport.ImportPdf(
                path,
                project,
                _pdfToTextPath,
                context.FillMissingOnly,
                runContext);
            if (!result.Ok || result.Value is null)
            {
                totalErrors++;
                messages.Add($"Error: {Path.GetFileName(path)}: {result.ErrorMessage}");
                continue;
            }

            totalFound += result.Value.Found;
            totalCreated += result.Value.Created;
            totalUpdated += result.Value.Updated;
            totalUncertain += result.Value.Uncertain;
            totalErrors += result.Value.Errors;
            foreach (var message in result.Value.Messages)
                messages.Add($"{Path.GetFileName(path)}: {message}");
        }

        return Result<ImportStats>.Success(new ImportStats(
            totalFound,
            totalCreated,
            totalUpdated,
            totalErrors,
            totalUncertain,
            messages));
    }

    private Task PostImportFilesAsync(
        string[] paths,
        Project project,
        ImportRunContext runContext,
        ImportManualWorkflowContext context,
        string importKind,
        string displayName)
    {
        if (runContext.DryRun)
            return Task.CompletedTask;

        var result = runContext.FileStaging is null
            ? _storedImportFiles.Store(
                context.ProjectPath,
                project.Metadata,
                importKind,
                paths)
            : _storedImportFiles.StoreStaged(
                context.ProjectPath,
                project.Metadata,
                importKind,
                paths,
                runContext.FileStaging,
                runContext.CancellationToken);

        if (result.MissingProjectPath)
        {
            AppendSummaryNotice(
                context.WorkflowActions,
                $"Hinweis: Projekt bitte speichern, um {displayName} im Projekt abzulegen.");
        }

        if (result.Errors.Count > 0)
        {
            AppendSummaryNotice(
                context.WorkflowActions,
                $"Hinweis: {result.Errors.Count} {displayName} konnten nicht im Projekt abgelegt werden.");
        }

        if (paths.Length > 0)
        {
            ImportPostProcessingController.TrackImportSource(
                project,
                Path.GetDirectoryName(paths[0]) ?? paths[0],
                importKind,
                DateTime.Now);
        }

        return Task.CompletedTask;
    }

    private static void AppendSummaryNotice(
        ImportRunWorkflowActions actions,
        string notice)
    {
        var summary = actions.GetSummaryText();
        actions.SetSummaryText(string.IsNullOrWhiteSpace(summary)
            ? notice
            : summary + "\n" + notice);
    }

    private Task PostImportFolderAsync(
        string folder,
        Project project,
        ImportRunContext runContext,
        ImportManualWorkflowContext context)
    {
        if (runContext.DryRun)
            return Task.CompletedTask;

        return ImportPostProcessingController.RunAsync(
            new ImportPostProcessingRequest(
                SourceFolder: folder,
                SourceLabel: runContext.Log.ImportType,
                Project: project,
                ProjectFolder: context.ProjectFolder,
                PdfImport: _pdfImport,
                MediaDistribution: _mediaDistribution,
                PdfToTextPath: _pdfToTextPath,
                FillMissingOnly: context.FillMissingOnly,
                Context: runContext,
                CollectionLock: context.WorkflowActions.CollectionLock),
            new ImportPostProcessingActions(
                SetProgressText: context.WorkflowActions.SetProgressText,
                SetProgressPercent: context.WorkflowActions.SetProgressPercent,
                AppendSummaryText: value => context.WorkflowActions.SetSummaryText(
                    context.WorkflowActions.GetSummaryText() + value),
                AppendDetailsText: value => context.WorkflowActions.SetDetailsText(
                    context.WorkflowActions.GetDetailsText() + value),
                SetStatus: context.WorkflowActions.SetStatus));
    }
}
