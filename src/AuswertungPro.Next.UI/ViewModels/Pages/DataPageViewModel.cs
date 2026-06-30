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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.DataPage;
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
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel : ObservableObject, IDisposable
{
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
    private readonly ServiceProvider _sp;
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _saveBannerTimer;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly IMeasureRecommendationService _measureRecommendationService;
    private readonly DataPageDropdownCommandSet _dropdownCommands;
    private readonly DataPageSelectedProtocolController _selectedProtocolController = new();
    private readonly DataPageProtocolDocumentController _protocolDocumentController = new();
    private readonly TrainingCaseIndex _trainingCaseIndex = new();
    private bool _disposed;

    internal ServiceProvider Services => _sp;

    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand EditSanierenOptionsCommand => _dropdownCommands.Sanieren.Edit;
    public IRelayCommand PreviewSanierenOptionsCommand => _dropdownCommands.Sanieren.Preview;
    public IRelayCommand ResetSanierenOptionsCommand => _dropdownCommands.Sanieren.Reset;
    public IRelayCommand<object?> AddSanierenOptionCommand => _dropdownCommands.Sanieren.Add;
    public IRelayCommand<object?> RemoveSanierenOptionCommand => _dropdownCommands.Sanieren.Remove;
    public IRelayCommand EditEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Edit;
    public IRelayCommand PreviewEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Preview;
    public IRelayCommand ResetEigentuemerOptionsCommand => _dropdownCommands.Eigentuemer.Reset;
    public IRelayCommand<object?> AddEigentuemerOptionCommand => _dropdownCommands.Eigentuemer.Add;
    public IRelayCommand<object?> RemoveEigentuemerOptionCommand => _dropdownCommands.Eigentuemer.Remove;
    public IRelayCommand EditPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Edit;
    public IRelayCommand PreviewPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Preview;
    public IRelayCommand ResetPruefungsresultatOptionsCommand => _dropdownCommands.Pruefungsresultat.Reset;
    public IRelayCommand<object?> AddPruefungsresultatOptionCommand => _dropdownCommands.Pruefungsresultat.Add;
    public IRelayCommand<object?> RemovePruefungsresultatOptionCommand => _dropdownCommands.Pruefungsresultat.Remove;
    public IRelayCommand EditReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Edit;
    public IRelayCommand PreviewReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Preview;
    public IRelayCommand ResetReferenzpruefungOptionsCommand => _dropdownCommands.Referenzpruefung.Reset;
    public IRelayCommand<object?> AddReferenzpruefungOptionCommand => _dropdownCommands.Referenzpruefung.Add;
    public IRelayCommand<object?> RemoveReferenzpruefungOptionCommand => _dropdownCommands.Referenzpruefung.Remove;
    public IRelayCommand EditEmpfohleneSanierungsmassnahmenOptionsCommand => _dropdownCommands.EmpfohleneSanierungsmassnahmen.Edit;
    public IRelayCommand PreviewEmpfohleneSanierungsmassnahmenOptionsCommand => _dropdownCommands.EmpfohleneSanierungsmassnahmen.Preview;
    public IRelayCommand ResetEmpfohleneSanierungsmassnahmenOptionsCommand => _dropdownCommands.EmpfohleneSanierungsmassnahmen.Reset;
    public IRelayCommand<object?> AddEmpfohleneSanierungsmassnahmenOptionCommand => _dropdownCommands.EmpfohleneSanierungsmassnahmen.Add;
    public IRelayCommand<object?> RemoveEmpfohleneSanierungsmassnahmenOptionCommand => _dropdownCommands.EmpfohleneSanierungsmassnahmen.Remove;
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
    public ObservableCollection<ProtocolEntry> SelectedProtocolEntries => _selectedProtocolController.Entries;

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
    public IReadOnlySet<string> TrainedHaltungen => _trainingCaseIndex.TrainedHaltungen;
    [ObservableProperty] private double _gridMinRowHeight = 38d;
    [ObservableProperty] private double _gridZoom = 1.0d;
    [ObservableProperty] private bool _isColumnReorderEnabled;
    public IRelayCommand ClearSearchCommand { get; }
    public bool IsProjectReady => _shell.IsProjectReady;
    public bool IsDataGridReadOnly => !_shell.IsProjectReady;

    public DataPageViewModel(ShellViewModel shell, ServiceProvider services)
    {
        _shell = shell;
        _sp = services;
        _measureRecommendationService = _sp.MeasureRecommendation;
        _saveBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveBannerTimer.Tick += (_, __) =>
        {
            _saveBannerTimer.Stop();
            IsSaveStatusVisible = false;
        };
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoSaveTimer.Tick += (_, __) => AutoSaveOnTimerTick();
        _shell.PropertyChanged += ShellPropertyChanged;

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
        _dropdownCommands = DataPageDropdownCommandFactory.Create(
            new DropdownCommandActions(
                EditSanierenOptions,
                PreviewSanierenOptions,
                ResetSanierenOptions,
                AddSanierenOption,
                RemoveSanierenOption),
            new DropdownCommandActions(
                EditEigentuemerOptions,
                PreviewEigentuemerOptions,
                ResetEigentuemerOptions,
                AddEigentuemerOption,
                RemoveEigentuemerOption),
            new DropdownCommandActions(
                EditPruefungsresultatOptions,
                PreviewPruefungsresultatOptions,
                ResetPruefungsresultatOptions,
                AddPruefungsresultatOption,
                RemovePruefungsresultatOption),
            new DropdownCommandActions(
                EditReferenzpruefungOptions,
                PreviewReferenzpruefungOptions,
                ResetReferenzpruefungOptions,
                AddReferenzpruefungOption,
                RemoveReferenzpruefungOption),
            new DropdownCommandActions(
                EditEmpfohleneSanierungsmassnahmenOptions,
                PreviewEmpfohleneSanierungsmassnahmenOptions,
                ResetEmpfohleneSanierungsmassnahmenOptions,
                AddEmpfohleneSanierungsmassnahmenOption,
                RemoveEmpfohleneSanierungsmassnahmenOption));
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
        SearchAndLinkMediaCommand = new RelayCommand(OpenMediaSearchWindow);
        OpenHydraulikCommand = new RelayCommand<HaltungRecord?>(OpenHydraulikPanel);
        PrintHydraulikCommand = new RelayCommand<HaltungRecord?>(PrintHydraulikPdf);
        PrintDossierCommand = new RelayCommand<HaltungRecord?>(PrintDossierPdf);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        PropertyChanged += DataPageViewModel_PropertyChanged;
        UpdateLearningInfo();
        LoadTrainedHaltungenAsync().SafeFireAndForget("TrainedHaltungen");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shell.PropertyChanged -= ShellPropertyChanged;
        PropertyChanged -= DataPageViewModel_PropertyChanged;
        _saveBannerTimer.Stop();
        _autoSaveTimer.Stop();
        LiveControl.LiveControlRetryBridge.Reset();
    }

    private void ShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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

    private void SyncSelectedProtocolFromFindings(HaltungRecord record)
        => _selectedProtocolController.SyncFromFindings(
            record,
            _sp.Protocols,
            ResolveCodeTitle,
            RefreshRecordInGrid,
            Selected?.Id == record.Id,
            _sp.CodeCatalog);

    private void RefreshSelectedProtocolEntries()
        => _selectedProtocolController.Refresh(Selected, _sp.CodeCatalog);

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

        var name = Selected.GetFieldValue("Haltungsname");
        var label = string.IsNullOrWhiteSpace(name) ? "diese Haltung" : $"die Haltung \"{name}\"";
        if (!_sp.Dialogs.Confirm($"Soll {label} wirklich geloescht werden?\n\nDie Zeile inkl. aller Daten wird entfernt.",
                "Haltung loeschen"))
            return;

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
            _measureRecommendationService.TrainModel(LearningReadinessPresenter.MinimumSamplesForTraining);
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
        DataPageAutoSaveController.Schedule(
            _sp.Settings.DataAutoSaveMode,
            markDirty: () => _shell.Project.Dirty = true,
            stopTimer: _autoSaveTimer.Stop,
            setInterval: interval =>
            {
                if (_autoSaveTimer.Interval != interval)
                    _autoSaveTimer.Interval = interval;
            },
            isTimerEnabled: () => _autoSaveTimer.IsEnabled,
            startTimer: _autoSaveTimer.Start,
            save: AutoSave);
    }

    private void AutoSaveOnTimerTick()
    {
        DataPageAutoSaveController.HandleTimerTick(
            _sp.Settings.DataAutoSaveMode,
            save: AutoSave,
            isProjectDirty: () => _shell.Project.Dirty,
            stopTimer: _autoSaveTimer.Stop);
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
            var options = PlayerWindowOptions.FromSettings(_sp.Settings);

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
            var logPath = DataPageVideoStartErrorLogWriter.TryWrite(ex, path);
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

        // Echte Haltungslaenge aus den Stammdaten fuer die Meter-Schaetzung
        // (sonst rechnet die Pipeline mit der 50m-Annahme)
        double? reachLengthM = null;
        var reachLengthRaw = record.GetFieldValue("Haltungslaenge_m")?.Replace(',', '.');
        if (double.TryParse(reachLengthRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var reachLength)
            && reachLength > 0)
        {
            reachLengthM = reachLength;
        }

        var request = new PipelineRequest(haltungId, videoPath, allowedCodes, ReachLengthM: reachLengthM);

        var win = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        var ok = win.ShowDialog() == true;

        if (ok && win.Result?.IsSuccess == true && win.Result.Document is not null)
        {
            // S4: Hat die Haltung manuell codierte Eintraege, vor dem Ersetzen rueckfragen.
            // Das bisherige Protokoll wird zwar in die Historie gesichert (wiederherstellbar),
            // die Anzeige (Current) aber durch die KI-Ergebnisse ersetzt.
            if (ProtocolReplacementService.HasManualCurrentEntries(record.Protocol)
                && !_sp.Dialogs.Confirm(
                    "Diese Haltung enthaelt manuell codierte Eintraege.\n\n" +
                    "Die KI-Reanalyse ersetzt das angezeigte Protokoll. Das bisherige Protokoll " +
                    "wird in die Historie verschoben (wiederherstellbar).\n\nFortfahren?",
                    "KI-Reanalyse"))
            {
                return;
            }

            record.Protocol = ProtocolReplacementService.PrepareReplacement(
                record.Protocol,
                win.Result.Document,
                user: "KI-Reanalyse",
                archiveComment: "Auto-Archiv vor KI-Reanalyse");

            _shell.MarkProjectDirty(record);

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
        _shell.SetStatus($"Kosten/Maßnahmen wiederhergestellt: {holding}");
    }

    private void OpenCosts(HaltungRecord? record)
    {
        OpenSanierungsMatrix(record);
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
            ? $"Maßnahmenvorschlag aus Schadenscodes gesetzt ({sourceText})"
            : $"Maßnahmenvorschlag mit Kostenschätzung gesetzt ({recommendation.EstimatedTotalCost.Value:0.00}, {sourceText})");
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

        _shell.SetStatus($"Maßnahmen: {filled} Haltungen befüllt, {skipped} übersprungen, {noSuggestion} ohne Vorschlag");
    }

    private void OpenSanierungOptimizationWindow(HaltungRecord? record)
    {
        OpenSanierungsmassnahmenWindow(record, InitialFocusMode.AiOptimization);
    }

    private void OpenSanierungsMatrix(HaltungRecord? record)
    {
        record ??= Selected;
        if (record is null)
            return;

        var holding = SanierungsMatrixNavigationTarget.FromRecord(record);
        if (string.IsNullOrWhiteSpace(holding))
        {
            _sp.Dialogs.Warn("Haltungsname fehlt in der Zeile.", "Sanierungs-Matrix");
            return;
        }

        Selected = record;
        _shell.NavigateToSanierungsMatrix(holding, singleHoldingMode: true, targetRecord: record);
        _shell.SetStatus($"Sanierungsmaßnahme geöffnet: {holding}");
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
                _shell.SetStatus($"KI-Sanierungsvorschlag übertragen: {holding}");
            };
        }

        var vm = new SanierungsmassnahmenViewModel(costCalcVm, optimizationVm, record, focus);
        var win = new SanierungsmassnahmenWindow(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        win.ShowDialog();
    }


    private string? EnsureVideoPath(HaltungRecord record)
    {
        var initial = !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoSourceFolder)
            ? _sp.Settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_sp.Settings.LastVideoFolder)
                ? _sp.Settings.LastVideoFolder
            : _sp.Settings.LastProjectPath is null
                ? null
                : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

        return DataPageVideoPathWorkflowController.Resolve(
            record,
            record.GetFieldValue("Link"),
            initial,
            ResolveExistingPath,
            Directory.Exists,
            DataPageVideoPathWorkflowController.ResolveWithVideoSearchTool,
            (title, initialFolder) => _sp.Dialogs.SelectFolder(title, initialFolder),
            folder =>
            {
                _sp.Settings.LastVideoSourceFolder = folder;
                _sp.Settings.LastVideoFolder = folder; // legacy compatibility
                _sp.Settings.Save();
            },
            (message, title) => _sp.Dialogs.Info(message, title),
            (title, filter, initialFolder) => _sp.Dialogs.OpenFile(title, filter, initialFolder),
            (path, userEdited) => SaveVideoLink(record, path, userEdited));
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

        var win = new MediaSearchWindow(Records.ToList(), initial, _sp);
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
            var dn = DataPageHydraulikReportCalculator.ParseDnMm(record.GetFieldValue("DN_mm"));
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
        => _protocolDocumentController.EnsureForPdf(record, _sp.Protocols, ResolveCodeTitle);

    private async void PrintHydraulikPdf(HaltungRecord? record)
    {
        if (record is null)
        {
            _sp.Dialogs.Info("Bitte zuerst eine Haltung auswaehlen.", "Hydraulik PDF");
            return;
        }

        var calc = DataPageHydraulikReportCalculator.BuildReportCalculation(
            record,
            _sp.Settings,
            saveSettings: _sp.Settings.Save);
        if (calc is null)
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
        var hydraulikAvailability = DataPageHydraulikReportCalculator.ReadAvailability(record);
        var dn = hydraulikAvailability.DnMm;
        var hydraulikAvailable = hydraulikAvailability.IsAvailable;

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
                calcResult = DataPageHydraulikReportCalculator.BuildReportCalculation(
                    record,
                    _sp.Settings,
                    dn!.Value,
                    saveSettings: _sp.Settings.Save);
            }

            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = dialog.SelectedOptions with
            {
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null,
                HoldingCost = dialog.SelectedOptions.IncludeKostenschaetzung ? holdingCost : null,
                OriginalPdfPaths = dialog.SelectedOptions.IncludeOriginalProtokolle ? originalPdfPaths : null,
            };

            var printableSections = DataPageDossierAvailability.EvaluatePrintableSections(
                options,
                record,
                projectFolder,
                hasSchachtVon: schachtVon != null,
                hasSchachtBis: schachtBis != null,
                hasHydraulikResult: calcResult != null,
                kostenAvailable,
                originalPdfPaths.Count);
            var hasDossierBaseSection = printableSections.HasDossierBaseSection;

            // Pruefung ob druckbar (muss auf UI-Thread, wegen MessageBox)
            if (!printableSections.HasAnySection)
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

        // Das Haltung-spezifische Protokoll aus der Verteilung bevorzugen (via Link -> Haltungs-
        // ordner), statt das grosse Original-PDF mit ALLEN Protokollen (PDF_Path/PDF_All[0]).
        var path = EnsureProtocolPath(record);
        if (string.IsNullOrWhiteSpace(path))
        {
            var projectFolder = _shell.GetProjectFolder() ?? "";
            var paths = DataPageProtocolPathResolver.ResolveOriginalPdfPaths(record, projectFolder);
            path = paths.Count > 0 ? paths[0] : null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            var name = record.GetFieldValue("Haltungsname") ?? "(unbekannt)";
            _sp.Dialogs.Info(
                $"Kein PDF gefunden fuer Haltung '{name}'.\n\nPruefen Sie, ob das Protokoll-PDF in der Verteilung liegt.",
                "Haltungsprotokoll (PDF)");
            return;
        }

        if (!AuswertungPro.Next.UI.Services.SafeShellOpen.TryOpen(path, out var error))
        {
            _sp.Dialogs.Warn($"PDF konnte nicht geoeffnet werden:\n{error}",
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
        var presentation = LearningReadinessPresenter.Build(stats, similarCases, estimatedCost);
        LearningInfo = presentation.Info;
        LearningTrafficLightColor = presentation.Color;
        LearningTrafficLightText = presentation.Text;
        IsLearningInfoVisible = presentation.IsVisible;
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
            _trainingCaseIndex.ReplaceCaseIds(state.Cases.Select(tc => tc.CaseId));
        }
        catch
        {
            // Training-Daten nicht verfügbar – kein Fehler
        }
    }

    /// <summary>
    /// Prüft ob eine Haltung im Training Center erfasst ist.
    /// </summary>
    public bool IsTrainedCase(string? haltungsname) => _trainingCaseIndex.IsTrainedCase(haltungsname);

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
                _measureRecommendationService.TrainModel(LearningReadinessPresenter.MinimumSamplesForTraining);
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
