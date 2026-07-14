using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Export;
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

    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private string _distributionProgress = "";
    [ObservableProperty] private bool _isDistributionInProgress;
    [ObservableProperty] private bool _isDistributionIndeterminate;
    [ObservableProperty] private double _distributionPercent;
    [ObservableProperty] private bool _isPageBusy;

    /// <summary>Lade-Overlay fuer Langlaeufer (xlsx-Export).</summary>
    public Services.BusyState Busy { get; } = new();

    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand ExportSchaechteCommand { get; }
    public IAsyncRelayCommand DistributeHoldingsCommand { get; }
    public IAsyncRelayCommand DistributeShaftsCommand { get; }
    public IAsyncRelayCommand DistributeDichtheitCommand { get; }

    /// <summary>Konfig-Karten (Ziel-Wurzel + Namens-/Ordner-Muster) je Verteil-/Export-Typ.</summary>
    public IReadOnlyList<DistributionTargetConfigViewModel> DistributionTargets { get; }

    public ExportPageViewModel(ShellViewModel shell, ServiceProvider sp)
        : this(
            shell,
            settings: sp.Settings,
            dialogs: sp.Dialogs,
            excelExport: sp.ExcelExport,
            toasts: sp.Toasts,
            costFieldSync: sp.CostFieldSync)
    {
    }

    public ExportPageViewModel(
        ShellViewModel shell,
        AppSettings settings,
        IDialogService dialogs,
        IExcelExportService excelExport,
        IToastService toasts,
        IDerivedCostFieldSynchronizer costFieldSync,
        IDistributionPatternResolver? patternResolver = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _excelExport = excelExport ?? throw new ArgumentNullException(nameof(excelExport));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _costFieldSync = costFieldSync ?? throw new ArgumentNullException(nameof(costFieldSync));
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanRunProjectExportCommands);
        ExportSchaechteCommand = new AsyncRelayCommand(ExportSchaechteAsync, CanRunProjectExportCommands);
        DistributeHoldingsCommand = new AsyncRelayCommand(DistributeHoldingsAsync, CanRunDistributeCommands);
        DistributeShaftsCommand = new AsyncRelayCommand(DistributeShaftsAsync, CanRunDistributeCommands);
        DistributeDichtheitCommand = new AsyncRelayCommand(DistributeDichtheitAsync, CanRunDistributeCommands);
        _patternResolver = patternResolver ?? new DistributionPatternResolver();
        DistributionTargets = BuildDistributionTargets(_patternResolver);
    }

    /// <summary>
    /// Baut den Excel-Zielpfad aus konfigurierter Ziel-Wurzel + Datei-Muster; <c>null</c>, wenn keine
    /// Wurzel gesetzt ist (dann faellt der Aufrufer auf den Speichern-Dialog zurueck).
    /// Rein und ohne Seiteneffekte -> testbar.
    /// </summary>
    internal static string? BuildConfiguredExcelPath(
        DistributionTargetConfig cfg,
        IDistributionPatternResolver resolver,
        DateTime datum)
    {
        if (string.IsNullOrWhiteSpace(cfg.Root))
            return null;

        // Excel ist eine einzelne Datei -> keine Ordner-Ebenen, nur Ziel-Wurzel + Datei-Muster.
        var relativ = resolver.ResolveRelativePath(
            ordnerPattern: null,
            unterordnerPattern: null,
            dateiPattern: cfg.DateiPattern,
            context: new DistributionPatternContext(datum),
            extension: ".xlsx");
        return Path.Combine(cfg.Root, relativ);
    }

    /// <summary>
    /// Baut die fuenf Konfig-Karten (Haltungen/Schaechte/Dichtheit verteilen + Excel-Export Haltungen/Schaechte).
    /// Die Live-Vorschau nutzt Beispielwerte (Altdorf, Beispiel-Haltung/-Schacht, heutiges Datum);
    /// Aenderungen werden ueber den debounced <see cref="AppSettings.Save"/> persistiert.
    /// </summary>
    private IReadOnlyList<DistributionTargetConfigViewModel> BuildDistributionTargets(IDistributionPatternResolver resolver)
    {
        var heute = DateTime.Today;
        var haltungSample = new DistributionPatternContext(heute, Gemeinde: "Altdorf", Haltung: "06.24341-35625");
        var schachtSample = new DistributionPatternContext(heute, Gemeinde: "Altdorf", Schachtnummer: "KS 60191");

        void OnCfgChanged() => _settings.Save();
        string? BrowseRoot() => _dialogs.SelectFolder("Ziel-Wurzel waehlen");

        const string haltungHinweis = "Platzhalter: {Datum} {Jahr} {Monat} {Gemeinde} {Haltung}";
        const string schachtHinweis = "Platzhalter: {Datum} {Jahr} {Monat} {Gemeinde} {Schachtnummer}";
        const string excelHinweis = "Platzhalter: {Datum} {Jahr} {Monat} {Gemeinde}";

        return new[]
        {
            new DistributionTargetConfigViewModel(
                "Haltungen verteilen", "PDF-Protokoll + Video je Haltung",
                _settings.HaltungDistribution, resolver, haltungSample, ".pdf",
                showFolderLevels: true, haltungHinweis, OnCfgChanged, BrowseRoot),
            new DistributionTargetConfigViewModel(
                "Schächte verteilen", "Schachtprotokoll je Schacht",
                _settings.SchachtDistribution, resolver, schachtSample, ".pdf",
                showFolderLevels: true, schachtHinweis, OnCfgChanged, BrowseRoot),
            new DistributionTargetConfigViewModel(
                "Dichtheitsprüfung verteilen", "DP-Protokoll je Schacht",
                _settings.DichtheitDistribution, resolver, schachtSample, ".pdf",
                showFolderLevels: true, schachtHinweis, OnCfgChanged, BrowseRoot),
            new DistributionTargetConfigViewModel(
                "Excel-Export Haltungen", "Eine Datei (Haltungen.xlsx)",
                _settings.HaltungExport, resolver, haltungSample, ".xlsx",
                showFolderLevels: false, excelHinweis, OnCfgChanged, BrowseRoot),
            new DistributionTargetConfigViewModel(
                "Excel-Export Schächte", "Eine Datei (Schächte.xlsx)",
                _settings.SchachtExport, resolver, schachtSample, ".xlsx",
                showFolderLevels: false, excelHinweis, OnCfgChanged, BrowseRoot),
        };
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
        DistributeHoldingsCommand.NotifyCanExecuteChanged();
        DistributeShaftsCommand.NotifyCanExecuteChanged();
        DistributeDichtheitCommand.NotifyCanExecuteChanged();
    }

    private async Task ExportAsync()
    {
        // Konfigurierte Ziel-Wurzel + Datei-Muster nutzen; ohne Wurzel den Speichern-Dialog wie bisher.
        var outPath = ResolveConfiguredExcelPath(_settings.HaltungExport)
            ?? _dialogs.SaveFile("Export (Haltungen.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Haltungen.xlsx");
        try
        {
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
        finally
        {
            IsPageBusy = false;
        }
    }

    private async Task ExportSchaechteAsync()
    {
        // Konfigurierte Ziel-Wurzel + Datei-Muster nutzen; ohne Wurzel den Speichern-Dialog wie bisher.
        var outPath = ResolveConfiguredExcelPath(_settings.SchachtExport)
            ?? _dialogs.SaveFile("Export (Schaechte.xlsx)", "Excel (*.xlsx)|*.xlsx", ".xlsx");
        if (outPath is null) return;

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Export_Vorlage", "Schächte.xlsx");
        if (!File.Exists(templatePath))
        {
            LastResult = $"Fehler: Vorlage nicht gefunden ({templatePath})";
            _shell.SetStatus("Export fehlgeschlagen");
            return;
        }

        try
        {
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
        finally
        {
            IsPageBusy = false;
        }
    }

    // ─── Distribution: Haltungen ───────────────────────────────────────────

    private async Task DistributeHoldingsAsync()
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

        var destFolder = ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.HaltungenVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;

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
                    progress: progress));
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
                    progress: progress));
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
                    progress: progress));
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
                    progress: progress));
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

    private async Task DistributeShaftsAsync()
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

        var destFolder = ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.SchaechteVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;

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
                    progress: progress));
            }
            else
            {
                results = await Task.Run(() => HoldingFolderDistributor.DistributeShafts(
                    pdfSourceFolder: pdfFolder!,
                    destGemeindeFolder: destFolder,
                    moveInsteadOfCopy: false,
                    overwrite: false,
                    project: _shell.Project,
                    progress: progress));
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

        var destFolder = ResolveDistributionSubfolder(AuswertungPro.Next.Infrastructure.Import.ProjectStructure.HaltungenVerteilt);
        if (string.IsNullOrWhiteSpace(destFolder)) return;

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
                var katasterPfad = KatasterXtfPathResolver.Resolve(_settings);
                if (!string.IsNullOrWhiteSpace(katasterPfad))
                    cadastre = await Task.Run(() => HaltungCadastreIndex.EnsureAndLoad(katasterPfad));
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
                    cadastre: cadastre));
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
                    cadastre: cadastre));
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
    /// Excel-Zielpfad aus der Konfiguration (Ziel-Wurzel + Datei-Muster); legt den Zielordner an.
    /// Null -> keine Wurzel gesetzt -> Aufrufer nutzt den Speichern-Dialog.
    /// </summary>
    private string? ResolveConfiguredExcelPath(DistributionTargetConfig cfg)
    {
        var ziel = BuildConfiguredExcelPath(cfg, _patternResolver, DateTime.Today);
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
