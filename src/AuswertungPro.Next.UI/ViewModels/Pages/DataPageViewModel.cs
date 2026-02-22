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
using System.Net.Http;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Sanierung;
using AuswertungPro.Next.UI.Ai.Sanierung.Dto;

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
    private readonly MeasureRecommendationService _measureRecommendationService;

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
    public IRelayCommand<HaltungRecord?> OpenCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> RestoreCostsCommand { get; }
    public IRelayCommand<HaltungRecord?> SuggestMeasuresCommand { get; }
    public IRelayCommand<HaltungRecord?> OptimizeSanierungKiCommand { get; }
    public IRelayCommand ShowModelStatusCommand { get; }

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
    [ObservableProperty] private double _gridMinRowHeight = 38d;
    [ObservableProperty] private bool _isColumnReorderEnabled;
    public IRelayCommand ClearSearchCommand { get; }
    public bool IsProjectReady => _shell.IsProjectReady;
    public bool IsDataGridReadOnly => !_shell.IsProjectReady;

    public DataPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _measureRecommendationService = new MeasureRecommendationService();
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

        var uiLayout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        GridMinRowHeight = uiLayout.GridMinRowHeight is >= 24d and <= 120d
            ? uiLayout.GridMinRowHeight
            : 38d;
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
        OpenCostsCommand = new RelayCommand<HaltungRecord?>(OpenCosts, CanOpenCosts);
        RestoreCostsCommand = new RelayCommand<HaltungRecord?>(RestoreCosts, CanRestoreCosts);
        SuggestMeasuresCommand = new RelayCommand<HaltungRecord?>(SuggestMeasures, CanSuggestMeasures);
        OptimizeSanierungKiCommand = new RelayCommand<HaltungRecord?>(OpenSanierungOptimizationWindow, CanOpenCosts);
        ShowModelStatusCommand = new RelayCommand(ShowModelStatus);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        PropertyChanged += DataPageViewModel_PropertyChanged;
        UpdateLearningInfo();
    }

    partial void OnGridMinRowHeightChanged(double value)
    {
        var clamped = Math.Clamp(value, 24d, 120d);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
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
            var entries = BuildEntriesFromFindings(record.VsaFindings);
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
            SelectedProtocolEntries.Add(entry);
    }

    private static IReadOnlyList<ProtocolEntry> BuildEntriesFromFindings(IEnumerable<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>();
        foreach (var f in findings)
        {
            var mStart = f.MeterStart ?? f.SchadenlageAnfang;
            var mEnd = f.MeterEnd ?? f.SchadenlageEnde;
            var time = ParseMpegTime(f.MPEG) ?? (f.Timestamp?.TimeOfDay);

            var entry = new ProtocolEntry
            {
                Code = f.KanalSchadencode?.Trim() ?? string.Empty,
                Beschreibung = f.Raw?.Trim() ?? string.Empty,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = time,
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    private static readonly Regex MeterRegex = new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

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

            var window = new PlayerWindow(path, options)
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
            MessageBox.Show(msg, "Video", MessageBoxButton.OK, MessageBoxImage.Error);
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

        if (Selected?.Id == record.Id)
            RefreshSelectedProtocolEntries();
    }

    private void OpenVideoAiPipeline(HaltungRecord? record)
    {
        if (record is null) return;

        var videoPath = EnsureVideoPath(record);
        if (string.IsNullOrWhiteSpace(videoPath)) return;

        var allowedCodes = _sp.CodeCatalog.AllowedCodes();
        if (allowedCodes is null || allowedCodes.Count == 0)
        {
            MessageBox.Show("VSA-Code-Katalog ist leer oder nicht geladen.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cfg = AiRuntimeConfig.Load();
        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert (AUSWERTUNGPRO_AI_ENABLED=0).", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var plausibility = new NoopAiSuggestionPlausibilityService();
        var pipeline = new VideoAnalysisPipelineService(cfg, plausibility, http);

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


    private static ProtocolEntry CloneProtocolEntry(ProtocolEntry source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProtocolEntry>(json) ?? new ProtocolEntry();
    }

    private string? EnsureProtocolPath(HaltungRecord record)
    {
        var holdingTokens = BuildHoldingTokens(record);

        var resolvedLink = ResolveExistingPath(record.GetFieldValue("Link"));
        var fromLink = TryResolveProtocolFromLink(resolvedLink, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromLink))
            return fromLink;

        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        var fromInitial = TryFindProtocolFromRoot(initial, holdingTokens);
        if (!string.IsNullOrWhiteSpace(fromInitial))
            return fromInitial;

        if (!string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
        {
            var projectDir = Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                var fromHoldings = TryFindProtocolFromRoot(Path.Combine(projectDir, "Haltungen"), holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromHoldings))
                    return fromHoldings;

                var fromStored = TryFindProtocolFromStoredPdfFiles(projectDir, holdingTokens);
                if (!string.IsNullOrWhiteSpace(fromStored))
                    return fromStored;
            }
        }

        return null;
    }

    private string? TryResolveProtocolFromLink(string? resolvedLink, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(resolvedLink))
            return null;

        if (string.Equals(Path.GetExtension(resolvedLink), ".pdf", StringComparison.OrdinalIgnoreCase))
            return resolvedLink;

        var folder = Path.GetDirectoryName(resolvedLink);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var inSameFolder = TryFindPdfInDirectory(folder, holdingTokens, SearchOption.TopDirectoryOnly);
        if (!string.IsNullOrWhiteSpace(inSameFolder))
            return inSameFolder;

        try
        {
            var parent = Directory.GetParent(folder);
            if (parent is not null && string.Equals(parent.Name, "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
            {
                var gemeindeRoot = parent.Parent?.FullName;
                if (!string.IsNullOrWhiteSpace(gemeindeRoot))
                {
                    var inGemeinde = TryFindProtocolFromRoot(gemeindeRoot, holdingTokens);
                    if (!string.IsNullOrWhiteSpace(inGemeinde))
                        return inGemeinde;
                }
            }
        }
        catch
        {
            // Continue with other lookup strategies.
        }

        return null;
    }

    private string? TryFindProtocolFromRoot(string? rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return null;

        var holdingDir = TryFindHoldingDirectory(rootDir, holdingTokens);
        if (!string.IsNullOrWhiteSpace(holdingDir))
        {
            var inHolding = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.TopDirectoryOnly);
            if (!string.IsNullOrWhiteSpace(inHolding))
                return inHolding;

            var inHoldingRecursive = TryFindPdfInDirectory(holdingDir, holdingTokens, SearchOption.AllDirectories);
            if (!string.IsNullOrWhiteSpace(inHoldingRecursive))
                return inHoldingRecursive;
        }

        return TryFindPdfInDirectory(rootDir, holdingTokens, SearchOption.AllDirectories);
    }

    private string? TryFindProtocolFromStoredPdfFiles(string projectDir, IReadOnlyList<string> holdingTokens)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var candidates = new List<string>();
        foreach (var stored in ParseStoredPathList(raw))
        {
            var resolved = TryResolveStoredPath(projectDir, stored);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;
            if (!string.Equals(Path.GetExtension(resolved), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;
            candidates.Add(resolved);
        }

        return PickBestPdfCandidate(candidates, holdingTokens);
    }

    private static string? TryFindHoldingDirectory(string rootDir, IReadOnlyList<string> holdingTokens)
    {
        if (holdingTokens.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var direct = Path.Combine(rootDir, token);
            if (Directory.Exists(direct))
                return direct;
        }

        foreach (var sub in SafeEnumerateDirectories(rootDir))
        {
            if (string.Equals(Path.GetFileName(sub), "__UNMATCHED", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var token in holdingTokens)
            {
                var candidate = Path.Combine(sub, token);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string? TryFindPdfInDirectory(string directory, IReadOnlyList<string> holdingTokens, SearchOption searchOption)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var files = SafeEnumerateFiles(directory, "*.pdf", searchOption);
        return PickBestPdfCandidate(files, holdingTokens);
    }

    private static string? PickBestPdfCandidate(IEnumerable<string> candidates, IReadOnlyList<string> holdingTokens)
    {
        var list = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
            return null;

        foreach (var token in holdingTokens)
        {
            var expectedSuffix = "_" + token + ".pdf";
            var exact = list
                .Where(path => Path.GetFileName(path).EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (exact.Count > 0)
                return exact[0];
        }

        return list
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
    {
        var holdingRaw = (record.GetFieldValue("Haltungsname") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(holdingRaw))
            return Array.Empty<string>();

        var sanitized = SanitizePathSegment(holdingRaw);
        return new[] { sanitized, holdingRaw }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var cleaned = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }

    private static IReadOnlyList<string> ParseStoredPathList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed is null)
                return Array.Empty<string>();

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }
        catch
        {
            return raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }
    }

    private static string? TryResolveStoredPath(string projectDir, string rawPath)
    {
        var path = (rawPath ?? string.Empty).Trim();
        if (path.Length == 0)
            return null;

        try
        {
            var full = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(projectDir, path));
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
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
            "Video (*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.wmv;*.mkv)|*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.wmv;*.mkv|Alle Dateien|*.*",
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
            MessageBox.Show("Haltungsname fehlt in der Zeile.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var projectPath = _sp.Settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            MessageBox.Show("Projekt bitte zuerst speichern/oeffnen, um Kosten wiederherzustellen.", "Kosten/Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var store = new ProjectCostStoreRepository().Load(projectPath);
        if (!store.ByHolding.TryGetValue(holding, out var cost))
        {
            var dir = Path.GetDirectoryName(projectPath);
            var storePath = string.IsNullOrWhiteSpace(dir) ? "" : ProjectCostStoreRepository.GetStorePath(dir);
            MessageBox.Show($"Keine gespeicherten Kosten/Massnahmen gefunden fuer:\n{holding}\n\nDatei:\n{storePath}",
                "Kosten/Massnahmen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ApplyCostsToRecord(record, cost, learn: false);
        _shell.SetStatus($"Kosten/Massnahmen wiederhergestellt: {holding}");
    }

    private void OpenCosts(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            MessageBox.Show("Haltungsname fehlt in der Zeile.", "Massnahmen",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var recommended = ParseRecommendedTemplates(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        var vm = new CostCalculatorViewModel(
            holding,
            null,
            recommended,
            _sp.Settings.LastProjectPath,
            cost => ApplyCostsToRecord(record, cost),
            haltungRecord: record,
            projectRecords: Records);
        var win = new CostCalculatorWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        win.ShowDialog();
    }

    private void SuggestMeasures(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var recommendation = _measureRecommendationService.Recommend(record, maxSuggestions: 5);
        if (recommendation.Measures.Count == 0)
        {
            MessageBox.Show(
                "Noch keine Vorschlaege verfuegbar. Bitte zuerst einige Haltungen mit Massnahmen bewerten.",
                "Massnahmen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var value = string.Join(Environment.NewLine, recommendation.Measures);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", value, FieldSource.Unknown, userEdited: false);
        foreach (var suggestion in recommendation.Measures)
            AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, suggestion);

        if (recommendation.EstimatedTotalCost is not null)
            record.SetFieldValue("Kosten", recommendation.EstimatedTotalCost.Value.ToString("0.00", CultureInfo.InvariantCulture), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerM is not null)
            record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(recommendation.RenovierungInlinerM.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.RenovierungInlinerStk is not null)
            record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(recommendation.RenovierungInlinerStk.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.AnschluesseVerpressen is not null)
            record.SetFieldValue("Anschluesse_verpressen", FormatInt(recommendation.AnschluesseVerpressen.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturManschette is not null)
            record.SetFieldValue("Reparatur_Manschette", FormatInt(recommendation.ReparaturManschette.Value), FieldSource.Unknown, userEdited: false);
        if (recommendation.ReparaturKurzliner is not null)
            record.SetFieldValue("Reparatur_Kurzliner", FormatInt(recommendation.ReparaturKurzliner.Value), FieldSource.Unknown, userEdited: false);

        _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
        _shell.Project.Dirty = true;
        var sourceText = recommendation.UsedTrainedModel ? "KI-Modell" : "Lernlogik";
        _shell.SetStatus(recommendation.EstimatedTotalCost is null
            ? $"Massnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Massnahmenvorschlag mit Kostenschaetzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
        UpdateLearningInfo(recommendation.SimilarCasesCount, recommendation.EstimatedTotalCost);
    }

    private void OpenSanierungOptimizationWindow(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null) return;

        var holding = (record.GetFieldValue("Haltungsname") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(holding))
        {
            MessageBox.Show("Haltungsname fehlt in der Zeile.", "KI Sanierung",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cfg = AiRuntimeConfig.Load();
        if (!cfg.Enabled)
        {
            MessageBox.Show(
                "KI ist deaktiviert (AUSWERTUNGPRO_AI_ENABLED=0).\n" +
                "Bitte Umgebungsvariable setzen und App neu starten.",
                "KI Sanierung", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Rule recommendation as starting point
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

        var aiService = new AiSanierungOptimizationService(cfg);
        var vm  = new AuswertungPro.Next.UI.ViewModels.Windows.SanierungOptimizationViewModel(record, aiService, ruleDto);

        vm.TransferredToPrimary += _ =>
        {
            _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
            _shell.Project.Dirty         = true;
            RefreshRecordInGrid(record);
            ScheduleAutoSave();
            _shell.SetStatus($"KI-Sanierungsvorschlag übertragen: {holding}");
        };

        var win = new AuswertungPro.Next.UI.Views.Windows.SanierungOptimizationWindow(vm)
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

        MessageBox.Show(message, "KI-Modell Status", MessageBoxButton.OK, MessageBoxImage.Information);
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

        MessageBox.Show(resManual.Message, "Video", MessageBoxButton.OK, MessageBoxImage.Information);

        var manual = _sp.Dialogs.OpenFile(
            "Video auswaehlen",
            "Video (*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.wmv;*.mkv)|*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.wmv;*.mkv|Alle Dateien|*.*",
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

    private string? ResolveExistingPath(string? raw)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath))
        {
            var baseDir = Path.GetDirectoryName(_sp.Settings.LastProjectPath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                if (File.Exists(combined))
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
        System.Windows.MessageBox.Show(items, "Sanieren-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
        System.Windows.MessageBox.Show(items, "Eigentuemer-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
        System.Windows.MessageBox.Show(items, "Pruefungsresultat-Liste", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
        System.Windows.MessageBox.Show(items, "Referenzpruefung-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
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
        System.Windows.MessageBox.Show(items, "Sanierungsmassnahmen-Liste", System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
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
            .Select(NormalizeRecommendationEntry)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyCostsToRecord(HaltungRecord record, HoldingCost cost, bool learn = true, bool includeCosts = true)
    {
        if (includeCosts)
        {
            // Transfer net amount to table field "Kosten".
            var netTotal = ResolveNetTotal(cost);
            var totalText = netTotal.ToString("0.00", CultureInfo.InvariantCulture);
            record.SetFieldValue("Kosten", totalText, FieldSource.Manual, userEdited: true);
        }

        var massnahmenText = BuildMeasuresText(cost);
        record.SetFieldValue("Empfohlene_Sanierungsmassnahmen", massnahmenText, FieldSource.Manual, userEdited: true);

        var inlinerMeters = SumMeasureLengths(
            cost,
            "NADELFILZ",
            "GFK",
            "SCHLAUCHLINER_NADELFILZ",
            "SCHLAUCHLINER_NADELFILZ_OPENEND",
            "SCHLAUCHLINER_GFK");
        // Domain rule: if a liner is selected, count exactly 1 piece.
        var inlinerStk = HasSelectedLiner(cost) ? 1 : 0;
        var anschluesse = Math.Max(
            SumSelectedQty(cost, "ANSCHLUSS_EINBINDEN"),
            SumSelectedQty(cost, "ANSCHLUSS_AUFFRAESEN"));
        // LEM is not a repair manschette and must not fill Reparatur_Manschette.
        var manschette = SumSelectedQty(cost, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
        var kurzliner = SumSelectedQty(cost, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

        record.SetFieldValue("Renovierung_Inliner_m", FormatDecimal(inlinerMeters), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Renovierung_Inliner_Stk", FormatInt(inlinerStk), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Anschluesse_verpressen", FormatInt(anschluesse), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Manschette", FormatInt(manschette), FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Reparatur_Kurzliner", FormatInt(kurzliner), FieldSource.Manual, userEdited: true);

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

    private static decimal ResolveNetTotal(HoldingCost cost)
    {
        if (cost.Total > 0m)
            return cost.Total;

        if (cost.TotalInclMwst > 0m && cost.MwstRate > 0m)
            return Math.Round(cost.TotalInclMwst / (1m + cost.MwstRate), 2, MidpointRounding.AwayFromZero);

        return cost.TotalInclMwst;
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

    private static string BuildMeasuresText(HoldingCost cost)
    {
        // Prefer transfer-marked rows for "Empfohlene Massnahmen".
        var markedPositions = cost.Measures
            .SelectMany(m => m.Lines)
            .Where(l => l.Selected && l.TransferMarked)
            .Select(l => FormatRecommendationBullet(l.Text))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markedPositions.Count > 0)
            return string.Join(Environment.NewLine, markedPositions);

        return "";
    }

    private static string FormatRecommendationBullet(string? value)
    {
        var normalized = NormalizeRecommendationEntry(value);
        return normalized.Length == 0 ? string.Empty : "- " + normalized;
    }

    private static string NormalizeRecommendationEntry(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    private static decimal SumMeasureLengths(HoldingCost cost, params string[] measureIds)
    {
        var sum = 0m;
        foreach (var measure in cost.Measures)
        {
            if (!measureIds.Any(id => MatchesIdentifier(measure.MeasureId, id)))
                continue;
            if (measure.LengthMeters is not null)
            {
                sum += measure.LengthMeters.Value;
                continue;
            }

            var fallback = measure.Lines
                .Where(l => l.Selected && string.Equals(l.Unit, "m", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Qty)
                .DefaultIfEmpty(0m)
                .Max();
            sum += fallback;
        }
        return sum;
    }

    private static bool HasSelectedLiner(HoldingCost cost)
    {
        foreach (var measure in cost.Measures)
        {
            var selectedLines = measure.Lines.Where(l => l.Selected).ToList();
            if (selectedLines.Count == 0)
                continue;

            if (selectedLines.Any(IsLinerLine))
                return true;

            // Fallback for legacy payloads where only measure id is reliable.
            if (IsLinerIdentifier(measure.MeasureId))
                return true;
        }

        return false;
    }

    private static bool IsLinerLine(CostLine line)
    {
        if (line is null)
            return false;

        if (IsLinerIdentifier(line.ItemKey))
            return true;

        var text = line.Text ?? "";
        return text.Contains("schlauchliner", StringComparison.OrdinalIgnoreCase)
            || text.Contains("nadelfilz", StringComparison.OrdinalIgnoreCase)
            || text.Contains("gfk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLinerIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_OPENEND")
            || MatchesIdentifier(value, "SCHLAUCHLINER_GFK")
            || MatchesIdentifier(value, "NADELFILZ_LINER_BIS_5M")
            || MatchesIdentifier(value, "SCHLAUCHLINER_NADELFILZ_BIS_5M")
            || MatchesIdentifier(value, "NADELFILZ")
            || MatchesIdentifier(value, "GFK");
    }

    private static bool MatchesIdentifier(string? value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var candidate = value.Trim();
        var token = pattern.Trim();
        if (string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy patterns like "NADELFILZ" or "GFK" should match newer IDs.
        if (token.IndexOf('_') >= 0 || token.IndexOf('-') >= 0)
            return false;

        return candidate.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static int SumSelectedQty(HoldingCost cost, params string[] itemKeys)
    {
        var total = 0m;
        foreach (var measure in cost.Measures)
        {
            foreach (var line in measure.Lines)
            {
                if (!line.Selected)
                    continue;
                if (!itemKeys.Any(key => string.Equals(line.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                    continue;
                total += line.Qty;
            }
        }
        return (int)Math.Round(total, 0, MidpointRounding.AwayFromZero);
    }

    private static string FormatDecimal(decimal value)
        => value <= 0m ? "" : value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Filter predicate for the DataGrid's CollectionView. 
    /// Matches if the Haltungsname contains the search term (either side of the pair).
    /// </summary>
    public bool MatchesSearch(HaltungRecord record)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        var haltung = record.GetFieldValue("Haltungsname") ?? "";
        // Match against full haltungsname or individual shaft numbers
        if (haltung.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        // Also check Strasse
        var strasse = record.GetFieldValue("Strasse") ?? "";
        if (strasse.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Updates the search result info text.
    /// </summary>
    public void UpdateSearchResultInfo(int visibleCount)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            SearchResultInfo = string.Empty;
        else
            SearchResultInfo = $"{visibleCount} von {Records.Count} Haltungen";
    }

    private static string FormatInt(int value)
        => value <= 0 ? "" : value.ToString(CultureInfo.InvariantCulture);

    private void PersistDataPageBasicUiSettings()
    {
        var layout = _sp.Settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.GridMinRowHeight = GridMinRowHeight;
        layout.IsColumnReorderEnabled = IsColumnReorderEnabled;
        _sp.Settings.DataPageLayout = layout;
        _sp.Settings.Save();
    }
}
