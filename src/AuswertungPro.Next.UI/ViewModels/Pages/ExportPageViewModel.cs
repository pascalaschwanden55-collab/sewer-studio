using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Application.UseCases.Import;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel : ObservableObject, IConfirmLeave, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly AuswertungPro.Next.Application.Xtf.IXtfRevisionExportService _xtfRevisionExport;
    private readonly IExcelExportService _excelExport;
    private readonly IToastService _toasts;
    private readonly IDerivedCostFieldSynchronizer _costFieldSync;
    private readonly IProjectCostStoreRepository _projectCosts;
    private readonly IStoredImportFileService _storedImportFiles;
    private readonly IDistributionPatternResolver _patternResolver;
    private readonly IDistributionDirectoryTreeResolver _directoryTreeResolver;
    private readonly IKatasterXtfPathResolver _katasterXtfPaths;
    private readonly IHaltungCadastreIndexProvider _haltungCadastreIndexes;
    private readonly IShaftDistributionService _shaftDistribution;
    private readonly Application.Export.IDistributionReconciliationService _distributionReconciliation;
    private readonly IImportFileStagingService? _importFileStaging;
    private readonly IImportTransactionJournal? _importTransactionJournal;
    private readonly ExportPageShellOperationGuard _shellOperationGuard = new();
    private readonly Func<bool> _saveProjectForActiveDistribution;
    private bool _isShaftDistributionActive;
    private CancellationTokenSource? _excelExportCancellation;
    private bool _disposed;

    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _distributionProgress = "";
    [ObservableProperty] private bool _isDistributionInProgress;
    [ObservableProperty] private bool _isDistributionIndeterminate;
    [ObservableProperty] private double _distributionPercent;
    [ObservableProperty] private bool _isPageBusy;
    [ObservableProperty] private string? _excelExportRoot;

    /// <summary>Lade-Overlay fuer Langlaeufer (xlsx-Export).</summary>
    public Services.BusyState Busy { get; } = new();

    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand ExportSchaechteCommand { get; }
    public IRelayCommand CancelExcelExportCommand { get; }
    public IAsyncRelayCommand DistributeHoldingsNormalCommand { get; }
    public IAsyncRelayCommand DistributeHoldingsSanierungCommand { get; }
    public IAsyncRelayCommand DistributeShaftsNormalCommand { get; }
    public IAsyncRelayCommand DistributeShaftsSanierungCommand { get; }
    public IAsyncRelayCommand DistributeDichtheitCommand { get; }
    /// <summary>Raeumt aus den Verteilordnern, was im Projekt kein Gegenstueck hat.</summary>
    public IAsyncRelayCommand AbgleichenCommand { get; }
    public IRelayCommand BrowseExcelExportRootCommand { get; }

    /// <summary>Erzeugt aus dem aktuellen Projektstand revidierte XTF-Dateien.</summary>
    public IRelayCommand ErzeugeXtfRevisionCommand { get; }

    /// <summary>Verzeichnisbaum-Karten fuer Haltungen, Schaechte und Dichtheitspruefungen.</summary>
    public IReadOnlyList<DistributionTargetConfigViewModel> DistributionTargets { get; }

    public ExportPageViewModel(ShellViewModel shell, ServiceProvider sp)
        : this(
            shell,
            settings: sp.Settings,
            dialogs: sp.Dialogs,
            excelExport: sp.ExcelExport,
            toasts: sp.Toasts,
            costFieldSync: sp.CostFieldSync,
            projectCosts: sp.CostStores.CreateProjectCostStore(),
            storedImportFiles: sp.StoredImportFiles,
            patternResolver: sp.DistributionPatterns,
            directoryTreeResolver: sp.DistributionDirectoryTree,
            katasterXtfPaths: sp.KatasterXtfPaths,
            haltungCadastreIndexes: sp.HaltungCadastreIndexes,
            shaftDistribution: sp.ShaftDistribution,
            importFileStaging: sp.ImportFileStaging,
            importTransactionJournal: sp.ImportTransactionJournal)
    {
    }

    [Obsolete("Uebergangskonstruktor. Neue Aufrufer sollen den Kosten-Speicher injizieren.")]
    public ExportPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IExcelExportService excelExport,
        IToastService toasts,
        IDerivedCostFieldSynchronizer costFieldSync,
        IDistributionPatternResolver? patternResolver = null,
        IDistributionDirectoryTreeResolver? directoryTreeResolver = null,
        IKatasterXtfPathResolver? katasterXtfPaths = null,
        IHaltungCadastreIndexProvider? haltungCadastreIndexes = null)
        : this(
            shell,
            settings,
            dialogs,
            excelExport,
            toasts,
            costFieldSync,
            CostStoreCompatibility.Factory.CreateProjectCostStore(),
            patternResolver,
            directoryTreeResolver,
            katasterXtfPaths,
            haltungCadastreIndexes)
    {
    }

    public ExportPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IExcelExportService excelExport,
        IToastService toasts,
        IDerivedCostFieldSynchronizer costFieldSync,
        IProjectCostStoreRepository projectCosts,
        IDistributionPatternResolver? patternResolver = null,
        IDistributionDirectoryTreeResolver? directoryTreeResolver = null,
        IKatasterXtfPathResolver? katasterXtfPaths = null,
        IHaltungCadastreIndexProvider? haltungCadastreIndexes = null)
        : this(
            shell,
            settings,
            dialogs,
            excelExport,
            toasts,
            costFieldSync,
            projectCosts,
            Services.StoredImportFileRegistry.CompatibilityService,
            patternResolver,
            directoryTreeResolver,
            katasterXtfPaths,
            haltungCadastreIndexes)
    {
    }

    public ExportPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IExcelExportService excelExport,
        IToastService toasts,
        IDerivedCostFieldSynchronizer costFieldSync,
        IProjectCostStoreRepository projectCosts,
        IStoredImportFileService storedImportFiles,
        IDistributionPatternResolver? patternResolver,
        IDistributionDirectoryTreeResolver? directoryTreeResolver,
        IKatasterXtfPathResolver? katasterXtfPaths,
        IHaltungCadastreIndexProvider? haltungCadastreIndexes,
        AuswertungPro.Next.Application.Xtf.IXtfRevisionExportService? xtfRevisionExport = null,
        IShaftDistributionService? shaftDistribution = null,
        Application.Export.IDistributionReconciliationService? distributionReconciliation = null,
        IImportFileStagingService? importFileStaging = null,
        IImportTransactionJournal? importTransactionJournal = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _excelExport = excelExport ?? throw new ArgumentNullException(nameof(excelExport));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _costFieldSync = costFieldSync ?? throw new ArgumentNullException(nameof(costFieldSync));
        _projectCosts = projectCosts ?? throw new ArgumentNullException(nameof(projectCosts));
        _storedImportFiles = storedImportFiles ?? throw new ArgumentNullException(nameof(storedImportFiles));
        ExportCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(ExportAsync), CanRunProjectExportCommands);
        ExportSchaechteCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(ExportSchaechteAsync), CanRunProjectExportCommands);
        CancelExcelExportCommand = new RelayCommand(
            CancelExcelExport,
            () => _excelExportCancellation is { IsCancellationRequested: false });
        DistributeHoldingsNormalCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(() => DistributeHoldingsAsync(DistributionVariant.Normal)), CanRunDistributeCommands);
        DistributeHoldingsSanierungCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(() => DistributeHoldingsAsync(DistributionVariant.Sanierung)), CanRunDistributeCommands);
        DistributeShaftsNormalCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(() => DistributeShaftsAsync(DistributionVariant.Normal), allowsInternalProjectSave: true), CanRunDistributeCommands);
        DistributeShaftsSanierungCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(() => DistributeShaftsAsync(DistributionVariant.Sanierung), allowsInternalProjectSave: true), CanRunDistributeCommands);
        DistributeDichtheitCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(DistributeDichtheitAsync), CanRunDistributeCommands);
        AbgleichenCommand = new AsyncRelayCommand(() => RunWithProjectOperationAsync(AbgleichenAsync), CanRunDistributeCommands);
        BrowseExcelExportRootCommand = new RelayCommand(BrowseExcelExportRoot);
        _xtfRevisionExport = xtfRevisionExport
            ?? new AuswertungPro.Next.Infrastructure.Import.Xtf.XtfRevisionExportService();
        ErzeugeXtfRevisionCommand = new RelayCommand(RunXtfRevisionWithProjectOperation, CanRunProjectExportCommands);
        _patternResolver = patternResolver ?? new DistributionPatternResolver();
        _directoryTreeResolver = directoryTreeResolver ?? new DistributionDirectoryTreeResolver(_patternResolver);
        _katasterXtfPaths = katasterXtfPaths ?? KatasterXtfPathResolver.CompatibilityService;
        _haltungCadastreIndexes = haltungCadastreIndexes ?? HaltungCadastreIndex.CurrentProvider;
        _shaftDistribution = shaftDistribution ?? new ShaftDistributionService();
        _distributionReconciliation = distributionReconciliation
            ?? new Infrastructure.Export.DistributionReconciliationService();
        _importFileStaging = importFileStaging;
        _importTransactionJournal = importTransactionJournal;
        _settings.MigrateLegacyExcelExportRoot();
        _excelExportRoot = _settings.ExcelExportRoot;
        DistributionTargets = BuildDistributionTargets(_patternResolver);
        _shell.RegisterShellOperationGuard(_shellOperationGuard);
        try
        {
            _saveProjectForActiveDistribution =
                _shell.CreateActiveProjectOperationSaveDelegate(_shellOperationGuard);
        }
        catch
        {
            _shell.UnregisterShellOperationGuard(_shellOperationGuard);
            throw;
        }
    }

    /// <summary>
    /// Baut den Excel-Zielpfad aus konfigurierter Ziel-Wurzel + Datei-Muster; <c>null</c>, wenn keine
    /// Wurzel gesetzt ist (dann faellt der Aufrufer auf den Speichern-Dialog zurueck).
    /// Rein und ohne Seiteneffekte -> testbar.
    /// </summary>
    internal static string? BuildConfiguredExcelPath(
        string? sharedRoot,
        DistributionTargetConfig cfg,
        IDistributionPatternResolver resolver,
        DateTime datum,
        string? fallbackFilePattern = null,
        bool forceFallback = false)
        => ExportExcelPathPolicy.BuildConfiguredPath(
            sharedRoot,
            cfg,
            resolver,
            datum,
            fallbackFilePattern,
            forceFallback);

    /// <summary>
    /// Baut den Excel-Zielpfad nur aus dem gemeinsamen Zielordner und dem festen Exportnamen.
    /// Die Oberflaeche bietet bewusst keine Dateinamens- oder Verzeichnisbaum-Muster fuer Excel an.
    /// </summary>
    internal static string? BuildFixedExcelPath(string? sharedRoot, string fileNameWithoutExtension)
        => ExportExcelPathPolicy.BuildFixedPath(sharedRoot, fileNameWithoutExtension);

    /// <summary>Rueckwaertskompatibler Test-/Hilfsweg fuer alte Aufrufer.</summary>
    internal static string? BuildConfiguredExcelPath(
        DistributionTargetConfig cfg,
        IDistributionPatternResolver resolver,
        DateTime datum)
        => BuildConfiguredExcelPath(cfg.Root, cfg, resolver, datum);

    /// <summary>
    /// Verhindert, dass beide Excel-Exporte im gemeinsamen Ordner denselben Zielpfad
    /// erhalten. Bei einer Kollision gilt fuer beide wieder ihr eindeutiger Standardname.
    /// </summary>
    internal static string? BuildCollisionSafeExcelPath(
        string? sharedRoot,
        DistributionTargetConfig cfg,
        string fallbackFilePattern,
        DistributionTargetConfig otherCfg,
        string otherFallbackFilePattern,
        IDistributionPatternResolver resolver,
        DateTime datum)
        => ExportExcelPathPolicy.BuildCollisionSafePath(
            sharedRoot,
            cfg,
            fallbackFilePattern,
            otherCfg,
            otherFallbackFilePattern,
            resolver,
            datum);

    /// <summary>
    /// Baut die drei Verzeichnisbaum-Karten fuer Haltungen, Schaechte und Dichtheit.
    /// Die Live-Vorschau nutzt die Projektgemeinde sowie Beispiel-Haltung/-Schacht und heutiges Datum;
    /// Aenderungen werden ueber den debounced <see cref="AppSettings.Save"/> persistiert.
    /// </summary>
    private IReadOnlyList<DistributionTargetConfigViewModel> BuildDistributionTargets(IDistributionPatternResolver resolver)
    {
        var heute = DateTime.Today;
        var gemeinde = _shell.Project.Metadata.TryGetValue("Gemeinde", out var projektGemeinde)
            && !string.IsNullOrWhiteSpace(projektGemeinde)
                ? projektGemeinde.Trim()
                : "Gemeinde";
        // Die drei Verteilungen erhalten typgerechte Beispiele fuer ihren kompletten,
        // sicheren Verzeichnisbaum.
        var haltungSample = new DistributionPatternContext(heute, gemeinde, "06.24341-35625");
        var schachtSample = new DistributionPatternContext(heute, gemeinde, Schachtnummer: "80454");
        var dichtheitSample = new DistributionPatternContext(heute, gemeinde, "06.24341-35625");

        void OnCfgChanged()
        {
            _settings.Save();
        }
        string? BrowseRoot() => _dialogs.SelectFolder("Ziel-Wurzel waehlen");

        const string haltungHinweis = "Der letzte Haltungsordner und die Dateinamen bleiben fuer die sichere Video-Zuordnung fest.";
        const string schachtHinweis = "Der letzte Schachtordner und der Dateiname bleiben fest.";
        const string dichtheitHinweis = "DP wird sicher je Haltung abgelegt; Objektordner und Dateiname bleiben fest.";
        return new[]
        {
            new DistributionTargetConfigViewModel(
                "Haltungen verteilen", "PDF-Protokoll + Video je Haltung",
                _settings.HaltungDistribution, resolver, haltungSample, ".pdf",
                showFilePattern: false, haltungHinweis, OnCfgChanged, BrowseRoot,
                fixedPattern: "{Datum}_{Haltung}",
                fixedObjectFolderPattern: "{Haltung}",
                directoryTreeResolver: _directoryTreeResolver,
                supportsSanierung: true),
            new DistributionTargetConfigViewModel(
                "Schächte verteilen", "Schachtprotokoll je Schacht",
                _settings.SchachtDistribution, resolver, schachtSample, ".pdf",
                showFilePattern: false, schachtHinweis, OnCfgChanged, BrowseRoot,
                fixedPattern: "{Datum}_{Schachtnummer}",
                fixedObjectFolderPattern: "{Schachtnummer}",
                directoryTreeResolver: _directoryTreeResolver,
                supportsSanierung: true),
            new DistributionTargetConfigViewModel(
                "Dichtheitsprüfung verteilen", "DP-Protokoll je Haltung",
                _settings.DichtheitDistribution, resolver, dichtheitSample, ".pdf",
                showFilePattern: false, dichtheitHinweis, OnCfgChanged, BrowseRoot,
                fixedPattern: "{Datum}_{Haltung}_DP",
                fixedObjectFolderPattern: "{Haltung}",
                directoryTreeResolver: _directoryTreeResolver),
        };
    }

    /// <summary>
    /// Erzeugt die revidierten XTF-Dateien. Zuerst laeuft eine reine Pruefung; erst nach
    /// ausdruecklicher Bestaetigung wird geschrieben. Kundenoriginale werden nur gelesen,
    /// die Revisionen landen in einem neuen Ordner mit Zeitstempel.
    /// </summary>
    private void ErzeugeXtfRevision()
    {
        var ziel = ExcelExportRoot;
        if (string.IsNullOrWhiteSpace(ziel))
            ziel = _dialogs.SelectFolder("Zielordner fuer die revidierte XTF waehlen");
        if (string.IsNullOrWhiteSpace(ziel))
            return;

        var projektPfad = _settings.LastProjectPath ?? "";

        var pruefung = _xtfRevisionExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfRevisionExportRequest(
                _shell.Project, projektPfad, ziel!, NurPruefen: true));

        if (!pruefung.Ok && pruefung.Dateien.Count == 0 && string.IsNullOrWhiteSpace(pruefung.Bericht))
        {
            _dialogs.Error(pruefung.Fehler ?? "Die Pruefung ist fehlgeschlagen.", "Revidierte XTF");
            return;
        }

        var weiter = _dialogs.ConfirmCancel(
            $"{pruefung.Bericht}\n\nDie Revision jetzt schreiben?\n" +
            "Die Originaldateien werden dabei nur gelesen.",
            "Revidierte XTF");
        if (weiter != DialogConfirm.Yes)
        {
            LastResult = "Revision abgebrochen.";
            return;
        }

        var ergebnis = _xtfRevisionExport.Erzeuge(
            new AuswertungPro.Next.Application.Xtf.XtfRevisionExportRequest(
                _shell.Project, projektPfad, ziel!));

        if (!ergebnis.Ok)
        {
            _dialogs.Error($"{ergebnis.Bericht}", "Revidierte XTF");
            LastResult = "Revision nicht vollstaendig erzeugt.";
            return;
        }

        LastResult = ergebnis.Dateien.Count == 0
            ? "Keine Aenderung — keine Revision noetig."
            : $"Revidierte XTF erzeugt: {ergebnis.Dateien.Count} Datei(en).";
        _toasts.Success(LastResult);
    }

    private void BrowseExcelExportRoot()
    {
        var selected = _dialogs.SelectFolder("Gemeinsamen Excel-Zielordner waehlen");
        if (!string.IsNullOrWhiteSpace(selected))
            ExcelExportRoot = selected;
    }

    partial void OnExcelExportRootChanged(string? value)
    {
        var normalized = _settings.SetExcelExportRoot(value);
        if (!string.Equals(_excelExportRoot, normalized, StringComparison.Ordinal))
        {
            _excelExportRoot = normalized;
            OnPropertyChanged(nameof(ExcelExportRoot));
        }

        _settings.Save();
    }

    /// <summary>Excel-Export braucht geladenes Projekt.</summary>
    private bool CanRunProjectExportCommands()
        => !_disposed && !IsPageBusy && _shell.Project is not null;

    /// <summary>Verteilung funktioniert auch ohne Projekt (Ordner-/PDF-basiert).</summary>
    private bool CanRunDistributeCommands()
        => !_disposed && !IsPageBusy;

    partial void OnIsPageBusyChanged(bool value)
    {
        _shellOperationGuard.Update(value, _isShaftDistributionActive);
        NotifyAllCommandsCanExecuteChanged();
    }

    /// <summary>
    /// Alle Commands ueber CanExecute-Aenderung informieren.
    /// Wird bei IsPageBusy-Aenderung und nach Projekt-Laden aufgerufen.
    /// </summary>
    public void NotifyAllCommandsCanExecuteChanged()
    {
        ExportCommand.NotifyCanExecuteChanged();
        ExportSchaechteCommand.NotifyCanExecuteChanged();
        DistributeHoldingsNormalCommand.NotifyCanExecuteChanged();
        DistributeHoldingsSanierungCommand.NotifyCanExecuteChanged();
        DistributeShaftsNormalCommand.NotifyCanExecuteChanged();
        DistributeShaftsSanierungCommand.NotifyCanExecuteChanged();
        DistributeDichtheitCommand.NotifyCanExecuteChanged();
        ErzeugeXtfRevisionCommand.NotifyCanExecuteChanged();
    }

    public bool ConfirmLeave()
    {
        if (!IsPageBusy)
            return true;

        _shell.SetStatus(
            "Seiten- oder Projektwechsel ist waehrend eines Exports oder einer Verteilung gesperrt. " +
            "Bitte den laufenden Vorgang zuerst abschliessen.");
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _excelExportCancellation?.Cancel();
        _shell.UnregisterShellOperationGuard(_shellOperationGuard);
        GC.SuppressFinalize(this);
    }

    private void SetShaftDistributionActive(bool value)
    {
        if (_isShaftDistributionActive == value)
            return;

        _isShaftDistributionActive = value;
        _shellOperationGuard.Update(IsPageBusy, value);
    }

    // ─── Distribution: Haltungen ───────────────────────────────────────────

    private async Task DistributeHoldingsAsync(DistributionVariant variant)
    {
        var sourceMode = _dialogs.ConfirmCancel(
            "Quelle:\nJa = PDF-Import verteilen\nNein = TXT-Import verteilen (z.B. kiDVDaten.txt)",
            "Haltungen verteilen");
        if (sourceMode == DialogConfirm.Cancel)
            return;

        var useTxtImport = sourceMode == DialogConfirm.No;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        string? txtFolder = null;
        string[] selectedTxtFiles = Array.Empty<string>();

        if (!useTxtImport)
        {
            var mode = _dialogs.ConfirmCancel(
                "PDF-Auswahl:\nJa = einzelne PDF-Protokolle auswaehlen\nNein = ganzen PDF-Ordner verwenden",
                "Haltungen verteilen (PDF)");
            if (mode == DialogConfirm.Cancel)
                return;

            if (mode == DialogConfirm.Yes)
            {
                selectedPdfFiles = _dialogs.OpenFiles("PDF-Protokolle auswaehlen", "PDF (*.pdf)|*.pdf");
                if (selectedPdfFiles.Length == 0)
                    return;
            }
            else
            {
                pdfFolder = _dialogs.SelectFolder("PDF-Ordner mit Protokollen waehlen");
                if (string.IsNullOrWhiteSpace(pdfFolder))
                    return;
            }
        }
        else
        {
            var mode = _dialogs.ConfirmCancel(
                "TXT-Auswahl:\nJa = einzelne TXT-Dateien auswaehlen\nNein = ganzen TXT-Ordner verwenden",
                "Haltungen verteilen (TXT)");
            if (mode == DialogConfirm.Cancel)
                return;

            if (mode == DialogConfirm.Yes)
            {
                selectedTxtFiles = _dialogs.OpenFiles("TXT-Dateien auswaehlen", "TXT (*.txt)|*.txt");
                if (selectedTxtFiles.Length == 0)
                    return;
            }
            else
            {
                txtFolder = _dialogs.SelectFolder("TXT-Ordner waehlen (z.B. mit kiDVDaten.txt)");
                if (string.IsNullOrWhiteSpace(txtFolder))
                    return;
            }
        }

        var videoFolder = _dialogs.SelectFolder("Video-Ordner mit Rohvideos waehlen");
        if (string.IsNullOrWhiteSpace(videoFolder)) return;

        var destFolder = ResolveConfiguredDistributionRoot(_settings.HaltungDistribution)
            ?? ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.HaltungenVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;
        var directoryConfig = SnapshotDistributionTree(_settings.HaltungDistribution);
        var projectContext = new ProjectOperationContext(
            _shell.Project,
            _settings.LastProjectPath);

        try
        {
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Verteilung gestartet...";
            _shell.SetStatus(DistributionProgress);

            var progress = new Progress<HoldingFolderDistributor.DistributionProgress>(p =>
            {
                IsDistributionIndeterminate = p.Total <= 0;
                DistributionPercent = p.Total > 0 ? (p.Processed * 100.0 / p.Total) : 0;
                var name = string.IsNullOrWhiteSpace(p.CurrentFile) ? "" : $" ({Path.GetFileName(p.CurrentFile)})";
                DistributionProgress = $"Verteilung: {p.Processed}/{p.Total}{name}";
                _shell.SetStatus(DistributionProgress);
            });

            IReadOnlyList<HoldingFolderDistributor.DistributionResult> results;
            if (!useTxtImport && selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeFiles(
                    pdfFiles: selectedPdfFiles,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: projectContext.Project,
                    progress: progress,
                    directoryConfig: directoryConfig,
                    variant: variant));
            }
            else if (!useTxtImport)
            {
                results = await Task.Run(() => HoldingFolderDistributor.Distribute(
                    pdfSourceFolder: pdfFolder!,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: projectContext.Project,
                    progress: progress,
                    directoryConfig: directoryConfig,
                    variant: variant));
            }
            else if (selectedTxtFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeTxtFiles(
                    txtFiles: selectedTxtFiles,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: projectContext.Project,
                    progress: progress,
                    directoryConfig: directoryConfig));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeTxt(
                    txtSourceFolder: txtFolder!,
                    videoSourceFolder: videoFolder,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    recursiveVideoSearch: true,
                    unmatchedFolderName: "__UNMATCHED",
                    project: projectContext.Project,
                    progress: progress,
                    directoryConfig: directoryConfig));
            }

            if (!ProjectIsStillCurrent(
                    projectContext,
                    "Haltungs-Verteilung",
                    filesMayRemain: results.Any(static result => result.Success)))
            {
                return;
            }

            // Aggregation und Formatierung an DistributionSummaryBuilder delegiert
            LastResult = DistributionSummaryBuilder.BuildHoldingDistributionSummary(results, useTxtImport);
            _shell.SetStatus(useTxtImport ? "Haltungsdaten (TXT) verteilt" : "Haltungsdaten verteilt");

            if (!useTxtImport && selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles, projectContext);
            if (useTxtImport && selectedTxtFiles.Length > 0)
                StoreTxtFiles(selectedTxtFiles, projectContext);

            _settings.LastVideoSourceFolder = videoFolder;
            _settings.LastDistributionTargetFolder = destFolder;
            _settings.LastVideoFolder = videoFolder;
            _settings.Save();
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
        }
    }

    // ─── Distribution: Dichtheitspruefung ──────────────────────────────────

    private async Task DistributeDichtheitAsync()
    {
        var mode = _dialogs.ConfirmCancel(
            "PDF-Auswahl:\nJa = einzelne DP-PDFs auswaehlen\nNein = ganzen PDF-Ordner verwenden",
            "Dichtheitsprüfung verteilen");
        if (mode == DialogConfirm.Cancel)
            return;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        if (mode == DialogConfirm.Yes)
        {
            selectedPdfFiles = _dialogs.OpenFiles("Dichtheitsprüfungs-PDFs auswaehlen", "PDF (*.pdf)|*.pdf");
            if (selectedPdfFiles.Length == 0)
                return;
        }
        else
        {
            pdfFolder = _dialogs.SelectFolder("PDF-Ordner mit Dichtheitsprüfungsprotokollen waehlen");
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return;
        }

        var destFolder = ResolveConfiguredDistributionRoot(_settings.DichtheitDistribution)
            ?? ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.HaltungenVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;
        var directoryConfig = SnapshotDistributionTree(_settings.DichtheitDistribution);
        var projectContext = new ProjectOperationContext(
            _shell.Project,
            _settings.LastProjectPath);

        try
        {
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Dichtheitsprüfung-Verteilung gestartet...";
            _shell.SetStatus(DistributionProgress);

            var progress = new Progress<HoldingFolderDistributor.DistributionProgress>(p =>
            {
                IsDistributionIndeterminate = p.Total <= 0;
                DistributionPercent = p.Total > 0 ? (p.Processed * 100.0 / p.Total) : 0;
                var name = string.IsNullOrWhiteSpace(p.CurrentFile) ? "" : $" ({Path.GetFileName(p.CurrentFile)})";
                DistributionProgress = $"Verteilung: {p.Processed}/{p.Total}{name}";
                _shell.SetStatus(DistributionProgress);
            });

            // Amtlichen Kataster laden (einmaliger Tabellen-Bau, danach gecached im SewerStudio-Ordner).
            // Fehlt die Datei, bleibt cadastre null -> Verteilung wie bisher.
            IHaltungCadastreResolver? cadastre = null;
            try
            {
                var katasterPfad = _katasterXtfPaths.Resolve(
                    _settings.AbwasserkatasterXtfPath,
                    _settings.KantonUriXtfDirectory);
                if (!string.IsNullOrWhiteSpace(katasterPfad))
                    cadastre = await Task.Run(() => _haltungCadastreIndexes.EnsureAndLoad(katasterPfad));
            }
            catch
            {
                // Kataster optional: ohne ihn laeuft die Verteilung wie bisher.
            }

            if (!ProjectIsStillCurrent(
                    projectContext,
                    "Dichtheitspruefungs-Verteilung",
                    filesMayRemain: false))
            {
                return;
            }

            IReadOnlyList<HoldingFolderDistributor.DistributionResult> results;
            if (selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheitFiles(
                    pdfFiles: selectedPdfFiles,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: projectContext.Project,
                    progress: progress,
                    cadastre: cadastre,
                    directoryConfig: directoryConfig));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheit(
                    pdfSourceFolder: pdfFolder!,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: projectContext.Project,
                    progress: progress,
                    cadastre: cadastre,
                    directoryConfig: directoryConfig));
            }

            if (!ProjectIsStillCurrent(
                    projectContext,
                    "Dichtheitspruefungs-Verteilung",
                    filesMayRemain: results.Any(static result => result.Success)))
            {
                return;
            }

            // Aggregation und Formatierung an DistributionSummaryBuilder delegiert
            LastResult = DistributionSummaryBuilder.BuildDichtheitDistributionSummary(results);
            _shell.SetStatus("Dichtheitsprüfungsprotokolle verteilt");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles, projectContext);
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private string? ResolveDistributionTargetFolder()
    {
        // Standardziel ist der aktive Projektordner, damit manuelle Verteilungen
        // im Projekt landen. Ohne gespeichertes Projekt bleibt der Ordnerdialog als Rueckfall.
        return Services.DistributionTargetFolderPolicy.Resolve(
            _shell.GetProjectFolder(),
            () => _dialogs.SelectFolder("Zielordner (Gemeinde) waehlen"));
    }

    // Zielordner + strukturierter Unterordner (Haltungen_Verteilt\ / Schächte_Verteilt\), damit manuelle
    // Verteilungen NICHT in den Projekt-Root, sondern in die vorgesehene Struktur landen.
    private string? ResolveDistributionSubfolder(string subfolder)
    {
        var baseFolder = ResolveDistributionTargetFolder();
        return string.IsNullOrWhiteSpace(baseFolder) ? null : Path.Combine(baseFolder, subfolder);
    }

    /// <summary>
    /// Friert die beiden Baumebenen fuer den gestarteten Lauf ein. Aenderungen in der
    /// Oberflaeche koennen so einen bereits laufenden Kopiervorgang nicht beeinflussen.
    /// </summary>
    internal static DistributionTargetConfig? SnapshotDistributionTree(DistributionTargetConfig cfg)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        if (string.IsNullOrWhiteSpace(cfg.OrdnerPattern)
            && string.IsNullOrWhiteSpace(cfg.UnterordnerPattern))
        {
            return null;
        }

        return new DistributionTargetConfig
        {
            OrdnerPattern = cfg.OrdnerPattern ?? string.Empty,
            UnterordnerPattern = cfg.UnterordnerPattern ?? string.Empty,
        };
    }

    /// <summary>
    /// Konfigurierte Ziel-Wurzel als Verteil-Ziel; null, wenn keine gesetzt ist
    /// (dann greift der bisherige Projektordner-/Dialog-Pfad wie zuvor).
    /// </summary>
    private static string? ResolveConfiguredDistributionRoot(DistributionTargetConfig cfg)
        => string.IsNullOrWhiteSpace(cfg.Root) ? null : cfg.Root;

    /// <summary>
    /// Excel-Zielpfad aus dem gemeinsamen Zielordner und dem festen Dateinamen; legt den Zielordner an.
    /// Null -> keine Wurzel gesetzt -> Aufrufer nutzt den Speichern-Dialog.
    /// </summary>
    private string? ResolveConfiguredExcelPath(string fixedFileName)
    {
        var ziel = BuildFixedExcelPath(_settings.ExcelExportRoot, fixedFileName);
        if (ziel is null)
            return null;

        var ordner = Path.GetDirectoryName(ziel);
        if (!string.IsNullOrWhiteSpace(ordner))
            Directory.CreateDirectory(ordner);
        return ziel;
    }

    private void StorePdfFiles(string[] paths, ProjectOperationContext projectContext)
        => StoreImportFiles(paths, "PDF", "PDF-Dateien", projectContext);

    private void StoreTxtFiles(string[] paths, ProjectOperationContext projectContext)
        => StoreImportFiles(paths, "TXT", "TXT-Dateien", projectContext);

    private void StoreImportFiles(
        IReadOnlyCollection<string> paths,
        string importKind,
        string displayName,
        ProjectOperationContext projectContext)
    {
        var result = _storedImportFiles.Store(
            projectContext.ProjectPath,
            projectContext.Project.Metadata,
            importKind,
            paths);

        if (result.MissingProjectPath)
        {
            LastResult += $"{Environment.NewLine}Hinweis: Projekt bitte speichern, um {displayName} im Projekt abzulegen.";
        }

        if (result.Errors.Count > 0)
        {
            LastResult += $"{Environment.NewLine}Hinweis: {result.Errors.Count} {displayName} konnten nicht im Projekt abgelegt werden.";
        }
    }

    private bool ProjectIsStillCurrent(
        ProjectOperationContext projectContext,
        string operation,
        bool filesMayRemain,
        bool projectDataChanged = false)
    {
        if (ActiveProjectGuard.IsCurrent(
                projectContext,
                _shell.Project,
                _settings.LastProjectPath))
        {
            return true;
        }

        LastResult = projectDataChanged
            ? $"{operation}: Das aktive Projekt wurde gewechselt. " +
              "Dateien und PDF-Pfade wurden im gestarteten Projekt uebernommen, " +
              "aber nicht gespeichert."
            : filesMayRemain
            ? $"{operation} beendet, aber nicht in Projektdaten uebernommen: " +
              "Das aktive Projekt wurde gewechselt. Bereits kopierte Dateien bleiben im Zielordner."
            : $"{operation} abgebrochen: Das aktive Projekt wurde gewechselt.";
        _shell.SetStatus(LastResult);
        return false;
    }

}
