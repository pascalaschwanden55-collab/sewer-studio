using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ImportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly AuswertungPro.Next.Application.Projects.IProjectRepository _projects;
    private readonly AuswertungPro.Next.Application.Projects.IProjectContentSignature _contentSignature;
    private readonly AuswertungPro.Next.Application.Import.IImportTransactionJournal _transactionJournal;
    private readonly IImportRunReportExporter _importRunReports;
    private readonly Services.ImportManualWorkflowController _manualWorkflowController;
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
    public IAsyncRelayCommand ImportSchachtProCommand { get; }
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
        var dialogs = sp.Dialogs;
        _settings = sp.Settings;
        _projects = sp.Projects;
        _contentSignature = sp.ProjectContentSignature;
        _transactionJournal = sp.ImportTransactionJournal;
        _importRunReports = sp.ImportRunReports;
        var oneClickImporter = sp.CreateOneClickProjectImportService();
        var resolvedCatalogPath = sp.VsaCatalogResolvedPath;
        _manualWorkflowController = new Services.ImportManualWorkflowController(
            dialogs,
            sp.PdfImport,
            sp.XtfImport,
            sp.WinCanImport,
            sp.IbakImport,
            sp.KinsImport,
            sp.SchachtProImport,
            sp.StoredImportFiles,
            sp.ImportFileStaging,
            sp.ImportMediaDistribution,
            sp.Diagnostics.ExplicitPdfToTextPath);
        _projectPortabilityController = new Services.ImportProjectPortabilityController(
            dialogs,
            sp.ProjectPortability);
        _projectPhotoAssignmentController = new Services.ImportProjectPhotoAssignmentController(
            dialogs,
            sp.ProjectPhotoAssignment);
        _protocolDistributionController = new Services.ImportProtocolDistributionController(
            dialogs,
            sp.NameBasedProtocolDistributor,
            sp.Logger);
        _protocolRegenerationController = new Services.ImportProtocolRegenerationController(
            dialogs,
            sp.ProtocolRegeneration,
            sp.CodeCatalog);
        _oneClickProjectController = new Services.ImportOneClickProjectController(
            dialogs,
            () => oneClickImporter,
            sp.OneClickImportReports,
            sp.ImportedFiles,
            sp.ImportFileStaging,
            sp.ImportTransactionJournal);
        _reportNavigationController = new Services.ImportReportNavigationController(
            dialogs,
            () => _settings.LastProjectPath,
            path => Services.SafeShellOpen.TryOpen(path, out _));
        _summaryExportController = new Services.ImportSummaryExportController(
            dialogs,
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
        ImportSchachtProCommand = new AsyncRelayCommand(ImportSchachtProAsync, CanStartImport);
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
        ImportSchachtProCommand.NotifyCanExecuteChanged();
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

    private Task RunManualImportAsync(
        Func<Services.ImportManualWorkflowContext, Task> runAsync)
    {
        _importCts?.Dispose();
        _importCts = new CancellationTokenSource();
        return runAsync(CreateManualWorkflowContext(_importCts.Token));
    }

    private Services.ImportManualWorkflowContext CreateManualWorkflowContext(
        CancellationToken cancellationToken)
        => new(
            ShowPreviewFirst: ShowPreviewFirst,
            FillMissingOnly: FillMissingOnly,
            ProjectPath: _shell.HasPersistedProject ? _settings.LastProjectPath : null,
            ProjectFolder: _shell.HasPersistedProject ? _shell.GetProjectFolder() : null,
            WorkflowActions: CreateImportRunWorkflowActions(),
            CancellationToken: cancellationToken);

    private Services.ImportRunWorkflowActions CreateImportRunWorkflowActions()
        => new(
            GetProject: () => _shell.Project,
            GetProjectPath: () => _shell.HasPersistedProject
                ? _settings.LastProjectPath
                : null,
            DeepCopyProject: _projects.DeepCopy,
            ReplaceProject: _shell.ReplaceProject,
            CreateRestorePoint: _shell.TryCreateImportRestorePoint,
            GetReportDir: () => _shell.HasPersistedProject
                ? _reportNavigationController.GetReportDirectory()
                : null,
            ExportReport: _importRunReports.Export,
            ShowPreview: ShowPreviewWindow,
            ValidatePlausibility: Application.Import.ImportPlausibilityValidator.Validate,
            DeduplicateAllPrimaryDamages: DeduplicateAllPrimaryDamages,
            RunAfterImportAsync: RunVsaAfterImport,
            SaveProject: _shell.TrySaveProject,
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
            CollectionLock: _shell.CollectionLock,
            ComputeSignature: _contentSignature.Compute,
            Journal: _transactionJournal);

    private bool ShowPreviewWindow(ImportPreviewResult preview, string label)
    {
        var win = new Views.Windows.ImportPreviewWindow(preview, label)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        return win.ShowDialog() == true;
    }

    // ──── Import Methods ────

    private Task ImportPdfAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportPdfAsync);

    private Task ImportSchachtPdfsFolderAsync()
        => _protocolDistributionController.ExecuteAsync(
            new Services.ImportProtocolDistributionActions(
                GetProjectFolder: _shell.GetProjectFolder,
                GetProject: () => _shell.Project,
                CollectionLock: _shell.CollectionLock,
                SaveProject: _shell.TrySaveProject));

    private Task ImportXtfAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportXtfAsync);

    private Task ImportWinCanAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportWinCanAsync);

    private Task ImportIbakAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportIbakAsync);

    private Task ImportKinsAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportKinsAsync);

    private Task ImportSchachtProAsync()
        => RunManualImportAsync(_manualWorkflowController.ImportSchachtProAsync);

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
                DeepCopyProject: _projects.DeepCopy,
                ReplaceProject: _shell.ReplaceProject,
                CollectionLock: _shell.CollectionLock,
                SaveProject: _shell.TrySaveProject,
                SetProgress: value => ImportProgress = value,
                AppendSummary: value => SummaryText += value,
                AppendDetails: value => DetailsText += value,
                ComputeSignature: _contentSignature.Compute,
                GetProjectPath: () => _settings.LastProjectPath));

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
