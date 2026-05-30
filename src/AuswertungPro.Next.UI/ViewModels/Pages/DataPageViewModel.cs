using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Helpers;
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
using AuswertungPro.Next.Infrastructure.Ai;
using System.Net.Http;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Hydraulik;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel : ObservableObject
{
    private const int MinimumSamplesForModelTraining = 25;
    private const int StrongModelThreshold = 100;
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
    public event Action? RecordsOrderChanged;
    /// <summary>
    /// Aktualisiert die laufende Nummer (NR) aller Records entsprechend der aktuellen Reihenfolge.
    /// </summary>
    private void UpdateNr()
    {
        for (int i = 0; i < Records.Count; i++)
        {
            Records[i].SetFieldValue("NR", (i + 1).ToString(), FieldSource.Manual, true);
        }
    }
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
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
        _measureRecommendationService = _sp.MeasureRecommendation;
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

        // Live-Control: Retry-Handler registrieren, damit der MCP eine Haltung
        // per Name erneut durch die KI-Videoanalyse schicken kann (nur wenn diese Seite lebt).
        LiveControl.LiveControlRetryBridge.Register(TryStartVideoAiPipelineByName);

        var uiLayout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
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

    private bool _isSyncingSelectedProtocol;

    private void SyncSelectedProtocolFromFindings(HaltungRecord record)
    {
        if (_isSyncingSelectedProtocol)
            return;

        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return;

        var needsProtocol = record.Protocol is null
                            || (record.Protocol.Current?.Entries.Count ?? 0) == 0
                            && (record.Protocol.Original?.Entries.Count ?? 0) == 0;
        if (!needsProtocol)
            return;

        _isSyncingSelectedProtocol = true;
        try
        {
            var entries = VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, ResolveCodeTitle);
            record.Protocol = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();
        }
        finally
        {
            _isSyncingSelectedProtocol = false;
        }
    }

    private void RefreshSelectedProtocolEntries()
    {
        SelectedProtocolEntries.Clear();
        var list = Selected?.Protocol?.Current?.Entries;
        if (list is null || list.Count == 0)
            return;

        foreach (var entry in list.Where(e => !e.IsDeleted))
        {
            // Beschreibung aus dem VSA-Katalog auflösen, wenn sie leer ist
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) || entry.Beschreibung.Length <= 3)
            {
                if (!string.IsNullOrWhiteSpace(entry.Code) &&
                    _sp.CodeCatalog.TryGet(entry.Code, out var def) &&
                    !string.IsNullOrWhiteSpace(def.Title))
                {
                    entry.Beschreibung = def.Title;
                }
            }

            SelectedProtocolEntries.Add(entry);
        }
    }

    private string? ResolveCodeTitle(string code)
        => _sp.CodeCatalog.TryGet(code, out var codeDef) && !string.IsNullOrWhiteSpace(codeDef.Title)
            ? codeDef.Title
            : null;

    private void NormalizeSelectedFindings(HaltungRecord record)
    {
        if (!VsaFindingNormalizer.Normalize(record))
            return;

        record.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);
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

    private bool CanMoveUp()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx > 0;
    }

    private bool CanMoveDown()
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        return idx >= 0 && idx < Records.Count - 1;
    }

    private void Add()
    {
        var record = _shell.Project.CreateNewRecord();
        _shell.Project.AddRecord(record);
        Selected = record;
        ScheduleAutoSave();
    }

    private void Remove()
    {
        if (Selected is null) return;

        var idx = Records.IndexOf(Selected);
        var removedId = Selected.Id;
        var removed = _shell.Project.RemoveRecord(removedId);
        if (!removed)
        {
            return;
        }

        if (Records.Count == 0)
        {
            Selected = null;
            ScheduleAutoSave();
            return;
        }

        if (idx >= Records.Count) idx = Records.Count - 1;
        Selected = Records[idx];
        ScheduleAutoSave();
    }

    private void MoveUp()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx <= 0) return;
        Records.Move(idx, idx - 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    private void MoveDown()
    {
        if (Selected is null) return;
        var idx = Records.IndexOf(Selected);
        if (idx < 0 || idx >= Records.Count - 1) return;
        Records.Move(idx, idx + 1);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
    }

    /// <summary>
    /// Verschiebt die aktuell selektierte Haltung an die angegebene 1-basierte Position.
    /// Alle Zeilen ab dieser Position rutschen um eins nach unten.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        if (Selected is null) return false;
        var idx = Records.IndexOf(Selected);
        if (idx < 0) return false;

        // 1-basiert -> 0-basiert
        int targetIdx = targetPosition - 1;
        if (targetIdx < 0) targetIdx = 0;
        if (targetIdx >= Records.Count) targetIdx = Records.Count - 1;
        if (targetIdx == idx) return false;

        Records.Move(idx, targetIdx);
        UpdateNr();
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RecordsOrderChanged?.Invoke();
        ScheduleAutoSave();
        return true;
    }

    private void Save()
    {
        var learnedAny = false;
        foreach (var record in Records)
            learnedAny |= _measureRecommendationService.Learn(record);
        if (learnedAny)
            _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
        UpdateLearningInfo();

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        ShowSaveStatus(_shell.Subtitle);
        if (!ok)
            IsSaveStatusVisible = true;
    }

    /// <summary>
    /// Schedules auto-save according to settings.
    /// </summary>
    public void ScheduleAutoSave()
    {
        _shell.Project.Dirty = true;
        var mode = _sp.Settings.DataAutoSaveMode.Normalize();
        switch (mode)
        {
            case AutoSaveMode.OnEachChange:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
            case AutoSaveMode.Every5Minutes:
            case AutoSaveMode.Every10Minutes:
                ScheduleIntervalAutoSave(mode);
                break;
            case AutoSaveMode.Disabled:
                _autoSaveTimer.Stop();
                break;
            default:
                _autoSaveTimer.Stop();
                AutoSave();
                break;
        }
    }

    private void ScheduleIntervalAutoSave(AutoSaveMode mode)
    {
        var interval = mode.GetInterval();
        if (interval is null)
        {
            _autoSaveTimer.Stop();
            return;
        }

        if (_autoSaveTimer.Interval != interval.Value)
            _autoSaveTimer.Interval = interval.Value;

        if (!_autoSaveTimer.IsEnabled)
            _autoSaveTimer.Start();
    }

    private void AutoSaveOnTimerTick()
    {
        var mode = _sp.Settings.DataAutoSaveMode.Normalize();
        if (mode is not (AutoSaveMode.Every5Minutes or AutoSaveMode.Every10Minutes))
        {
            _autoSaveTimer.Stop();
            return;
        }

        AutoSave();

        // No pending changes left -> no need to keep ticking.
        if (!_shell.Project.Dirty)
            _autoSaveTimer.Stop();
    }

    private void AutoSave()
    {
        if (!_shell.IsProjectReady || !_shell.Project.Dirty)
            return;

        SaveDropdownOptions();
        var ok = _shell.TrySaveProject();
        if (ok)
            ShowSaveStatus("Automatisch gespeichert");
    }

    private void PlayVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var options = new PlayerWindowOptions(
                EnableHardwareDecoding: _sp.Settings.VideoHwDecoding,
                DropLateFrames: _sp.Settings.VideoDropLateFrames,
                SkipFrames: _sp.Settings.VideoSkipFrames,
                FileCachingMs: _sp.Settings.VideoFileCachingMs,
                NetworkCachingMs: _sp.Settings.VideoNetworkCachingMs,
                CodecThreads: _sp.Settings.VideoCodecThreads,
                VideoOutput: _sp.Settings.VideoOutput);

            var damageOverlay = DataPageVideoOverlayBuilder.Build(record);

            var window = new PlayerWindow(path, options,
                damageOverlay: damageOverlay,
                serviceProvider: _sp,
                haltungId: record.Id.ToString(),
                haltungRecord: record)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.Show();
        }
        catch (Exception ex)
        {
            var logPath = TryWriteVideoStartErrorLog(ex, path);
            var nativeHint = ex.Message.Contains("native side", StringComparison.OrdinalIgnoreCase)
                ? "\n\nHinweis: Bitte pruefen, ob 'VideoLAN.LibVLC.Windows' fuer dieses Projekt/Plattform installiert ist."
                : string.Empty;
            var msg = logPath is null
                ? $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\n(Details: ex.ToString() nicht gespeichert)"
                : $"Video konnte nicht gestartet werden:\n{ex.Message}{nativeHint}\n\nDetails gespeichert in:\n{logPath}";
            _sp.Dialogs.Error(msg, "Video");
        }
    }

    private void OpenProtocol(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath)
            ? null
            : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var resolvedVideoPath = ResolveExistingPath(record.GetFieldValue("Link"));
        var dlg = new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow(
            record,
            _shell.Project,
            _sp,
            resolvedVideoPath,
            projectFolder,
            markDirty: () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
                ScheduleAutoSave();
            });
        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        dlg.ShowDialog();

        // Protokoll-Änderungen in die Haltungsfelder zurückschreiben.
        SyncObservationsToHoldingFields(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();
    }

    public void SyncObservationsToHoldingFields(HaltungRecord? record, bool showStatus = false)
    {
        if (record is null)
            return;

        var entries = record.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries is null)
            return;

        var changed = false;

        var mapped = DataPageProtocolObservationMapper.Build(entries, record.VsaFindings);
        var primaryText = mapped.PrimaryDamageText;
        var currentPrimary = record.GetFieldValue("Primaere_Schaeden") ?? string.Empty;
        if (!string.Equals(currentPrimary, primaryText, StringComparison.Ordinal))
        {
            record.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
            changed = true;
        }

        if (DataPageProtocolObservationMapper.HasFindingChanges(record.VsaFindings, mapped.Findings))
        {
            record.VsaFindings = mapped.Findings;
            changed = true;
        }

        if (!changed)
            return;

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        RefreshRecordInGrid(record);

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();

        ScheduleAutoSave();
        if (showStatus)
            _shell.SetStatus("Beobachtungen in Haltungen-Feldern aktualisiert");
    }

    private void OpenVideoAiPipeline(HaltungRecord? record)
    {
        if (record is null) return;

        var videoPath = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(videoPath)) return;

        var allowedCodes = _sp.CodeCatalog.AllowedCodes();
        if (allowedCodes is null || allowedCodes.Count == 0)
        {
            _sp.Dialogs.Warn("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI");
            return;
        }

        var cfg = new AppSettingsAiSettingsProvider()
            .Load()
            .ToRuntimeSettings();
        if (!cfg.Enabled)
        {
            _sp.Dialogs.Info("KI ist deaktiviert (SEWERSTUDIO_AI_ENABLED=0).", "Videoanalyse KI");
            return;
        }

        var timeout = cfg.OllamaRequestTimeout > TimeSpan.Zero
            ? cfg.OllamaRequestTimeout
            : TimeSpan.FromMinutes(30);
        using var http = new HttpClient { Timeout = timeout };
        var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
        var plausibility = new RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var pipeline = _sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);

        var haltungId = record.GetFieldValue("Haltungsname") ?? record.Id.ToString();
        var request = new PipelineRequest(haltungId, videoPath, allowedCodes);

        var win = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var ok = win.ShowDialog() == true;

        if (ok && win.Result?.IsSuccess == true && win.Result.Document is not null)
        {
            record.Protocol = win.Result.Document;

            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;

            RefreshRecordInGrid(record);
            if (Selected?.Id == record.Id)
                RefreshSelectedProtocolEntries();

            ScheduleAutoSave();
        }
    }

    /// <summary>
    /// Startet die KI-Videoanalyse fuer eine Haltung anhand ihres Namens erneut –
    /// genutzt von der Live-Control-Bruecke (MCP retry_holding).
    /// Die Suche laeuft sofort; das Analyse-Fenster wird per Dispatcher nachgeschoben,
    /// damit die Live-Control-Antwort nicht bis zum Schliessen des Fensters blockiert.
    /// </summary>
    public LiveControl.LiveControlRetryResult TryStartVideoAiPipelineByName(string haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return new LiveControl.LiveControlRetryResult(false, "Haltungsname fehlt.");

        var name = haltungsname.Trim();
        var record = _shell.Project.Data.FirstOrDefault(r =>
            string.Equals(r.GetFieldValue("Haltungsname"), name, StringComparison.OrdinalIgnoreCase));

        if (record is null)
            return new LiveControl.LiveControlRetryResult(
                false, $"Haltung '{name}' nicht im geladenen Projekt gefunden.");

        // Modales Analyse-Fenster nicht blockierend hier oeffnen – nachschieben.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => OpenVideoAiPipeline(record));

        return new LiveControl.LiveControlRetryResult(
            true, $"KI-Videoanalyse fuer '{name}' gestartet.");
    }


    private static ProtocolEntry CloneProtocolEntry(ProtocolEntry source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProtocolEntry>(json) ?? new ProtocolEntry();
    }

    private string? EnsureProtocolPath(HaltungRecord record)
    {
        var resolvedLink = ResolveExistingPath(record.GetFieldValue("Link"));

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var storedFilesRaw = _shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) ? raw : null;

        return DataPageProtocolPathResolver.FindProtocolPath(
            record,
            resolvedLink,
            initial,
            _sp.Settings.LastProjectPath,
            storedFilesRaw);
    }

    private static string? TryWriteVideoStartErrorLog(Exception ex, string path)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logsDir);

            var safeName = Path.GetFileNameWithoutExtension(path);
            foreach (var c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "video";

            var file = $"video_start_error_{DateTime.Now:yyyyMMdd_HHmmss}_{safeName}.txt";
            var logPath = Path.Combine(logsDir, file);

            var content =
                $"Time: {DateTime.Now:O}{Environment.NewLine}" +
                $"VideoPath: {path}{Environment.NewLine}" +
                $"Exception:{Environment.NewLine}{ex}{Environment.NewLine}";
            File.WriteAllText(logPath, content);
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private void RelinkVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var path = _sp.Dialogs.OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            initial);
        if (string.IsNullOrWhiteSpace(path))
            return;

        var selectedDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(selectedDir))
        {
            _sp.Settings.LastVideoSourceFolder = selectedDir;
            _sp.Settings.LastVideoFolder = selectedDir; // legacy compatibility
            _sp.Settings.Save();
        }

        SaveVideoLink(record, path, userEdited: true);
    }

    private bool CanOpenCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanRestoreCosts(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private bool CanSuggestMeasures(HaltungRecord? record)
    {
        if (record is not null)
            return true;
        return Selected is not null;
    }

    private void RestoreCosts(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _sp.Dialogs.Warn("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen");
            return;
        }

        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _sp.Dialogs.Info("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen");
            return;
        }

        var store = new ProjectCostStoreRepository().Load(projectPath);
        if (!store.ByHolding.TryGetValue(holding, out var cost))
        {
            var dir = Path.GetDirectoryName(projectPath);
            var storePath = string.IsNullOrWhiteSpace(dir) ? "" : ProjectCostStoreRepository.GetStorePath(dir);
            _sp.Dialogs.Info($"Keine gespeicherten Kosten/Massnahmen gefunden fuer:\n{holding}\n\nDatei:\n{storePath}",
                "Kosten/Massnahmen");
            return;
        }

        ApplyCostsToRecord(record, cost, learn: false);
        _shell.SetStatus($"Kosten/Massnahmen wiederhergestellt: {holding}");
    }

    private void OpenCosts(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.CostCalculator);
    }

    private void SuggestMeasures(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
        if (recommendation.Measures.Count == 0)
        {
            _sp.Dialogs.Info(
                "Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.",
                "Massnahmen");
            return;
        }

        DataPageSanierungCostMapper.ApplyRecommendation(record, recommendation);
        foreach (var suggestion in recommendation.Measures)
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        var sourceText = recommendation.UsedTrainedModel ? "KI-Modell" : "Lernlogik";
        _shell.SetStatus(recommendation.EstimatedTotalCost is null
            ? $"Massnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Massnahmenvorschlag mit Kostenschaetzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
        UpdateLearningInfo(recommendation.SimilarCasesCount, recommendation.EstimatedTotalCost);

        // Show result dialog so user sees the suggested measures
        var summary = string.Join("\n", recommendation.Measures);
        if (recommendation.EstimatedTotalCost is not null)
            summary += $"\n\nGeschaetzte Kosten: {recommendation.EstimatedTotalCost.Value:N2}";
        summary += $"\n\nQuelle: {sourceText}";
        if (recommendation.SimilarCasesCount > 0)
            summary += $" ({recommendation.SimilarCasesCount} aehnliche Faelle)";
        _sp.Dialogs.Info(summary, "Empfohlene Sanierungsmassnahmen");
    }

    /// <summary>
    /// Batch: Fuer alle Haltungen mit Sanierungsbedarf (oder fehlenden Massnahmen)
    /// automatisch Sanierungsmassnahmen vorschlagen.
    /// </summary>
    public void SuggestAllMeasures()
    {
        var records = _shell.Project.Data;
        if (records.Count == 0)
        {
            _sp.Dialogs.Info("Keine Haltungen vorhanden.", "Massnahmen");
            return;
        }

        var filled = 0;
        var skipped = 0;
        var noSuggestion = 0;

        foreach (var record in records)
        {
            // Nur Records mit Sanierungsbedarf oder schlechter Zustandsnote beruecksichtigen
            var pruefung = (record.GetFieldValue("Pruefungsresultat") ?? "").Trim();
            var existingMeasures = (record.GetFieldValue("Empfohlene_Sanierungsmassnahmen") ?? "").Trim();
            var hasDamageCodes = record.VsaFindings is not null && record.VsaFindings.Count > 0
                || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"));

            // Ueberspringe Records die bereits manuell bearbeitete Massnahmen haben
            if (!string.IsNullOrWhiteSpace(existingMeasures))
            {
                var meta = record.FieldMeta.GetValueOrDefault("Empfohlene_Sanierungsmassnahmen");
                if (meta is not null && meta.UserEdited)
                {
                    skipped++;
                    continue;
                }
            }

            // Nur Records mit Sanierungsbedarf oder Schadenscodes verarbeiten
            if (!string.Equals(pruefung, "Sanierungsbedarf", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pruefung, "beobachten", StringComparison.OrdinalIgnoreCase)
                && !hasDamageCodes)
            {
                skipped++;
                continue;
            }

            var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            if (recommendation.Measures.Count == 0)
            {
                noSuggestion++;
                continue;
            }

            DataPageSanierungCostMapper.ApplyRecommendation(record, recommendation);
            foreach (var suggestion in recommendation.Measures)
                AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

            filled++;
        }

        if (filled > 0)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
        }

        _shell.SetStatus($"Massnahmen: {filled} Haltungen befuellt, {skipped} uebersprungen, {noSuggestion} ohne Vorschlag");
    }

    private void OpenSanierungOptimizationWindow(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.AiOptimization);
    }

    private void OpenSanierungsmassnahmenWindow(HaltungRecord? record, InitialFocusMode focus)
    {
        record ??= Selected;
        if (record is null) return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            _sp.Dialogs.Warn("Haltungsname fehlt in der Zeile.", "Sanierungsmassnahmen");
            return;
        }

        // Build CostCalculatorViewModel
        var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        var costCalcVm = new CostCalculatorViewModel(
            holding,
            null,
            recommended,
            _sp.Settings.LastProjectPath,
            cost => ApplyCostsToRecord(record, cost),
            haltungRecord: record,
            projectRecords: Records);

        // Build SanierungOptimizationViewModel (nullable when AI disabled)
        SanierungOptimizationViewModel? optimizationVm = null;
        var cfg = new AppSettingsAiSettingsProvider()
            .Load()
            .ToRuntimeSettings();
        if (cfg.Enabled)
        {
            var ruleResult = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
            RuleRecommendationDto? ruleDto = null;
            if (ruleResult.Measures.Count > 0)
            {
                ruleDto = new RuleRecommendationDto
                {
                    Measures         = ruleResult.Measures,
                    EstimatedCost    = ruleResult.EstimatedTotalCost,
                    UsedTrainedModel = ruleResult.UsedTrainedModel
                };
            }

            var aiService = _sp.CreateSanierungOptimization(cfg);
            optimizationVm = new SanierungOptimizationViewModel(record, aiService, ruleDto);

            optimizationVm.TransferredToPrimary += _ =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty         = true;
                RefreshRecordInGrid(record);
                ScheduleAutoSave();
                _shell.SetStatus($"KI-Sanierungsvorschlag uebertragen: {holding}");
            };
        }

        var vm = new SanierungsmassnahmenViewModel(costCalcVm, optimizationVm, record, focus);
        var win = new SanierungsmassnahmenWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        win.ShowDialog();
    }

    private void ShowModelStatus()
    {
        var stats = _measureRecommendationService.GetStats();
        var status = stats.TrainedModelAvailable ? "Aktiv" : "Noch nicht trainiert";
        var trainedAt = stats.TrainedAtUtc is null
            ? "-"
            : stats.TrainedAtUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var modelSamples = stats.TrainedModelSamples?.ToString(CultureInfo.InvariantCulture) ?? "0";

        var message =
            $"Lernfaelle gesamt: {stats.TotalSamples}\n" +
            $"Schadenscodes: {stats.DistinctDamageCodes}\n" +
            $"Code-Signaturen: {stats.CodeSignatures}\n" +
            $"KI-Modell: {status}\n" +
            $"Modell-Faelle: {modelSamples}\n" +
            $"Letztes Training: {trainedAt}\n" +
            $"Modell-Datei:\n{stats.ModelPath}";

        _sp.Dialogs.Info(message, "KI-Modell Status");
    }

    private string? EnsureVideoPath(HaltungRecord record)
    {
        var resolved = ResolveExistingPath(record.GetFieldValue("Link"));
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            if (!string.Equals(resolved, record.GetFieldValue("Link")?.Trim(), StringComparison.OrdinalIgnoreCase))
                SaveVideoLink(record, resolved, userEdited: false);
            return resolved;
        }

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
        {
            var tool = new VideoSearchTool(initial);
            var res = tool.ResolveForRecord(record);
            if (res.Success && !string.IsNullOrWhiteSpace(res.VideoPath))
                return SaveVideoLink(record, res.VideoPath!, userEdited: false);
        }

        var folder = _sp.Dialogs.SelectFolder("Video-Ordner auswaehlen", initial);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        _sp.Settings.LastVideoSourceFolder = folder;
        _sp.Settings.LastVideoFolder = folder; // legacy compatibility
        _sp.Settings.Save();

        var toolManual = new VideoSearchTool(folder);
        var resManual = toolManual.ResolveForRecord(record);
        if (resManual.Success && !string.IsNullOrWhiteSpace(resManual.VideoPath))
            return SaveVideoLink(record, resManual.VideoPath!, userEdited: false);

        _sp.Dialogs.Info(resManual.Message, "Video");

        var manual = _sp.Dialogs.OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            folder);
        if (string.IsNullOrWhiteSpace(manual))
            return null;

        return SaveVideoLink(record, manual, userEdited: true);
    }

    private string SaveVideoLink(HaltungRecord record, string path, bool userEdited)
    {
        record.SetFieldValue("Link", path, FieldSource.Unknown, userEdited: userEdited);
        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        return path;
    }

    public void OpenMediaSearchWindow()
    {
        if (Records.Count == 0)
        {
            _shell.SetStatus("Keine Haltungen vorhanden.");
            return;
        }

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
                : null;

        var win = new MediaSearchWindow(Records.ToList(), initial);
        win.Owner = System.Windows.Application.Current?.MainWindow;

        if (win.ShowDialog() == true && win.Applied)
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty = true;
            OnPropertyChanged(nameof(Records));
            _shell.SetStatus($"Medien verlinkt: {win.AppliedVideoCount} Videos, {win.AppliedPdfCount} PDFs, {win.AppliedFotoCount} Fotos");
        }
    }

    private void OpenHydraulikPanel(HaltungRecord? record)
    {
        var vm = new HydraulikPanelViewModel(_sp.Settings);

        if (record is not null)
        {
            var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
            var material = record.GetFieldValue("Rohrmaterial");
            vm.LoadFromRecord(dn, material, null);
        }

        var win = new HydraulikPanelWindow(vm);
        win.Owner = System.Windows.Application.Current?.MainWindow;
        win.ShowDialog();
    }

    private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _sp.Dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Haltungsprotokoll AWU");
            return;
        }

        var doc = EnsureProtocolDocumentForPdf(record);
        var holding = record.GetFieldValue("Haltungsname");
        var defaultName = $"Haltungsprotokoll_AWU_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Haltungsprotokoll AWU als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new Application.Reports.HaltungsprotokollPdfOptions
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var projectFolder = _shell.GetProjectFolder() ?? string.Empty;
            var pdf = _sp.ProtocolPdfExporter.BuildHaltungsprotokollPdf(
                _shell.Project,
                record,
                doc,
                projectFolder,
                options);

            File.WriteAllBytes(output, pdf);
            _sp.Dialogs.Info($"AWU-Haltungsprotokoll wurde erstellt:\n{output}", "Haltungsprotokoll AWU");
        }
        catch (Exception ex)
        {
            _sp.Dialogs.Error($"AWU-Haltungsprotokoll konnte nicht erstellt werden:\n{ex.Message}", "Haltungsprotokoll AWU");
        }
    }

    private ProtocolDocument EnsureProtocolDocumentForPdf(HaltungRecord record)
    {
        if (record.Protocol is not null)
        {
            record.Protocol.Current ??= new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = new List<ProtocolEntry>()
            };

            if ((record.Protocol.Original.Entries.Count == 0)
                && (record.Protocol.Current.Entries.Count == 0)
                && record.VsaFindings is { Count: > 0 })
            {
                var imported = VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, ResolveCodeTitle);
                record.Protocol = _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", imported, null);
            }

            return record.Protocol;
        }

        var entries = record.VsaFindings is { Count: > 0 }
            ? VsaFindingToProtocolEntryMapper.BuildEntries(record.VsaFindings, ResolveCodeTitle)
            : Array.Empty<ProtocolEntry>();
        return _sp.Protocols.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
    }

    private async void PrintHydraulikPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _sp.Dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF");
            return;
        }

        // Build input from record
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm")) ?? 300;
        var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
        var vm = new HydraulikPanelViewModel(_sp.Settings);
        vm.LoadFromRecord(dn, materialRaw, null);

        var mat = vm.SelectedMaterial;
        double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
        double wasserstand = dn / 2; // default half-fill

        var input = new HydraulikInput(
            DN_mm: dn,
            Wasserstand_mm: wasserstand,
            Gefaelle_Promille: vm.Gefaelle,
            Kb: kb,
            AbwasserTyp: "MR",
            Temperatur_C: vm.Temperatur);

        var result = HydraulikEngine.Berechne(input);
        if (result is null)
        {
            _sp.Dialogs.Warn("Hydraulik-Berechnung konnte nicht durchgefuehrt werden.\nBitte DN und Gefaelle pruefen.", "Hydraulik PDF");
            return;
        }

        // Show print options dialog
        var dialog = new HydraulikPrintDialog();
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        if (dialog.ShowDialog() != true || dialog.SelectedOptions is null)
            return;

        // SaveFile dialog
        var holding = record.GetFieldValue("Haltungsname") ?? "Haltung";
        var defaultName = $"Hydraulik_{SanitizeFilenamePart(holding)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Hydraulik-Bericht als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var calc = new Application.Reports.HydraulikCalcResult
            {
                DN_mm = input.DN_mm,
                Wasserstand_mm = input.Wasserstand_mm,
                Gefaelle_Promille = input.Gefaelle_Promille,
                Kb = input.Kb,
                AbwasserTyp = input.AbwasserTyp,
                Temperatur_C = input.Temperatur_C,
                Material = mat.Label,
                V_T = result.V_T,
                Q_T = result.Q_T,
                A_T = result.A_T,
                Lu_T = result.Lu_T,
                Rhy_T = result.Rhy_T,
                Bsp = result.Bsp,
                V_V = result.V_V,
                Q_V = result.Q_V,
                Re = result.Re,
                Fr = result.Fr,
                Lambda = result.Lambda,
                Tau = result.Tau,
                Ny = result.Ny,
                Vc = result.Abl.Vc,
                Ic = result.Abl.Ic,
                TauC = result.Abl.TauC,
                Auslastung = result.Auslastung,
                VelocityOk = result.VelocityOk,
                ShearOk = result.ShearOk,
                FroudeOk = result.Fr <= 1,
                AblagerungOk = result.AblagerungOk,
            };

            // PDF-Erzeugung auf Background-Thread (verhindert UI-Freeze)
            var pdf = await Task.Run(() => Application.Reports.HydraulikPdfBuilder.Build(record, calc, options));
            await Task.Run(() => File.WriteAllBytes(output, pdf));

            _sp.Dialogs.Info($"PDF wurde erstellt:\n{output}", "Hydraulik PDF");
        }
        catch (Exception ex)
        {
            _sp.Dialogs.Error($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Hydraulik PDF");
        }
    }

    private async void PrintDossierPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _sp.Dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Dossier");
            return;
        }

        var holdingLabel = record.GetFieldValue("Haltungsname") ?? "";
        var (vonNr, bisNr) = Application.Reports.ProtocolPdfExporter.SplitHoldingNodes(holdingLabel);

        var schachtVon = FindSchachtByNummer(vonNr);
        var schachtBis = FindSchachtByNummer(bisNr);

        // Hydraulik pruefen
        var dn = TryParseDnMm(record.GetFieldValue("DN_mm"));
        var gefaelleRaw = record.GetFieldValue("Gefaelle_Promille");
        double? gefaelle = null;
        if (!string.IsNullOrWhiteSpace(gefaelleRaw))
        {
            var gText = gefaelleRaw.Trim().Replace(',', '.');
            if (double.TryParse(gText, NumberStyles.Float, CultureInfo.InvariantCulture, out var gVal))
                gefaelle = gVal;
        }
        var hydraulikAvailable = dn.HasValue && dn.Value > 0 && gefaelle.HasValue && gefaelle.Value > 0;

        // Kosten pruefen
        var projectFolder = _shell.GetProjectFolder() ?? "";
        var costRepo = new Infrastructure.Costs.ProjectCostStoreRepository();
        var costStore = costRepo.Load(_sp.Settings.LastProjectPath);
        Domain.Models.HoldingCost? holdingCost = null;
        if (costStore.ByHolding.TryGetValue(holdingLabel.Trim(), out var hc))
            holdingCost = hc;
        var kostenField = record.GetFieldValue("Kosten");
        var kostenAvailable = holdingCost?.Measures is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(kostenField)
            || !string.IsNullOrWhiteSpace(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));

        // Original-PDFs pruefen (Haltung + Schaechte)
        var originalPdfPaths = DataPageProtocolPathResolver.ResolveOriginalPdfPaths(record, projectFolder);
        if (schachtVon != null)
            DataPageProtocolPathResolver.ResolveSchachtPdfPaths(schachtVon, projectFolder, originalPdfPaths);
        if (schachtBis != null)
            DataPageProtocolPathResolver.ResolveSchachtPdfPaths(schachtBis, projectFolder, originalPdfPaths);

        // Dialog oeffnen
        var dialog = new DossierPrintDialog();
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        dialog.SetAvailability(
            schachtVon != null, vonNr,
            schachtBis != null, bisNr,
            hydraulikAvailable,
            kostenAvailable,
            originalPdfPaths.Count);

        if (dialog.ShowDialog() != true || dialog.SelectedOptions is null)
            return;

        // SaveFileDialog
        var defaultName = $"Dossier_{SanitizeFilenamePart(holdingLabel)}_{DateTime.Now:yyyyMMdd}.pdf";
        var output = _sp.Dialogs.SaveFile(
            "Haltungsdossier als PDF speichern",
            "PDF (*.pdf)|*.pdf",
            defaultExt: "pdf",
            defaultFileName: defaultName);
        if (string.IsNullOrWhiteSpace(output))
            return;

        try
        {
            // Hydraulik berechnen falls gewuenscht
            Application.Reports.HydraulikCalcResult? calcResult = null;
            if (dialog.SelectedOptions.IncludeHydraulik && hydraulikAvailable)
            {
                var materialRaw = record.GetFieldValue("Rohrmaterial") ?? "";
                var vm = new HydraulikPanelViewModel(_sp.Settings);
                vm.LoadFromRecord(dn!.Value, materialRaw, gefaelle);

                var mat = vm.SelectedMaterial;
                double kb = vm.IsNeuzustand ? mat.KbNeu : mat.KbAlt;
                double wasserstand = dn.Value / 2;

                var input = new HydraulikInput(
                    DN_mm: dn.Value,
                    Wasserstand_mm: wasserstand,
                    Gefaelle_Promille: vm.Gefaelle,
                    Kb: kb,
                    AbwasserTyp: "MR",
                    Temperatur_C: vm.Temperatur);

                var result = HydraulikEngine.Berechne(input);
                if (result != null)
                {
                    calcResult = new Application.Reports.HydraulikCalcResult
                    {
                        DN_mm = input.DN_mm,
                        Wasserstand_mm = input.Wasserstand_mm,
                        Gefaelle_Promille = input.Gefaelle_Promille,
                        Kb = input.Kb,
                        AbwasserTyp = input.AbwasserTyp,
                        Temperatur_C = input.Temperatur_C,
                        Material = mat.Label,
                        V_T = result.V_T, Q_T = result.Q_T, A_T = result.A_T,
                        Lu_T = result.Lu_T, Rhy_T = result.Rhy_T, Bsp = result.Bsp,
                        V_V = result.V_V, Q_V = result.Q_V,
                        Re = result.Re, Fr = result.Fr, Lambda = result.Lambda,
                        Tau = result.Tau, Ny = result.Ny,
                        Vc = result.Abl.Vc, Ic = result.Abl.Ic, TauC = result.Abl.TauC,
                        Auslastung = result.Auslastung,
                        VelocityOk = result.VelocityOk, ShearOk = result.ShearOk,
                        FroudeOk = result.Fr <= 1, AblagerungOk = result.AblagerungOk,
                    };
                }
            }

            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null,
                HoldingCost = dialog.SelectedOptions.IncludeKostenschaetzung ? holdingCost : null,
                OriginalPdfPaths = dialog.SelectedOptions.IncludeOriginalProtokolle ? originalPdfPaths : null,
            };

            var hasDossierBaseSection =
                options.IncludeDeckblatt
                || options.IncludeHaltungsprotokoll
                || (options.IncludeFotos && DataPageDossierAvailability.HasPrintablePhotos(record, projectFolder))
                || (options.IncludeSchachtVon && schachtVon != null)
                || (options.IncludeSchachtBis && schachtBis != null)
                || (options.IncludeHydraulik && calcResult != null)
                || (options.IncludeKostenschaetzung && kostenAvailable);

            // Pruefung ob druckbar (muss auf UI-Thread, wegen MessageBox)
            if (!hasDossierBaseSection && !(options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0))
            {
                _sp.Dialogs.Info(
                    "Die ausgewaehlte Kombination enthaelt keine druckbaren Inhalte.",
                    "Dossier");
                return;
            }

            // PDF-Erzeugung auf Background-Thread (verhindert UI-Freeze)
            // Alle CPU-intensiven Operationen: Build, Merge, WriteAllBytes
            var localHasDossierBase = hasDossierBaseSection;
            await Task.Run(() =>
            {
                var originalsAlreadyMerged = false;
                byte[] pdf;
                if (localHasDossierBase)
                {
                    pdf = Application.Reports.HaltungsDossierPdfBuilder.Build(
                        _shell.Project, record, schachtVon, schachtBis, calcResult, projectFolder, options);
                }
                else
                {
                    pdf = Infrastructure.Media.PdfMergeHelper.MergeOriginals(originalPdfPaths);
                    if (pdf.Length == 0)
                        throw new InvalidOperationException("Die Original-Protokolle konnten nicht zusammengefuehrt werden.");
                    originalsAlreadyMerged = true;
                }

                // Original-PDFs anhaengen
                if (!originalsAlreadyMerged && options.IncludeOriginalProtokolle && originalPdfPaths.Count > 0)
                    pdf = Infrastructure.Media.PdfMergeHelper.MergeWithOriginals(pdf, originalPdfPaths);

                File.WriteAllBytes(output, pdf);
            });

            _sp.Dialogs.Info($"Dossier wurde erstellt:\n{output}", "Dossier");
        }
        catch (Exception ex)
        {
            _sp.Dialogs.Error($"Dossier konnte nicht erstellt werden:\n{ex.Message}", "Dossier");
        }
    }

    private void OpenOriginalPdf(HaltungRecord? record)
    {
        if (record is null)
            return;

        var projectFolder = _shell.GetProjectFolder() ?? "";
        var paths = DataPageProtocolPathResolver.ResolveOriginalPdfPaths(record, projectFolder);

        if (paths.Count == 0)
        {
            var name = record.GetFieldValue("Haltungsname") ?? "(unbekannt)";
            _sp.Dialogs.Info(
                $"Kein PDF gefunden fuer Haltung '{name}'.\n\nPruefen Sie, ob das Original-PDF im Projektordner liegt.",
                "Haltungsprotokoll (PDF)");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(paths[0]) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _sp.Dialogs.Warn($"PDF konnte nicht geoeffnet werden:\n{ex.Message}",
                "Fehler");
        }
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
        => DataPageProtocolPathResolver.ResolveExistingPath(raw, _sp.Settings.LastProjectPath);

    private void ShowSaveStatus(string? text)
    {
        SaveStatus = string.IsNullOrWhiteSpace(text) ? "Gespeichert" : text;
        IsSaveStatusVisible = true;
        _saveBannerTimer.Stop();
        _saveBannerTimer.Start();
    }

    private void UpdateLearningInfo(int? similarCases = null, decimal? estimatedCost = null)
    {
        var stats = _measureRecommendationService.GetStats();
        if (stats.TotalSamples <= 0)
        {
            LearningInfo = "Lernbasis: 0 Faelle";
            UpdateLearningTrafficLight(0);
            IsLearningInfoVisible = true;
            return;
        }

        var suffix = string.Empty;
        if (similarCases is not null && similarCases.Value > 0)
        {
            suffix = estimatedCost is null
                ? $" / letzte Schaetzung aus {similarCases.Value} aehnlichen Haltungen"
                : $" / letzte Kostenschaetzung {estimatedCost.Value:0.00} aus {similarCases.Value} aehnlichen Haltungen";
        }

        var modelText = stats.TrainedModelAvailable
            ? $" / KI-Modell aktiv ({stats.TrainedModelSamples ?? 0} Faelle)"
            : $" / KI-Modell ab {MinimumSamplesForModelTraining} Faellen";

        LearningInfo = $"Lernbasis: {stats.TotalSamples} Faelle{suffix}{modelText}";
        UpdateLearningTrafficLight(stats.TotalSamples);
        IsLearningInfoVisible = true;
    }

    private void UpdateLearningTrafficLight(int totalSamples)
    {
        if (totalSamples >= StrongModelThreshold)
        {
            LearningTrafficLightColor = "#2E7D32";
            LearningTrafficLightText = "Gruen";
            return;
        }

        if (totalSamples >= MinimumSamplesForModelTraining)
        {
            LearningTrafficLightColor = "#F9A825";
            LearningTrafficLightText = "Gelb";
            return;
        }

        LearningTrafficLightColor = "#C62828";
        LearningTrafficLightText = "Rot";
    }

    /// <summary>
    /// Lädt die CaseIds aus dem Training Center und normalisiert sie zu Haltungsnamen.
    /// </summary>
    private async Task LoadTrainedHaltungenAsync()
    {
        try
        {
            var store = new TrainingCenterStore();
            var state = await store.LoadAsync();
            TrainedHaltungen.Clear();
            foreach (var tc in state.Cases)
            {
                var name = NormalizeTrainingCaseId(tc.CaseId);
                if (!string.IsNullOrWhiteSpace(name))
                    TrainedHaltungen.Add(name);
            }
        }
        catch
        {
            // Training-Daten nicht verfügbar – kein Fehler
        }
    }

    /// <summary>
    /// Normalisiert eine Training-CaseId zu einem Haltungsnamen.
    /// Entfernt Datums-Prefixe wie "20250602_" und Knoten-Prefixe wie "07.", "10.".
    /// </summary>
    private static string NormalizeTrainingCaseId(string caseId)
    {
        var v = (caseId ?? "").Trim();
        // Datums-Prefix entfernen (z.B. "20250602_06.24341-35625" → "06.24341-35625")
        v = Regex.Replace(v, @"^\d{8}_", "");
        return v;
    }

    /// <summary>
    /// Prüft ob eine Haltung im Training Center erfasst ist.
    /// </summary>
    public bool IsTrainedCase(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname) || TrainedHaltungen.Count == 0)
            return false;
        // Exakter Match
        if (TrainedHaltungen.Contains(haltungsname))
            return true;
        // Ohne Knoten-Prefixe vergleichen (z.B. "07.1028055" → "1028055")
        var stripped = StripNodePrefixes(haltungsname);
        foreach (var trained in TrainedHaltungen)
        {
            if (string.Equals(StripNodePrefixes(trained), stripped, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static readonly Regex NodePrefixRx = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    private static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRx.Replace(holdingKey, "");
        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        return $"{NodePrefixRx.Replace(left, "")}-{NodePrefixRx.Replace(right, "")}";
    }

    public void EnsureOptionForField(string fieldName, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (fieldName == "Sanieren_JaNein")
            AddOptionIfMissing(SanierenOptions, text);
        else if (fieldName == "Eigentuemer")
            return;
        else if (fieldName == "Pruefungsresultat")
            AddOptionIfMissing(PruefungsresultatOptions, text);
        else if (fieldName == "Referenzpruefung")
            AddOptionIfMissing(ReferenzpruefungOptions, text);
        else if (fieldName == "Empfohlene_Sanierungsmassnahmen")
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, text);
    }

    private void AddOptionIfMissing(ObservableCollection<string> options, string value)
    {
        if (!AddOptionIfMissingCore(options, value))
            return;
        SaveDropdownOptions();
    }

    private static bool AddOptionIfMissingCore(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return false;
        if (options.Any(x => x.Equals(text, StringComparison.OrdinalIgnoreCase)))
            return false;
        options.Insert(0, text);
        return true;
    }

    /// <summary>
    /// Seeds measure template names from Offerten (MeasureTemplateStore) into the dropdown.
    /// Ensures all known template names are available for selection.
    /// </summary>
    private void SeedMeasureTemplateNames()
    {
        try
        {
            var store = new MeasureTemplateStore();
            var catalog = store.LoadMerged(_sp.Settings.LastProjectPath);
            foreach (var measure in catalog.Measures)
            {
                if (measure.Disabled)
                    continue;
                var name = measure.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (EmpfohleneSanierungsmassnahmenOptions.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                EmpfohleneSanierungsmassnahmenOptions.Add(name);
            }
        }
        catch
        {
            // Non-critical: template seeding failure should not block startup
        }
    }

    private void EditSanierenOptions()
    {
        var vm = new OptionsEditorViewModel(SanierenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            SanierenOptions.Clear();
            foreach (var item in vm.Items)
                SanierenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewSanierenOptions()
    {
        var items = string.Join("\n", SanierenOptions);
        _sp.Dialogs.Info(items, "Sanieren-Liste");
    }

    private void ResetSanierenOptions()
    {
        SanierenOptions.Clear();
        foreach (var item in new[] { "Nein", "Ja" })
            SanierenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddSanierenOption(object? value)
        => AddOptionIfMissing(SanierenOptions, ExtractText(value));

    private void RemoveSanierenOption(object? value)
        => RemoveOptionFromList(SanierenOptions, ExtractText(value));

    private void EditEigentuemerOptions()
    {
        var vm = new OptionsEditorViewModel(EigentuemerOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            EigentuemerOptions.Clear();
            foreach (var item in vm.Items)
                EigentuemerOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEigentuemerOptions()
    {
        var items = string.Join("\n", EigentuemerOptions);
        _sp.Dialogs.Info(items, "Eigentuemer-Liste");
    }

    private void ResetEigentuemerOptions()
    {
        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEigentuemerOption(object? value)
        => AddOptionIfMissing(EigentuemerOptions, ExtractText(value));

    private void RemoveEigentuemerOption(object? value)
        => RemoveOptionFromList(EigentuemerOptions, ExtractText(value));

    private void EditPruefungsresultatOptions()
    {
        var vm = new OptionsEditorViewModel(PruefungsresultatOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            PruefungsresultatOptions.Clear();
            foreach (var item in vm.Items)
                PruefungsresultatOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewPruefungsresultatOptions()
    {
        var items = string.Join("\n", PruefungsresultatOptions);
        _sp.Dialogs.Info(items, "Pruefungsresultat-Liste");
    }

    private void ResetPruefungsresultatOptions()
    {
        PruefungsresultatOptions.Clear();
        foreach (var item in new[]
                 {
                     "Pruefung bestanden",
                     "Pruefung knapp nicht bestanden",
                     "Pruefung nicht bestanden (grob undicht)",
                     "Keine"
                 })
            PruefungsresultatOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddPruefungsresultatOption(object? value)
        => AddOptionIfMissing(PruefungsresultatOptions, ExtractText(value));

    private void RemovePruefungsresultatOption(object? value)
        => RemoveOptionFromList(PruefungsresultatOptions, ExtractText(value));

    private void EditReferenzpruefungOptions()
    {
        var vm = new OptionsEditorViewModel(ReferenzpruefungOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            ReferenzpruefungOptions.Clear();
            foreach (var item in vm.Items)
                ReferenzpruefungOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewReferenzpruefungOptions()
    {
        var items = string.Join("\n", ReferenzpruefungOptions);
        _sp.Dialogs.Info(items, "Referenzpruefung-Liste");
    }

    private void ResetReferenzpruefungOptions()
    {
        ReferenzpruefungOptions.Clear();
        foreach (var item in new[] { "Ja", "Nein" })
            ReferenzpruefungOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddReferenzpruefungOption(object? value)
        => AddOptionIfMissing(ReferenzpruefungOptions, ExtractText(value));

    private void RemoveReferenzpruefungOption(object? value)
        => RemoveOptionFromList(ReferenzpruefungOptions, ExtractText(value));

    private void EditEmpfohleneSanierungsmassnahmenOptions()
    {
        var vm = new OptionsEditorViewModel(EmpfohleneSanierungsmassnahmenOptions);
        var dlg = new OptionsEditorWindow(vm);
        if (dlg.ShowDialog() == true)
        {
            EmpfohleneSanierungsmassnahmenOptions.Clear();
            foreach (var item in vm.Items)
                EmpfohleneSanierungsmassnahmenOptions.Add(item);
            SaveDropdownOptions();
        }
    }

    private void PreviewEmpfohleneSanierungsmassnahmenOptions()
    {
        var items = string.Join("\n", EmpfohleneSanierungsmassnahmenOptions);
        _sp.Dialogs.Info(items, "Sanierungsmassnahmen-Liste");
    }

    private void ResetEmpfohleneSanierungsmassnahmenOptions()
    {
        EmpfohleneSanierungsmassnahmenOptions.Clear();
        foreach (var item in new[] { "" })
            EmpfohleneSanierungsmassnahmenOptions.Add(item);
        SaveDropdownOptions();
    }

    private void AddEmpfohleneSanierungsmassnahmenOption(object? value)
        => AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private void RemoveEmpfohleneSanierungsmassnahmenOption(object? value)
        => RemoveOptionFromList(EmpfohleneSanierungsmassnahmenOptions, ExtractText(value));

    private static string ExtractText(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is System.Windows.Controls.ComboBox combo)
            return combo.Text ?? string.Empty;
        return value.ToString() ?? string.Empty;
    }

    private void RemoveOptionFromList(ObservableCollection<string> options, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return;
        var existing = options.FirstOrDefault(x => x.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return;
        options.Remove(existing);
        SaveDropdownOptions();
    }

    private void SaveDropdownOptions()
    {
        EnforceEigentuemerOptionsExact();
        SyncDropdownOptionsFromRecords();
        DropdownOptionsStore.SaveSanierenOptions(SanierenOptions);
        DropdownOptionsStore.SaveEigentuemerOptions(EigentuemerOptions);
        DropdownOptionsStore.SavePruefungsresultatOptions(PruefungsresultatOptions);
        DropdownOptionsStore.SaveReferenzpruefungOptions(ReferenzpruefungOptions);
        DropdownOptionsStore.SaveEmpfohleneSanierungsmassnahmenOptions(EmpfohleneSanierungsmassnahmenOptions);
    }

    private void SyncDropdownOptionsFromRecords()
    {
        foreach (var record in Records)
        {
            AddOptionIfMissingCore(SanierenOptions, record.GetFieldValue("Sanieren_JaNein"));
            AddOptionIfMissingCore(PruefungsresultatOptions, record.GetFieldValue("Pruefungsresultat"));
            AddOptionIfMissingCore(ReferenzpruefungOptions, record.GetFieldValue("Referenzpruefung"));

            var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
            foreach (var entry in recommended)
                AddOptionIfMissingCore(EmpfohleneSanierungsmassnahmenOptions, entry);
        }
    }

    private void EnforceEigentuemerOptionsExact()
    {
        var same = EigentuemerOptions.Count == FixedEigentuemerOptions.Length;
        if (same)
        {
            for (var i = 0; i < FixedEigentuemerOptions.Length; i++)
            {
                if (!string.Equals(EigentuemerOptions[i], FixedEigentuemerOptions[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
        }

        if (same)
            return;

        EigentuemerOptions.Clear();
        foreach (var item in FixedEigentuemerOptions)
            EigentuemerOptions.Add(item);
    }

    private static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(DataPageSanierungCostMapper.NormalizeRecommendationEntry)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyCostsToRecord(HaltungRecord record, HoldingCost cost, bool learn = true, bool includeCosts = true)
    {
        DataPageSanierungCostMapper.ApplyCosts(record, cost, includeCosts);

        // Force a replace notification on the collection so dictionary-backed
        // grid cells refresh immediately without extra user clicks.
        RefreshRecordInGrid(record);

        if (learn)
        {
            var learnedNow = _measureRecommendationService.Learn(record);
            if (learnedNow)
                _measureRecommendationService.TrainModel(MinimumSamplesForModelTraining);
            UpdateLearningInfo();
        }

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
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

    /// <summary>
    /// Filter predicate for the DataGrid's CollectionView.
    /// Matches if the Haltungsname contains the search term (either side of the pair).
    /// </summary>
    public bool MatchesSearch(HaltungRecord record)
        => DataPageSearchMatcher.Matches(record, SearchText);

    /// <summary>
    /// Updates the search result info text.
    /// </summary>
    public void UpdateSearchResultInfo(int visibleCount)
        => SearchResultInfo = DataPageSearchMatcher.BuildResultInfo(SearchText, visibleCount, Records.Count);

    private void PersistDataPageBasicUiSettings()
    {
        var layout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.GridZoom = GridZoom;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _sp.Settings.DataPageLayout = layout;
        _sp.Settings.Save();
    }
}
