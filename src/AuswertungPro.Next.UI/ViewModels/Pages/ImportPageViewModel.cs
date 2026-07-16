using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ImportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _settings;
    private readonly AuswertungPro.Next.Application.Projects.IProjectRepository _projects;
    private readonly IPdfImportService _pdfImport;
    private readonly IXtfImportService _xtfImport;
    private readonly IWinCanDbImportService _winCanImport;
    private readonly IIbakImportService _ibakImport;
    private readonly IKinsImportService _kinsImport;
    private readonly IStoredImportFileService _storedImportFiles;
    private readonly IImportRunReportExporter _importRunReports;
    private readonly string? _pdfToTextPath;
    private readonly Services.ImportProjectPortabilityController _projectPortabilityController;
    private readonly Services.ImportProjectPhotoAssignmentController _projectPhotoAssignmentController;
    private readonly Services.ImportProtocolDistributionController _protocolDistributionController;
    private readonly Services.ImportProtocolRegenerationController _protocolRegenerationController;
    private readonly Services.ImportOneClickProjectController _oneClickProjectController;
    private readonly Services.ImportReportNavigationController _reportNavigationController;
    private readonly Services.ImportSummaryExportController _summaryExportController;
    private readonly Services.ImportCatalogController _catalogController;
    private readonly Services.ImportVsaEvaluationController _vsaEvaluationController;

    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _detailsText = "";
    [ObservableProperty] private string _importProgress = "";
    [ObservableProperty] private double _importProgressPercent;
    [ObservableProperty] private string _importPhase = "";
    [ObservableProperty] private bool _isImportInProgress;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _showPreviewFirst;
    [ObservableProperty] private string _catalogStatus = "";
    [ObservableProperty] private bool _isCatalogOk;
    [ObservableProperty] private bool _fillMissingOnly;

    private CancellationTokenSource? _importCts;

    public IAsyncRelayCommand ImportPdfCommand { get; }
    public IAsyncRelayCommand ImportSchachtPdfsFolderCommand { get; }
    public IAsyncRelayCommand ImportXtfCommand { get; }
    public IAsyncRelayCommand ImportWinCanCommand { get; }
    public IAsyncRelayCommand ImportIbakCommand { get; }
    public IAsyncRelayCommand ImportKinsCommand { get; }
    public IRelayCommand ExportImportSummaryCommand { get; }
    public IRelayCommand ReloadCatalogCommand { get; }
    public IRelayCommand CancelImportCommand { get; }
    public IRelayCommand OpenLastReportCommand { get; }
    public IRelayCommand OpenReportFolderCommand { get; }
    public IAsyncRelayCommand MakeProjectPortableCommand { get; }
    public IAsyncRelayCommand AssignPhotosFromFolderCommand { get; }
    public IAsyncRelayCommand ImportKanalProjektCommand { get; }
    public IAsyncRelayCommand ProtokollNeuGenerierenCommand { get; }

    public ImportPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        ArgumentNullException.ThrowIfNull(sp);
        _dialogs = sp.Dialogs;
        _settings = sp.Settings;
        _projects = sp.Projects;
        _pdfImport = sp.PdfImport;
        _xtfImport = sp.XtfImport;
        _winCanImport = sp.WinCanImport;
        _ibakImport = sp.IbakImport;
        _kinsImport = sp.KinsImport;
        _storedImportFiles = sp.StoredImportFiles;
        _importRunReports = sp.ImportRunReports;
        _pdfToTextPath = sp.Diagnostics.ExplicitPdfToTextPath;
        var oneClickImporter = sp.CreateOneClickProjectImportService();
        var resolvedCatalogPath = sp.VsaCatalogResolvedPath;
        _projectPortabilityController = new Services.ImportProjectPortabilityController(
            _dialogs,
            sp.ProjectPortability);
        _projectPhotoAssignmentController = new Services.ImportProjectPhotoAssignmentController(
            _dialogs,
            sp.ProjectPhotoAssignment);
        _protocolDistributionController = new Services.ImportProtocolDistributionController(
            _dialogs,
            sp.NameBasedProtocolDistributor,
            sp.Logger);
        _protocolRegenerationController = new Services.ImportProtocolRegenerationController(
            _dialogs,
            sp.ProtocolRegeneration,
            sp.CodeCatalog);
        _oneClickProjectController = new Services.ImportOneClickProjectController(
            _dialogs,
            () => oneClickImporter,
            sp.OneClickImportReports);
        _reportNavigationController = new Services.ImportReportNavigationController(
            _dialogs,
            () => _settings.LastProjectPath,
            path => Services.SafeShellOpen.TryOpen(path, out _));
        _summaryExportController = new Services.ImportSummaryExportController(
            _dialogs,
            sp.ImportSummaryExporter,
            sp.Logger);
        _catalogController = new Services.ImportCatalogController(
            () => _settings.VsaCatalogSecXmlPath,
            () => _settings.VsaCatalogNodXmlPath,
            () => resolvedCatalogPath,
            sp.CodeCatalog,
            sp.Logger);
        _vsaEvaluationController = new Services.ImportVsaEvaluationController(
            sp.Vsa,
            sp.Logger);

        ImportPdfCommand = new AsyncRelayCommand(ImportPdfAsync, CanStartImport);
        ImportSchachtPdfsFolderCommand = new AsyncRelayCommand(ImportSchachtPdfsFolderAsync, CanStartImport);
        ImportXtfCommand = new AsyncRelayCommand(ImportXtfAsync, CanStartImport);
        ImportWinCanCommand = new AsyncRelayCommand(ImportWinCanAsync, CanStartImport);
        ImportIbakCommand = new AsyncRelayCommand(ImportIbakAsync, CanStartImport);
        ImportKinsCommand = new AsyncRelayCommand(ImportKinsAsync, CanStartImport);
        ExportImportSummaryCommand = new RelayCommand(ExportImportSummary);
        ReloadCatalogCommand = new RelayCommand(ReloadCatalog);
        CancelImportCommand = new RelayCommand(CancelImport, () => CanCancel);
        OpenLastReportCommand = new RelayCommand(_reportNavigationController.OpenLastReport);
        OpenReportFolderCommand = new RelayCommand(_reportNavigationController.OpenReportFolder);
        MakeProjectPortableCommand = new AsyncRelayCommand(MakeProjectPortableAsync);
        AssignPhotosFromFolderCommand = new AsyncRelayCommand(AssignPhotosFromFolderAsync);
        ImportKanalProjektCommand = new AsyncRelayCommand(ImportKanalProjektAsync, CanStartImport);
        ProtokollNeuGenerierenCommand = new AsyncRelayCommand(ProtokollNeuGenerierenAsync);

        ApplyCatalogStatus(_catalogController.GetStatus());
    }

    private bool CanStartImport()
        => !IsImportInProgress;

    partial void OnIsImportInProgressChanged(bool value)
    {
        _ = value;
        ImportPdfCommand.NotifyCanExecuteChanged();
        ImportSchachtPdfsFolderCommand.NotifyCanExecuteChanged();
        ImportXtfCommand.NotifyCanExecuteChanged();
        ImportWinCanCommand.NotifyCanExecuteChanged();
        ImportIbakCommand.NotifyCanExecuteChanged();
        ImportKinsCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanCancelChanged(bool value)
    {
        _ = value;
        (CancelImportCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    // ──── Cancel ────

    private void CancelImport()
    {
        _importCts?.Cancel();
        CanCancel = false;
        ImportPhase = "Abbruch angefordert...";
    }

    // ──── Generic Orchestrator ────

    private async Task RunImportAsync<TArg>(
        string label,
        TArg source,
        Func<TArg, Project, ImportRunContext, Result<ImportStats>> importFunc,
        bool dryRun = false,
        Func<TArg, Project, ImportRunContext, Task>? postImportAsync = null,
        bool saveProjectAfterCommit = false)
    {
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();

        await Services.ImportRunWorkflowController.RunAsync(
            new Services.ImportRunWorkflowRequest<TArg>(
                label,
                source,
                importFunc,
                dryRun,
                postImportAsync,
                saveProjectAfterCommit),
            new Services.ImportRunWorkflowActions(
                GetProject: () => _shell.Project,
                DeepCopyProject: _projects.DeepCopy,
                ReplaceProject: _shell.ReplaceProject,
                CreateRestorePoint: _shell.TryCreateImportRestorePoint,
                GetReportDir: _reportNavigationController.GetReportDirectory,
                ExportReport: _importRunReports.Export,
                ShowPreview: ShowPreviewWindow,
                ValidatePlausibility: Application.Import.ImportPlausibilityValidator.Validate,
                DeduplicateAllPrimaryDamages: DeduplicateAllPrimaryDamages,
                RunAfterImportAsync: RunVsaAfterImport,
                SaveProject: () => _ = _shell.TrySaveProject(),
                SetStatus: _shell.SetStatus,
                SetCanCancel: value => CanCancel = value,
                SetIsImportInProgress: value => IsImportInProgress = value,
                SetProgressPercent: value => ImportProgressPercent = value,
                SetPhase: value => ImportPhase = value,
                SetProgressText: value => ImportProgress = value,
                GetSummaryText: () => SummaryText,
                SetSummaryText: value => SummaryText = value,
                GetDetailsText: () => DetailsText,
                SetDetailsText: value => DetailsText = value,
                SetLastReportPath: _reportNavigationController.SetLastReportPath,
                CollectionLock: _shell.CollectionLock),
            _importCts.Token);
    }

    private Task RunImportWithOptionalPreviewAsync<TArg>(
        string label,
        TArg source,
        Func<TArg, Project, ImportRunContext, Result<ImportStats>> importFunc,
        Func<TArg, Project, ImportRunContext, Task>? postImportAsync = null,
        bool saveProjectAfterCommit = false)
        => RunImportAsync(
            label,
            source,
            importFunc,
            dryRun: ShowPreviewFirst,
            postImportAsync: postImportAsync,
            saveProjectAfterCommit: saveProjectAfterCommit);

    private bool ShowPreviewWindow(ImportPreviewResult preview, string label)
    {
        var win = new Views.Windows.ImportPreviewWindow(preview, label)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return win.ShowDialog() == true;
    }

    // ──── Import Methods ────

    private async Task ImportPdfAsync()
    {
        var paths = _dialogs.OpenFiles("PDF importieren", "PDF (*.pdf)|*.pdf");
        if (paths.Length == 0) return;

        // Auto-Save nach Commit wie bei WinCan/IBAK/KINS — Import-Arbeit nicht nur im RAM (Audit H4)
        await RunImportWithOptionalPreviewAsync(
            "PDF",
            paths,
            ImportPdfCore,
            postImportAsync: PostImportPdfAsync,
            saveProjectAfterCommit: true);
    }

    private Task ImportSchachtPdfsFolderAsync()
        => _protocolDistributionController.ExecuteAsync(
            new Services.ImportProtocolDistributionActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                CollectionLock: _shell.CollectionLock,
                SaveProject: () => _shell.SaveCommand.Execute(null)));

    private Result<ImportStats> ImportPdfCore(string[] paths, Project project, ImportRunContext ctx)
    {
        var totalFound = 0;
        var totalCreated = 0;
        var totalUpdated = 0;
        var totalUncertain = 0;
        var totalErrors = 0;
        var messages = new List<string>();

        for (var i = 0; i < paths.Length; i++)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var path = paths[i];
            ctx.Progress?.Report(new Application.Import.ImportProgress(
                "PDF lesen", i + 1, paths.Length,
                $"PDF {i + 1}/{paths.Length}: {Path.GetFileName(path)}", Path.GetFileName(path)));

            var res = _pdfImport.ImportPdf(path, project, _pdfToTextPath, FillMissingOnly, ctx);
            if (!res.Ok || res.Value is null)
            {
                totalErrors++;
                messages.Add($"Error: {Path.GetFileName(path)}: {res.ErrorMessage}");
                continue;
            }

            totalFound += res.Value.Found;
            totalCreated += res.Value.Created;
            totalUpdated += res.Value.Updated;
            totalUncertain += res.Value.Uncertain;
            totalErrors += res.Value.Errors;
            foreach (var msg in res.Value.Messages)
                messages.Add($"{Path.GetFileName(path)}: {msg}");
        }

        return Result<ImportStats>.Success(new ImportStats(totalFound, totalCreated, totalUpdated, totalErrors, totalUncertain, messages));
    }

    private Task PostImportPdfAsync(string[] paths, Project project, ImportRunContext ctx)
    {
        if (!ctx.DryRun)
        {
            StorePdfFiles(paths, project);
            if (paths.Length > 0)
                Services.ImportPostProcessingController.TrackImportSource(
                    project,
                    Path.GetDirectoryName(paths[0]) ?? paths[0],
                    "PDF",
                    DateTime.Now);
        }

        return Task.CompletedTask;
    }

    private async Task ImportXtfAsync()
    {
        var paths = _dialogs.OpenFiles(
            "Daten importieren (XTF/M150/MDB)",
            "Daten (*.xtf;*.m150;*.mdb;*.xml)|*.xtf;*.m150;*.mdb;*.xml|XTF (*.xtf)|*.xtf|M150/XML (*.m150;*.xml)|*.m150;*.xml|MDB (*.mdb)|*.mdb|Alle Dateien|*.*");
        if (paths.Length == 0) return;

        // Auto-Save nach Commit wie bei WinCan/IBAK/KINS (Audit H4)
        await RunImportWithOptionalPreviewAsync(
            "XTF",
            paths,
            ImportXtfCore,
            postImportAsync: PostImportXtfAsync,
            saveProjectAfterCommit: true);
    }

    private Result<ImportStats> ImportXtfCore(string[] paths, Project project, ImportRunContext ctx)
    {
        return _xtfImport.ImportXtfFiles(paths, project, ctx);
    }

    private Task PostImportXtfAsync(string[] paths, Project project, ImportRunContext ctx)
    {
        if (!ctx.DryRun)
        {
            StoreXtfFiles(paths, project);
            if (paths.Length > 0)
                Services.ImportPostProcessingController.TrackImportSource(
                    project,
                    Path.GetDirectoryName(paths[0]) ?? paths[0],
                    "XTF",
                    DateTime.Now);
        }

        return Task.CompletedTask;
    }

    private async Task ImportWinCanAsync()
    {
        var folder = _dialogs.SelectFolder("WinCan-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunImportWithOptionalPreviewAsync(
            "WinCan",
            folder,
            ImportFolderCore(_winCanImport.ImportWinCanExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
    }

    private async Task ImportIbakAsync()
    {
        var folder = _dialogs.SelectFolder("IBAK-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunImportWithOptionalPreviewAsync(
            "IBAK",
            folder,
            ImportFolderCore(_ibakImport.ImportIbakExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
    }

    private async Task ImportKinsAsync()
    {
        var folder = _dialogs.SelectFolder("KINS-Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder)) return;

        await RunImportWithOptionalPreviewAsync(
            "KINS",
            folder,
            ImportFolderCore(_kinsImport.ImportKinsExport),
            postImportAsync: PostImportFolderAsync,
            saveProjectAfterCommit: true);
    }

    private static Func<string, Project, ImportRunContext, Result<ImportStats>> ImportFolderCore(
        Func<string, Project, ImportRunContext?, Result<ImportStats>> svcImport)
    {
        return (folder, project, ctx) => svcImport(folder, project, ctx);
    }

    private async Task PostImportFolderAsync(string folder, Project project, ImportRunContext ctx)
    {
        if (ctx.DryRun) return;

        await Services.ImportPostProcessingController.RunAsync(
            new Services.ImportPostProcessingRequest(
                folder,
                ctx.Log.ImportType,
                project,
                _shell.GetProjectFolder(),
                _pdfImport,
                _pdfToTextPath,
                FillMissingOnly,
                ctx,
                _shell.CollectionLock),
            new Services.ImportPostProcessingActions(
                SetProgressText: value => ImportProgress = value,
                SetProgressPercent: value => ImportProgressPercent = value,
                AppendSummaryText: value => SummaryText += value,
                AppendDetailsText: value => DetailsText += value,
                SetStatus: _shell.SetStatus));
    }

    // ──── Post-Import Helpers ────

    /// <summary>
    /// Macht das aktuelle Projekt portabel: alle Medienpfade relativ auf die Projekt-Kopie,
    /// Fotos aus der Quelle ins Projekt holen. Danach 1:1 auf einen anderen PC kopierbar.
    /// </summary>
    private Task MakeProjectPortableAsync()
        => _projectPortabilityController.ExecuteAsync(
            new Services.ImportProjectPortabilityActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                SaveProject: _shell.TrySaveProject,
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value,
                AppendDetails: value => DetailsText += value));

    /// <summary>
    /// Erzeugt am Ende der Bearbeitung je Haltung das programm-EIGENE Protokoll (mit Fotos, Suffix _E)
    /// in die Verteilung (Haltungen_Verteilt) und verlinkt es relativ als „Eigenes Protokoll" (PDF_Eigen).
    /// Das ORIGINAL-Protokoll (PDF_Path) bleibt unberuehrt. Immer aktuell (Haltungsnummer, DN, Befunde).
    /// </summary>
    private Task ProtokollNeuGenerierenAsync()
        => _protocolRegenerationController.ExecuteAsync(
            new Services.ImportProtocolRegenerationActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                SaveProject: _shell.TrySaveProject,
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value,
                AppendDetails: value => DetailsText += value,
                SetStatus: _shell.SetStatus));

    /// <summary>
    /// Ordnet Fotos aus einem gewaehlten Quellordner den Haltungen/Beobachtungen zu (per Dateiname,
    /// IKAS wie WinCan), kopiert sie ins Projekt und verlinkt relativ. Fuer haltungs-benannte Fotos;
    /// GUID-benannte (nur ueber die DB zuordenbar) bleiben offen.
    /// </summary>
    private Task AssignPhotosFromFolderAsync()
        => _projectPhotoAssignmentController.ExecuteAsync(
            new Services.ImportProjectPhotoAssignmentActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                SaveProject: _shell.TrySaveProject,
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value,
                AppendDetails: value => DetailsText += value));

    /// <summary>
    /// Ein-Knopf-Import: Quellordner der Kanalfernsehdaten waehlen → Format erkennen (WinCan/IKAS/KINS) →
    /// massgebliche Quelle importieren (inkl. Pro-Beobachtung-Fotos) → Rohdaten archivieren →
    /// Filme/PDFs verteilen → Fotos zentral gruppieren → relativ verlinken. Nutzt den getesteten
    /// ProjectImportOrchestrator. Die 5 manuellen Format-Knoepfe bleiben als Spezialfall.
    /// </summary>
    private Task ImportKanalProjektAsync()
        => _oneClickProjectController.ExecuteAsync(
            new Services.ImportOneClickProjectActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                CollectionLock: _shell.CollectionLock,
                SaveProject: _shell.TrySaveProject,
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value,
                AppendDetails: value => DetailsText += value));

    private Task RunVsaAfterImport(Project project, string sourceLabel)
        => _vsaEvaluationController.ExecuteAsync(
            project,
            sourceLabel,
            new Services.ImportVsaEvaluationActions(
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value));

    // ──── Catalog ────

    private void ApplyCatalogStatus(Services.ImportCatalogStatus status)
    {
        CatalogStatus = status.Text;
        IsCatalogOk = status.IsOk;
    }

    private void ReloadCatalog()
    {
        var result = _catalogController.Reload();
        if (!string.IsNullOrWhiteSpace(result.UserError))
            DetailsText = result.UserError;
        ApplyCatalogStatus(result.Status);
    }

    // ──── Import report ────

    private void ExportImportSummary()
        => _summaryExportController.Execute(
            new Services.ImportSummaryExportActions(
                GetProjectPath: () => _settings.LastProjectPath,
                GetProject: () => _shell.Project,
                SetLastResult: value => LastResult = value,
                SetStatus: _shell.SetStatus));

    // ──── File Storage ────

    private void StoreXtfFiles(string[] paths, Project project)
    {
        StoreImportFiles(paths, project, "XTF", "XTF-Dateien");
    }

    private void StorePdfFiles(string[] paths, Project project)
    {
        StoreImportFiles(paths, project, "PDF", "PDF-Dateien");
    }

    private void StoreImportFiles(string[] paths, Project project, string importKind, string displayName)
    {
        var result = _storedImportFiles.Store(
            _settings.LastProjectPath,
            project.Metadata,
            importKind,
            paths);

        if (result.MissingProjectPath)
        {
            LastResult += $"\nHinweis: Projekt bitte speichern, um {displayName} im Projekt abzulegen.";
        }

        if (result.Errors.Count > 0)
        {
            LastResult += $"\nHinweis: {result.Errors.Count} {displayName} konnten nicht im Projekt abgelegt werden.";
        }
    }

    /// <summary>
    /// Nach jedem Import: Primaere_Schaeden aller Records deduplizieren.
    /// Entfernt doppelte Zeilen (gleicher Code + Meter) aus dem fertigen Text.
    /// </summary>
    private static void DeduplicateAllPrimaryDamages(Project project)
    {
        try
        {
            foreach (var rec in project.Data)
            {
                var raw = rec.GetFieldValue("Primaere_Schaeden");
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var clean = XtfPrimaryDamageFormatter.DeduplicateText(raw);
                if (!string.Equals(raw, clean, StringComparison.Ordinal))
                {
                    rec.FieldMeta.TryGetValue("Primaere_Schaeden", out var meta);
                    var source = meta?.Source ?? FieldSource.Manual;
                    rec.SetFieldValue("Primaere_Schaeden", clean, source, userEdited: false);
                }
            }
        }
        catch
        {
            // Dedup-Fehler sollen Import nicht brechen
        }
    }

    partial void OnLastResultChanged(string value)
    {
        SummaryText = value ?? "";
        if (string.IsNullOrWhiteSpace(DetailsText))
            DetailsText = SummaryText;
    }
}
