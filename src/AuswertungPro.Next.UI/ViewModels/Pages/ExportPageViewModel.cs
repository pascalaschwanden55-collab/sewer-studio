using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class ExportPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly IExcelExportService _excelExport;
    private readonly IToastService _toasts;
    private readonly IDerivedCostFieldSynchronizer _costFieldSync;
    private readonly IDistributionPatternResolver _patternResolver;
    private readonly IDistributionDirectoryTreeResolver _directoryTreeResolver;
    private readonly IKatasterXtfPathResolver _katasterXtfPaths;
    private readonly IHaltungCadastreIndexProvider _haltungCadastreIndexes;

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
    public IAsyncRelayCommand DistributeHoldingsNormalCommand { get; }
    public IAsyncRelayCommand DistributeHoldingsSanierungCommand { get; }
    public IAsyncRelayCommand DistributeShaftsNormalCommand { get; }
    public IAsyncRelayCommand DistributeShaftsSanierungCommand { get; }
    public IAsyncRelayCommand DistributeDichtheitCommand { get; }
    public IRelayCommand BrowseExcelExportRootCommand { get; }

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
            patternResolver: sp.DistributionPatterns,
            directoryTreeResolver: sp.DistributionDirectoryTree,
            katasterXtfPaths: sp.KatasterXtfPaths,
            haltungCadastreIndexes: sp.HaltungCadastreIndexes)
    {
    }

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
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _excelExport = excelExport ?? throw new ArgumentNullException(nameof(excelExport));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _costFieldSync = costFieldSync ?? throw new ArgumentNullException(nameof(costFieldSync));
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanRunProjectExportCommands);
        ExportSchaechteCommand = new AsyncRelayCommand(ExportSchaechteAsync, CanRunProjectExportCommands);
        DistributeHoldingsNormalCommand = new AsyncRelayCommand(() => DistributeHoldingsAsync(DistributionVariant.Normal), CanRunDistributeCommands);
        DistributeHoldingsSanierungCommand = new AsyncRelayCommand(() => DistributeHoldingsAsync(DistributionVariant.Sanierung), CanRunDistributeCommands);
        DistributeShaftsNormalCommand = new AsyncRelayCommand(() => DistributeShaftsAsync(DistributionVariant.Normal), CanRunDistributeCommands);
        DistributeShaftsSanierungCommand = new AsyncRelayCommand(() => DistributeShaftsAsync(DistributionVariant.Sanierung), CanRunDistributeCommands);
        DistributeDichtheitCommand = new AsyncRelayCommand(DistributeDichtheitAsync, CanRunDistributeCommands);
        BrowseExcelExportRootCommand = new RelayCommand(BrowseExcelExportRoot);
        _patternResolver = patternResolver ?? new DistributionPatternResolver();
        _directoryTreeResolver = directoryTreeResolver ?? new DistributionDirectoryTreeResolver(_patternResolver);
        _katasterXtfPaths = katasterXtfPaths ?? KatasterXtfPathResolver.CompatibilityService;
        _haltungCadastreIndexes = haltungCadastreIndexes ?? HaltungCadastreIndex.CurrentProvider;
        _settings.MigrateLegacyExcelExportRoot();
        _excelExportRoot = _settings.ExcelExportRoot;
        DistributionTargets = BuildDistributionTargets(_patternResolver);
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
    {
        if (string.IsNullOrWhiteSpace(sharedRoot))
            return null;

        // Excel ist eine einzelne Datei -> keine Ordner-Ebenen, nur Ziel-Wurzel + Datei-Muster.
        var selectedPattern = forceFallback || string.IsNullOrWhiteSpace(cfg.DateiPattern)
            ? fallbackFilePattern
            : cfg.DateiPattern;
        if (string.IsNullOrWhiteSpace(selectedPattern))
            selectedPattern = "Export";

        var relativ = resolver.ResolveRelativePath(
            ordnerPattern: null,
            unterordnerPattern: null,
            dateiPattern: selectedPattern,
            context: new DistributionPatternContext(datum),
            extension: ".xlsx");
        return Path.Combine(sharedRoot, relativ);
    }

    /// <summary>
    /// Baut den Excel-Zielpfad nur aus dem gemeinsamen Zielordner und dem festen Exportnamen.
    /// Die Oberflaeche bietet bewusst keine Dateinamens- oder Verzeichnisbaum-Muster fuer Excel an.
    /// </summary>
    internal static string? BuildFixedExcelPath(string? sharedRoot, string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(sharedRoot))
            return null;
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            throw new ArgumentException("Der feste Excel-Dateiname darf nicht leer sein.", nameof(fileNameWithoutExtension));

        var safeFileName = ProjectPathResolver.SanitizePathSegment(fileNameWithoutExtension.Trim());
        return Path.Combine(sharedRoot.Trim(), safeFileName + ".xlsx");
    }

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
    {
        var target = BuildConfiguredExcelPath(
            sharedRoot, cfg, resolver, datum, fallbackFilePattern);
        if (target is null)
            return null;

        var other = BuildConfiguredExcelPath(
            sharedRoot, otherCfg, resolver, datum, otherFallbackFilePattern);
        return string.Equals(target, other, StringComparison.OrdinalIgnoreCase)
            ? BuildConfiguredExcelPath(
                sharedRoot,
                cfg,
                resolver,
                datum,
                fallbackFilePattern,
                forceFallback: true)
            : target;
    }

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
        => !IsPageBusy && _shell.Project is not null;

    /// <summary>Verteilung funktioniert auch ohne Projekt (Ordner-/PDF-basiert).</summary>
    private bool CanRunDistributeCommands()
        => !IsPageBusy;

    partial void OnIsPageBusyChanged(bool value)
    {
        _ = value;
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
    }

    private async Task ExportAsync()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Haltungen.xlsx");
        try
        {
            // Ein gemeinsamer Zielordner, fester Dateiname; ohne Zielordner den Dialog wie bisher.
            var outPath = ResolveConfiguredExcelPath("Haltungen")
                ?? _dialogs.SaveFile("Export (Haltungen.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
            if (outPath is null)
                return;

            IsPageBusy = true;
            using var busy = Busy.Enter("Haltungen werden exportiert …");

            // Vor dem Export die abgeleiteten Kostenfelder auf den aktuellen Stand ziehen
            // (Sanieren=Nein/leer -> geleert, damit nur echte Sanierungen exportiert werden).
            // Gesperrte costs.json (loadError) -> NICHT syncen, um keinen leeren Stand zu schreiben.
            var projectPath = _settings.LastProjectPath ?? "";
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                var store = new AuswertungPro.Next.Infrastructure.Costs.ProjectCostStoreRepository()
                    .Load(projectPath, out var syncLoadError);
                if (syncLoadError is null)
                    _costFieldSync.Sync(_shell.Project, store);
            }

            var res = await Task.Run(() =>
                _excelExport.ExportToTemplate(_shell.Project, templatePath, outPath, headerRow: 11, startRow: 12));
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
            if (res.Ok)
                _toasts.Success($"Haltungen exportiert: {Path.GetFileName(outPath)}");
            else
                _toasts.Error(res.ErrorMessage ?? "Haltungs-Export fehlgeschlagen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Haltungs-Excel-Export");
            LastResult = $"Fehler: {userMessage}";
            _shell.SetStatus("Export fehlgeschlagen");
            _toasts.Error($"Haltungs-Export fehlgeschlagen: {userMessage}");
        }
        finally
        {
            IsPageBusy = false;
        }
    }

    private async Task ExportSchaechteAsync()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Schächte.xlsx");
        try
        {
            var outPath = ResolveConfiguredExcelPath("Schaechte")
                ?? _dialogs.SaveFile("Export (Schaechte.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
            if (outPath is null)
                return;

            if (!File.Exists(templatePath))
            {
                LastResult = $"Fehler: Vorlage nicht gefunden ({templatePath})";
                _shell.SetStatus("Export fehlgeschlagen");
                return;
            }

            IsPageBusy = true;
            using var busy = Busy.Enter("Schächte werden exportiert …");
            var res = await Task.Run(() =>
                _excelExport.ExportSchaechteToTemplate(_shell.Project, templatePath, outPath, headerRow: 12, startRow: 13));
            LastResult = res.Ok ? $"Exportiert: {outPath}" : $"Fehler: {res.ErrorMessage}";
            _shell.SetStatus(res.Ok ? "Exportiert" : "Export fehlgeschlagen");
            if (res.Ok)
                _toasts.Success($"Schächte exportiert: {Path.GetFileName(outPath)}");
            else
                _toasts.Error(res.ErrorMessage ?? "Schacht-Export fehlgeschlagen.");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Schacht-Excel-Export");
            LastResult = $"Fehler: {userMessage}";
            _shell.SetStatus("Export fehlgeschlagen");
            _toasts.Error($"Schacht-Export fehlgeschlagen: {userMessage}");
        }
        finally
        {
            IsPageBusy = false;
        }
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

        try
        {
            IsPageBusy = true;
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
                    project: _shell.Project,
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
                    project: _shell.Project,
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
                    project: _shell.Project,
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
                    project: _shell.Project,
                    progress: progress,
                    directoryConfig: directoryConfig));
            }

            // Aggregation und Formatierung an DistributionSummaryBuilder delegiert
            LastResult = DistributionSummaryBuilder.BuildHoldingDistributionSummary(results, useTxtImport);
            _shell.SetStatus(useTxtImport ? "Haltungsdaten (TXT) verteilt" : "Haltungsdaten verteilt");

            if (!useTxtImport && selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
            if (useTxtImport && selectedTxtFiles.Length > 0)
                StoreTxtFiles(selectedTxtFiles);

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
            IsPageBusy = false;
        }
    }

    // ─── Distribution: Schaechte ───────────────────────────────────────────

    private async Task DistributeShaftsAsync(DistributionVariant variant)
    {
        var mode = _dialogs.ConfirmCancel(
            "PDF-Auswahl:\nJa = einzelne Schacht-PDFs auswaehlen\nNein = ganzen PDF-Ordner verwenden",
            "Schaechte verteilen");
        if (mode == DialogConfirm.Cancel)
            return;

        string? pdfFolder = null;
        string[] selectedPdfFiles = Array.Empty<string>();
        if (mode == DialogConfirm.Yes)
        {
            selectedPdfFiles = _dialogs.OpenFiles("Schacht-PDFs auswaehlen", "PDF (*.pdf)|*.pdf");
            if (selectedPdfFiles.Length == 0)
                return;
        }
        else
        {
            pdfFolder = _dialogs.SelectFolder("PDF-Ordner mit Schachtprotokollen waehlen");
            if (string.IsNullOrWhiteSpace(pdfFolder))
                return;
        }

        var destFolder = ResolveConfiguredDistributionRoot(_settings.SchachtDistribution)
            ?? ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.SchaechteVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;
        var directoryConfig = SnapshotDistributionTree(_settings.SchachtDistribution);

        try
        {
            IsPageBusy = true;
            IsDistributionInProgress = true;
            IsDistributionIndeterminate = true;
            DistributionPercent = 0;
            DistributionProgress = "Schacht-Verteilung gestartet...";
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
            if (selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeShaftFiles(
                    pdfFiles: selectedPdfFiles,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress,
                    directoryConfig: directoryConfig,
                    variant: variant));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeShafts(
                    pdfSourceFolder: pdfFolder!,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress,
                    directoryConfig: directoryConfig,
                    variant: variant));
            }

            // Aggregation und Formatierung an DistributionSummaryBuilder delegiert
            var summary = DistributionSummaryBuilder.BuildShaftDistributionSummary(results);
            var pdfUpdated = ApplyPdfPathsToSchachtRecords(results);
            LastResult = pdfUpdated > 0
                ? summary + $"PDF-Pfade aktualisiert: {pdfUpdated}{Environment.NewLine}"
                : summary;
            _shell.SetStatus("Schachtprotokolle verteilt");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
            IsPageBusy = false;
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

        try
        {
            IsPageBusy = true;
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

            IReadOnlyList<HoldingFolderDistributor.DistributionResult> results;
            if (selectedPdfFiles.Length > 0)
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeDichtheitFiles(
                    pdfFiles: selectedPdfFiles,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
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
                    project: _shell.Project,
                    progress: progress,
                    cadastre: cadastre,
                    directoryConfig: directoryConfig));
            }

            // Aggregation und Formatierung an DistributionSummaryBuilder delegiert
            LastResult = DistributionSummaryBuilder.BuildDichtheitDistributionSummary(results);
            _shell.SetStatus("Dichtheitsprüfungsprotokolle verteilt");

            if (selectedPdfFiles.Length > 0)
                StorePdfFiles(selectedPdfFiles);
        }
        finally
        {
            IsDistributionInProgress = false;
            IsDistributionIndeterminate = false;
            DistributionProgress = "";
            DistributionPercent = 0;
            IsPageBusy = false;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private int ApplyPdfPathsToSchachtRecords(IReadOnlyList<HoldingFolderDistributor.DistributionResult> results)
    {
        var updated = 0;
        foreach (var r in results)
        {
            if (!r.Success || string.IsNullOrWhiteSpace(r.DestPdfPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                continue;

            var folderName = Path.GetFileName(r.HoldingFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName))
                continue;

            var record = _shell.Project.SchaechteData.FirstOrDefault(x =>
                string.Equals(SanitizePathSegment((x.GetFieldValue("Schachtnummer") ?? "").Trim()), folderName, StringComparison.OrdinalIgnoreCase));
            if (record is null)
                continue;

            record.SetFieldValue("PDF_Path", r.DestPdfPath);
            updated++;
        }

        if (updated > 0)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        return updated;
    }

    private static string SanitizePathSegment(string value)
        => AuswertungPro.Next.Application.Common.ProjectPathResolver.SanitizePathSegment(value);

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

    private void StorePdfFiles(string[] paths)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "PDF");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredPdfFiles(projectDir);
        foreach (var s in stored)
            if (!existing.Contains(s, StringComparer.OrdinalIgnoreCase))
                existing.Add(s);

        _shell.Project.Metadata["PDF_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private void StoreTxtFiles(string[] paths)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "TXT");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredTxtFiles(projectDir);
        foreach (var s in stored)
            if (!existing.Contains(s, StringComparer.OrdinalIgnoreCase))
                existing.Add(s);

        _shell.Project.Metadata["TXT_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private List<string> LoadStoredPdfFiles(string projectDir)
    {
        _ = projectDir;
        _shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw);
        return StoredFileListParser.Parse(raw);
    }

    private List<string> LoadStoredTxtFiles(string projectDir)
    {
        _ = projectDir;
        _shell.Project.Metadata.TryGetValue("TXT_StoredFiles", out var raw);
        return StoredFileListParser.Parse(raw);
    }
}
