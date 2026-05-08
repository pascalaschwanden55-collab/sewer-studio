using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using System.Net.Http;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel : ObservableObject
{
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();
    private const int MinimumSamplesForModelTraining = 25;
    private const int StrongModelThreshold = 100;
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
    public event Action? RecordsOrderChanged;
    // Phase 5.1.B Etappe 4 Sub-D: ServiceProvider-Field entfernt — alle Bundle-Aufrufer
    // (PlayerWindow, ProtocolEntryEditorDialog, ProtocolObservationsWindow) ziehen
    // ihre Services nun selbst via App.Resolve<T>() aus dem DI-Container.
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _saveBannerTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly IMeasureRecommendationService _measureRecommendationService;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand EditSanierenOptionsCommand { get; }
    public IRelayCommand PreviewSanierenOptionsCommand { get; }
    public IRelayCommand ResetSanierenOptionsCommand { get; }
    public IRelayCommand<object?> AddSanierenOptionCommand { get; }
    public IRelayCommand<object?> RemoveSanierenOptionCommand { get; }
    public IRelayCommand EditEigentuemerOptionsCommand { get; }
    public IRelayCommand PreviewEigentuemerOptionsCommand { get; }
    public IRelayCommand ResetEigentuemerOptionsCommand { get; }
    public IRelayCommand<object?> AddEigentuemerOptionCommand { get; }
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand { get; }
    public IRelayCommand EditPruefungsresultatOptionsCommand { get; }
    public IRelayCommand PreviewPruefungsresultatOptionsCommand { get; }
    public IRelayCommand ResetPruefungsresultatOptionsCommand { get; }
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand { get; }
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand { get; }
    public IRelayCommand EditReferenzpruefungOptionsCommand { get; }
    public IRelayCommand PreviewReferenzpruefungOptionsCommand { get; }
    public IRelayCommand ResetReferenzpruefungOptionsCommand { get; }
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand { get; }
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand { get; }
    public IRelayCommand EditEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand PreviewEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand ResetEmpfohleneSanierungsmassnahmenOptionsCommand { get; }
    public IRelayCommand<object?> AddEmpfohleneSanierungsmassnahmenOptionCommand { get; }
    public IRelayCommand<object?> RemoveEmpfohleneSanierungsmassnahmenOptionCommand { get; }
    public IRelayCommand<HaltungRecord?> PlayVideoCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenProtocolCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenVideoAiPipelineCommand { get; }
    public IRelayCommand<HaltungRecord?> RelinkVideoCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenOriginalPdfCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintAwuHaltungsprotokollCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> RestoreCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> SuggestMeasuresCommand { get; }
    public IRelayCommand SuggestAllMeasuresCommand { get; }
    public IRelayCommand<HaltungRecord?> OptimizeSanierungKiCommand { get; }
    public IRelayCommand ShowModelStatusCommand { get; }
    public IRelayCommand SearchAndLinkMediaCommand { get; }
    public IRelayCommand<HaltungRecord?> OpenHydraulikCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintHydraulikCommand { get; }
    public IRelayCommand<HaltungRecord?> PrintDossierCommand { get; }

    /// <summary>Phase 1.4: Steuert Sichtbarkeit der Hydraulik-Toolbar-Buttons.
    /// Default = true (alles sichtbar wie heute).</summary>
    public bool ShowExpertenmodusFeatures => App.Resolve<AppSettings>().ShowExpertenmodusFeatures;

    public IReadOnlyList<string> Columns => FieldCatalog.ColumnOrder;
    public ObservableCollection<HaltungRecord> Records => _shell.Project.Data;
    public Project Project => _shell.Project;

    public ObservableCollection<string> SanierenOptions { get; }
    public ObservableCollection<string> EigentuemerOptions { get; }
    public ObservableCollection<string> PruefungsresultatOptions { get; }
    public ObservableCollection<string> ReferenzpruefungOptions { get; }
    public ObservableCollection<string> EmpfohleneSanierungsmassnahmenOptions { get; }
    public ObservableCollection<string> AusgefuehrtDurchOptions { get; }
    public ObservableCollection<ProtocolEntry> SelectedProtocolEntries { get; } = new();

    [ObservableProperty] private HaltungRecord? _selected;
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private bool _isSaveStatusVisible;
    [ObservableProperty] private string _learningInfo = string.Empty;
    [ObservableProperty] private bool _isLearningInfoVisible;
    [ObservableProperty] private string _learningTrafficLightColor = "#C62828";
    [ObservableProperty] private string _learningTrafficLightText = "Rot";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _searchResultInfo = string.Empty;

    /// <summary>
    /// Normalisierte Haltungsnamen die im Training Center erfasst sind.
    /// Wird beim Start geladen; DataPage nutzt dieses Set für die rote Zeilenmarkierung.
    /// </summary>
    public HashSet<string> TrainedHaltungen { get; } = new(StringComparer.OrdinalIgnoreCase);
    [ObservableProperty] private double _gridMinRowHeight = 38d;
    [ObservableProperty] private double _gridZoom = 1.0d;
    [ObservableProperty] private bool _isColumnReorderEnabled;
    public IRelayCommand ClearSearchCommand { get; }
    public bool IsProjectReady => _shell.IsProjectReady;
    public bool IsDataGridReadOnly => !_shell.IsProjectReady;

    public DataPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _measureRecommendationService = App.Resolve<AuswertungPro.Next.Application.Ai.IMeasureRecommendationService>();
        _saveBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveBannerTimer.Tick += (_, __) =>
        {
            _saveBannerTimer.Stop();
            IsSaveStatusVisible = false;
        };
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoSaveTimer.Tick += (_, __) => AutoSaveOnTimerTick();
        _shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
            {
                OnPropertyChanged(nameof(IsProjectReady));
                OnPropertyChanged(nameof(IsDataGridReadOnly));
            }
            else if (e.PropertyName == nameof(ShellViewModel.Project))
            {
                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(Records));
                UpdateSearchResultInfo(Records.Count);
            }
        };

        var uiLayout = App.Resolve<AppSettings>().DataPageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 240d
            ? uiLayout.GridMinRowHeight
            : 38d;
        GridZoom = uiLayout.GridZoom is >= 0.5d and <= 2.0d
            ? uiLayout.GridZoom
            : 1.0d;
        IsColumnReorderEnabled = uiLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadReferenzpruefungOptions());
        EmpfohleneSanierungsmassnahmenOptions = new ObservableCollection<string>(
            DropdownOptionsStore.LoadEmpfohleneSanierungsmassnahmenOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));

        // Seed measure template names from Offerten into dropdown if missing
        SeedMeasureTemplateNames();

        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        SaveCommand = new RelayCommand(Save);
        EditSanierenOptionsCommand = new RelayCommand(EditSanierenOptions);
        PreviewSanierenOptionsCommand = new RelayCommand(PreviewSanierenOptions);
        ResetSanierenOptionsCommand = new RelayCommand(ResetSanierenOptions);
        AddSanierenOptionCommand = new RelayCommand<object?>(AddSanierenOption);
        RemoveSanierenOptionCommand = new RelayCommand<object?>(RemoveSanierenOption);
        EditEigentuemerOptionsCommand = new RelayCommand(EditEigentuemerOptions);
        PreviewEigentuemerOptionsCommand = new RelayCommand(PreviewEigentuemerOptions);
        ResetEigentuemerOptionsCommand = new RelayCommand(ResetEigentuemerOptions);
        AddEigentuemerOptionCommand = new RelayCommand<object?>(AddEigentuemerOption);
        RemoveEigentuemerOptionCommand = new RelayCommand<object?>(RemoveEigentuemerOption);
        EditPruefungsresultatOptionsCommand = new RelayCommand(EditPruefungsresultatOptions);
        PreviewPruefungsresultatOptionsCommand = new RelayCommand(PreviewPruefungsresultatOptions);
        ResetPruefungsresultatOptionsCommand = new RelayCommand(ResetPruefungsresultatOptions);
        AddPruefungsresultatOptionCommand = new RelayCommand<object?>(AddPruefungsresultatOption);
        RemovePruefungsresultatOptionCommand = new RelayCommand<object?>(RemovePruefungsresultatOption);
        EditReferenzpruefungOptionsCommand = new RelayCommand(EditReferenzpruefungOptions);
        PreviewReferenzpruefungOptionsCommand = new RelayCommand(PreviewReferenzpruefungOptions);
        ResetReferenzpruefungOptionsCommand = new RelayCommand(ResetReferenzpruefungOptions);
        AddReferenzpruefungOptionCommand = new RelayCommand<object?>(AddReferenzpruefungOption);
        RemoveReferenzpruefungOptionCommand = new RelayCommand<object?>(RemoveReferenzpruefungOption);
        EditEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(EditEmpfohleneSanierungsmassnahmenOptions);
        PreviewEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(PreviewEmpfohleneSanierungsmassnahmenOptions);
        ResetEmpfohleneSanierungsmassnahmenOptionsCommand = new RelayCommand(ResetEmpfohleneSanierungsmassnahmenOptions);
        AddEmpfohleneSanierungsmassnahmenOptionCommand = new RelayCommand<object?>(AddEmpfohleneSanierungsmassnahmenOption);
        RemoveEmpfohleneSanierungsmassnahmenOptionCommand = new RelayCommand<object?>(RemoveEmpfohleneSanierungsmassnahmenOption);
        PlayVideoCommand = new RelayCommand<HaltungRecord?>(PlayVideo);
        OpenProtocolCommand = new RelayCommand<HaltungRecord?>(OpenProtocol);
        OpenVideoAiPipelineCommand = new RelayCommand<HaltungRecord?>(OpenVideoAiPipeline);
        RelinkVideoCommand = new RelayCommand<HaltungRecord?>(RelinkVideo);
        OpenOriginalPdfCommand = new RelayCommand<HaltungRecord?>(OpenOriginalPdf);
        PrintAwuHaltungsprotokollCommand = new RelayCommand<HaltungRecord?>(PrintAwuHaltungsprotokollPdf);
        OpenCostsCommand = new RelayCommand<HaltungRecord?>(OpenCosts, CanOpenCosts);
        RestoreCostsCommand = new RelayCommand<HaltungRecord?>(RestoreCosts, CanRestoreCosts);
        SuggestMeasuresCommand = new RelayCommand<HaltungRecord?>(SuggestMeasures, CanSuggestMeasures);
        SuggestAllMeasuresCommand = new RelayCommand(SuggestAllMeasures);
        OptimizeSanierungKiCommand = new RelayCommand<HaltungRecord?>(OpenSanierungOptimizationWindow, CanOpenCosts);
        ShowModelStatusCommand = new RelayCommand(ShowModelStatus);
        SearchAndLinkMediaCommand = new RelayCommand(OpenMediaSearchWindow);
        OpenHydraulikCommand = new RelayCommand<HaltungRecord?>(OpenHydraulikPanel);
        PrintHydraulikCommand = new RelayCommand<HaltungRecord?>(PrintHydraulikPdf);
        PrintDossierCommand = new RelayCommand<HaltungRecord?>(PrintDossierPdf);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        PropertyChanged += DataPageViewModel_PropertyChanged;
        UpdateLearningInfo();
        LoadTrainedHaltungenAsync().SafeFireAndForget("TrainedHaltungen");
    }

    partial void OnGridMinRowHeightChanged(double value)
    {
        var clamped = Math.Clamp(value, 24d, 240d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
            return;
        }

        PersistDataPageBasicUiSettings();
    }

    partial void OnGridZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.5d, 2.0d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridZoom = clamped;
            return;
        }

        PersistDataPageBasicUiSettings();
    }

    partial void OnIsColumnReorderEnabledChanged(bool value)
    {
        _ = value;
        PersistDataPageBasicUiSettings();
    }

    private void DataPageViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Selected))
        {
            (RemoveCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (OpenCostsCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (RestoreCostsCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (SuggestMeasuresCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();
            (OptimizeSanierungKiCommand as RelayCommand<HaltungRecord?>)?.NotifyCanExecuteChanged();

            if (Selected is not null)
            {
                NormalizeSelectedFindings(Selected);
                SyncSelectedProtocolFromFindings(Selected);
            }

            RefreshSelectedProtocolEntries();
        }
    }

    private static readonly Regex MeterRegex = new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);
    private static readonly Regex ContinuousDefectMarkerRegex = new(@"^[AB]\d{2}$", RegexOptions.Compiled);
    private static readonly Regex EmbeddedVsaCodeRegex = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    private void NormalizeSelectedFindings(HaltungRecord record)
    {
        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var changed = false;
        foreach (var f in record.VsaFindings)
        {
            var raw = f.Raw ?? string.Empty;

            if (f.MeterStart is null && f.SchadenlageAnfang is null && !string.IsNullOrWhiteSpace(raw))
            {
                var meter = TryParseMeter(raw);
                if (meter is not null)
                {
                    f.MeterStart = meter;
                    changed = true;
                }
            }

            if (f.MeterEnd is null && f.SchadenlageEnde is null && !string.IsNullOrWhiteSpace(raw))
            {
                // If no explicit end, leave empty; but if text has a second meter, use it.
                var meterRange = TryParseSecondMeter(raw);
                if (meterRange is not null)
                {
                    f.MeterEnd = meterRange;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(f.MPEG) && !string.IsNullOrWhiteSpace(raw))
            {
                var mpeg = TryParseTime(raw);
                if (!string.IsNullOrWhiteSpace(mpeg))
                {
                    f.MPEG = mpeg;
                    changed = true;
                }
            }
        }

        var deduped = new List<VsaFinding>(record.VsaFindings.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in record.VsaFindings)
        {
            var effectiveCode = ResolveFindingEffectiveCode(finding.KanalSchadencode, finding.Raw);
            if (!string.Equals(finding.KanalSchadencode, effectiveCode, StringComparison.OrdinalIgnoreCase))
            {
                finding.KanalSchadencode = effectiveCode;
                changed = true;
            }

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
            var meterKey = meter.HasValue
                ? meter.Value.ToString("F2", CultureInfo.InvariantCulture)
                : string.Empty;
            var key = $"{effectiveCode}|{meterKey}";
            if (!seen.Add(key))
            {
                changed = true;
                continue;
            }

            deduped.Add(finding);
        }

        if (deduped.Count != record.VsaFindings.Count)
        {
            record.VsaFindings = deduped;
            changed = true;
        }

        if (!changed)
            return;

        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);
    }

    private static double? TryParseMeter(string raw)
    {
        var match = MeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var valueText = match.Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static double? TryParseSecondMeter(string raw)
    {
        var matches = MeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var valueText = matches[1].Groups[1].Value.Replace(',', '.');
        if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static string? TryParseTime(string raw)
    {
        var match = TimeRegex.Match(raw);
        if (!match.Success)
            return null;

        return match.Groups[1].Value;
    }

    private static double? TryParseDnMm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            && value > 0)
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
            && value > 0)
        {
            return value;
        }

        if (text.Contains(',') && text.Contains('.'))
        {
            var commaAsDecimal = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
            if (double.TryParse(commaAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;

            var dotAsDecimal = text.Replace(",", string.Empty, StringComparison.Ordinal);
            if (double.TryParse(dotAsDecimal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }
        else if (text.Contains(','))
        {
            var normalized = text.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0)
                return value;
        }

        var digitsOnly = text.Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(digitsOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 50)
            return value;

        return null;
    }

    private void OpenOriginalPdf(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = _shell.GetProjectFolder() ?? "";
        var paths = ResolveOriginalPdfPaths(record, projectFolder);

        if (paths.Count == 0)
        {
            var name = record.GetFieldValue("Haltungsname") ?? "(unbekannt)";
            _dialogs.ShowMessage(
                $"Kein PDF gefunden fuer Haltung '{name}'.\n\nPruefen Sie, ob das Original-PDF im Projektordner liegt.",
                "Haltungsprotokoll (PDF)",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(paths[0]) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"PDF konnte nicht geoeffnet werden:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
    {
        var paths = new List<string>();

        // PDF_Path
        var pdfPath = record.GetFieldValue("PDF_Path")?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        // PDF_All (semikolon-getrennt)
        var pdfAll = record.GetFieldValue("PDF_All")?.Trim();
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            foreach (var part in pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries))
                AddResolvedPdf(paths, part.Trim(), projectFolder);
        }

        return paths;
    }

    private static bool HasPrintableDossierPhotos(HaltungRecord record, string projectFolder)
    {
        var entries = record.Protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return false;

        foreach (var entry in entries)
        {
            if (entry.IsDeleted || entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            foreach (var raw in entry.FotoPaths)
            {
                var resolved = ResolveDossierPhotoPath(raw, projectFolder);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    return true;
            }
        }

        return false;
    }

    private static string? ResolveDossierPhotoPath(string? raw, string projectFolder)
    {
        // Containment-Check: Dossier-Foto muss IM Projektordner liegen.
        // Verhindert dass eine manipulierte Projektdatei (extern geoeffnet)
        // beliebige lokale Bilder ins Dossier-PDF einbettet.
        return AuswertungPro.Next.Application.Common.ProjectPathResolver
            .ResolveContainedFile(raw, projectFolder);
    }

    private static void ResolveSchachtPdfPaths(SchachtRecord schacht, string projectFolder, List<string> paths)
    {
        var pdfPath = schacht.GetFieldValue("PDF_Path")?.Trim();
        AddResolvedPdf(paths, pdfPath, projectFolder);

        var link = schacht.GetFieldValue("Link")?.Trim();
        if (!string.IsNullOrWhiteSpace(link) && link.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            AddResolvedPdf(paths, link, projectFolder);
    }

    private static void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        // Containment-Check: PDFs aus Projektdateien duerfen nur AUF Dateien
        // im Projektordner zeigen. Blockiert manipulierte Projektdateien, die
        // willkuerliche lokale PDFs (z.B. C:\Users\...) ins Dossier einbinden.
        var contained = AuswertungPro.Next.Application.Common.ProjectPathResolver
            .ResolveContainedFile(raw, projectFolder);
        if (contained != null)
        {
            if (!paths.Contains(contained, StringComparer.OrdinalIgnoreCase))
                paths.Add(contained);
            return;
        }

        // Fallback: nur Dateinamen im Projektordner suchen (Pfad selbst war
        // ausserhalb / nicht-existent). Das ist sicher, weil TryFindPdfInProject
        // ausschliesslich innerhalb projectFolder/Haltungen/Misc/Docu/PDF/...
        // sucht.
        if (!string.IsNullOrWhiteSpace(projectFolder))
        {
            var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
            var fallback = TryFindPdfInProject(Path.GetFileName(normalized), projectFolder);
            if (fallback != null && !paths.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                paths.Add(fallback);
        }
    }

    private static string? TryFindPdfInProject(string fileName, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // 1. Direkt im Projektordner
        var direct = Path.Combine(projectFolder, fileName);
        if (File.Exists(direct))
            return direct;

        // 2. In Haltungen/<ID>/ Unterordnern
        var haltungenDir = Path.Combine(projectFolder, "Haltungen");
        if (Directory.Exists(haltungenDir))
        {
            try
            {
                var found = Directory.GetFiles(haltungenDir, fileName, SearchOption.AllDirectories);
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        // 3. In typischen Unterordnern (Misc, Docu, PDF, Protokolle)
        foreach (var sub in new[] { "Misc", "Docu", "PDF", "Protokolle", "Dokumente" })
        {
            var subDir = Path.Combine(projectFolder, sub);
            if (!Directory.Exists(subDir))
                continue;
            try
            {
                var found = Directory.GetFiles(subDir, fileName, SearchOption.AllDirectories);
                if (found.Length > 0)
                    return found[0];
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        return null;
    }

    private SchachtRecord? FindSchachtByNummer(string? nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            return null;
        return _shell.Project.SchaechteData.FirstOrDefault(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), nummer, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeFilenamePart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        foreach (var c in Path.GetInvalidFileNameChars())
            text = text.Replace(c, '_');
        return text.Trim();
    }

    private string? ResolveExistingPath(string? raw)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath))
        {
            var baseDir = Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                // SEC-C1-Hardening (Audit 2026-04-23): Containment-Check fuer
                // relative Pfade gegen den Projekt-Basisordner. Verhindert
                // dass `..\..\windows\system32\...` aus einer manipulierten
                // Projektdatei aus dem Projekt heraussspringt.
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                var rootWithSep = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
                if (combined.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(combined))
                    return combined;
            }
        }

        return null;
    }

    private void ShowSaveStatus(string? text)
    {
        SaveStatus = string.IsNullOrWhiteSpace(text) ? "Gespeichert" : text;
        IsSaveStatusVisible = true;
        _saveBannerTimer.Stop();
        _saveBannerTimer.Start();
    }

    public void RefreshSelectedRecord()
    {
        if (Selected is not null)
            RefreshRecordInGrid(Selected);
    }

    private void RefreshRecordInGrid(HaltungRecord record)
    {
        var index = Records.IndexOf(record);
        if (index < 0)
            return;

        Records[index] = record;
        if (Selected?.Id == record.Id)
            Selected = record;
    }

    private void PersistDataPageBasicUiSettings()
    {
        var layout = App.Resolve<AppSettings>().DataPageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.GridZoom = GridZoom;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        App.Resolve<AppSettings>().DataPageLayout = layout;
        App.Resolve<AppSettings>().Save();
    }
}
