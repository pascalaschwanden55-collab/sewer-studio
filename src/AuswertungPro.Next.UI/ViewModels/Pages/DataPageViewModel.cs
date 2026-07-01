using AuswertungPro.Next.UI.Dialogs;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Hydraulik;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DataPageViewModel : ObservableObject, IDisposable
{
    public event Action? RecordsOrderChanged;

    private readonly ServiceProvider _sp;
    private readonly ShellViewModel _shell;
    private readonly DataPageTimerController _timers;
    private readonly DataPagePrintController _printController;
    private readonly DataPageOriginalPdfController _originalPdfController;
    private readonly DataPageMeasureSuggestionController _measureSuggestionController;
    private readonly DataPageCostRestoreController _costRestoreController;
    private readonly DataPageVideoRelinkController _videoRelinkController;
    private readonly DataPageVideoPlaybackController _videoPlaybackController;
    private readonly DataPageMediaSearchController _mediaSearchController;
    private readonly DataPageProtocolWindowController _protocolWindowController;
    private readonly DataPageObservationSyncController _observationSyncController;
    private readonly DataPageRecordCollectionController _recordCollectionController;
    private readonly DataPageVideoAnalysisController _videoAnalysisController;
    private readonly DataPageSanierungWindowController _sanierungWindowController;
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
    public IRelayCommand<HaltungRecord?> PlayGegenVideoCommand { get; }
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
        _timers = new DataPageTimerController(
            value => SaveStatus = value,
            value => IsSaveStatusVisible = value,
            AutoSaveOnTimerTick);
        _printController = new DataPagePrintController(
            _sp.Dialogs,
            _sp.ProtocolPdfExporter,
            () => _shell.GetProjectFolder(),
            record => DataPageHydraulikReportCalculator.BuildReportCalculation(
                record,
                _sp.Settings,
                saveSettings: _sp.Settings.Save),
            getLastProjectPath: () => _sp.Settings.LastProjectPath,
            findSchachtByNummer: FindSchachtByNummer,
            buildDossierHydraulikCalculation: (record, dn) => DataPageHydraulikReportCalculator.BuildReportCalculation(
                record,
                _sp.Settings,
                dn,
                saveSettings: _sp.Settings.Save));
        _originalPdfController = new DataPageOriginalPdfController(
            _sp.Dialogs,
            EnsureProtocolPath,
            () => _shell.GetProjectFolder(),
            DataPageProtocolPathResolver.ResolveOriginalPdfPaths,
            DataPageOriginalPdfController.TryShellOpen);
        _shell.PropertyChanged += ShellPropertyChanged;
        HookRunningNumbers();

        // Live-Control: Retry-Handler registrieren, damit der MCP eine Haltung
        // per Name erneut durch die KI-Videoanalyse schicken kann (nur wenn diese Seite lebt).
        LiveControl.LiveControlRetryBridge.Register(TryStartVideoAiPipelineByName);

        var gridLayout = DataPageGridLayoutController.Restore(_sp.Settings.DataPageLayout);
        GridMinRowHeight = gridLayout.GridMinRowHeight;
        GridZoom = gridLayout.GridZoom;
        IsColumnReorderEnabled = gridLayout.IsColumnReorderEnabled;

        SanierenOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadSanierenOptions());
        EigentuemerOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadEigentuemerOptions());
        PruefungsresultatOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadPruefungsresultatOptions());
        ReferenzpruefungOptions = new ObservableCollection<string>(DropdownOptionsStore.LoadReferenzpruefungOptions());
        EmpfohleneSanierungsmassnahmenOptions = new ObservableCollection<string>(
            DropdownOptionsStore.LoadEmpfohleneSanierungsmassnahmenOptions());
        AusgefuehrtDurchOptions = new ObservableCollection<string>(FieldCatalog.GetComboItems("Ausgefuehrt_durch"));
        _measureSuggestionController = new DataPageMeasureSuggestionController(
            _sp.Dialogs,
            _measureRecommendationService,
            () => Selected,
            () => Records,
            value => AddOptionIfMissing(EmpfohleneSanierungsmassnahmenOptions, value),
            () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
            },
            _shell.SetStatus,
            UpdateLearningInfo);
        _costRestoreController = new DataPageCostRestoreController(
            _sp.Dialogs,
            () => Selected,
            () => _sp.Settings.LastProjectPath,
            projectPath => new ProjectCostStoreRepository().Load(projectPath),
            ProjectCostStoreRepository.GetStorePath,
            (record, cost) => ApplyCostsToRecord(record, cost, learn: false),
            _shell.SetStatus);
        _videoRelinkController = new DataPageVideoRelinkController(
            _sp.Dialogs,
            () => _sp.Settings.LastVideoSourceFolder,
            () => _sp.Settings.LastVideoFolder,
            () => _sp.Settings.LastProjectPath,
            folder =>
            {
                _sp.Settings.LastVideoSourceFolder = folder;
                _sp.Settings.LastVideoFolder = folder; // legacy compatibility
                _sp.Settings.Save();
            },
            (record, path, userEdited) => SaveVideoLink(record, path, userEdited));
        _videoPlaybackController = new DataPageVideoPlaybackController(
            _sp.Dialogs,
            EnsureVideoPath,
            () => PlayerWindowOptions.FromSettings(_sp.Settings),
            DataPageVideoOverlayBuilder.Build,
            ShowPlayerWindow,
            (ex, path) => DataPageVideoStartErrorLogWriter.TryWrite(ex, path));
        _mediaSearchController = new DataPageMediaSearchController(
            () => Records,
            () => _sp.Settings.LastVideoSourceFolder,
            () => _sp.Settings.LastVideoFolder,
            ShowMediaSearchWindow,
            () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
            },
            () => OnPropertyChanged(nameof(Records)),
            _shell.SetStatus);
        _observationSyncController = new DataPageObservationSyncController(
            () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
            },
            RefreshRecordInGrid,
            record => Selected?.Id == record.Id,
            RefreshSelectedProtocolEntries,
            ScheduleAutoSave,
            _shell.SetStatus);
        _protocolWindowController = new DataPageProtocolWindowController(
            () => _shell.Project,
            () => _sp.Settings.LastProjectPath,
            ResolveExistingPath,
            ShowProtocolWindow,
            () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
                ScheduleAutoSave();
            },
            record => SyncObservationsToHoldingFields(record),
            record =>
            {
                if (Selected?.Id == record.Id)
                    RefreshSelectedProtocolEntries();
            });
        _recordCollectionController = new DataPageRecordCollectionController(
            () => _shell.Project,
            () => Selected,
            value => Selected = value,
            (message, title) => _sp.Dialogs.Confirm(message, title),
            () => RecordsOrderChanged?.Invoke(),
            ScheduleAutoSave);
        _videoAnalysisController = new DataPageVideoAnalysisController(
            _sp.Dialogs,
            () => Records,
            EnsureVideoPath,
            () => _sp.CodeCatalog.AllowedCodes(),
            () => new AppSettingsAiSettingsProvider().Load().ToRuntimeSettings(),
            (cfg, plausibility, http) => _sp.CreateVideoAnalysisPipeline(cfg, plausibility, http),
            ShowVideoAnalysisPipelineWindow,
            record => Selected?.Id == record.Id,
            _shell.MarkProjectDirty,
            RefreshRecordInGrid,
            RefreshSelectedProtocolEntries,
            ScheduleAutoSave,
            action => System.Windows.Application.Current?.Dispatcher.BeginInvoke(action));
        _sanierungWindowController = new DataPageSanierungWindowController(
            _sp.Dialogs,
            () => Selected,
            ParseRecommendedTemplates,
            () => new AppSettingsAiSettingsProvider().Load().ToRuntimeSettings(),
            (record, maxSuggestions) => _measureRecommendationService.Recommend(record, maxSuggestions),
            (record, cost) => ApplyCostsToRecord(record, cost),
            () =>
            {
                _shell.Project.ModifiedAtUtc = DateTime.UtcNow;
                _shell.Project.Dirty = true;
            },
            RefreshRecordInGrid,
            ScheduleAutoSave,
            _shell.SetStatus,
            ShowSanierungsmassnahmenWindow);

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
        PlayGegenVideoCommand = new RelayCommand<HaltungRecord?>(PlayGegenVideo);
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
        if (_numberedRecords is not null)
            _numberedRecords.CollectionChanged -= RecordsCollectionChangedForNumbers;
        PropertyChanged -= DataPageViewModel_PropertyChanged;
        _timers.Stop();
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
            HookRunningNumbers();
        }
    }

    // Transiente Anzeige-Laufnummer (1..N): bei Projektwechsel neu abonnieren und durchzaehlen,
    // danach bei jeder Reihenfolge-/Bestandsaenderung (Add/Remove/Move) automatisch aktualisieren.
    private System.Collections.Specialized.INotifyCollectionChanged? _numberedRecords;

    private void HookRunningNumbers()
    {
        if (_numberedRecords is not null)
            _numberedRecords.CollectionChanged -= RecordsCollectionChangedForNumbers;

        _numberedRecords = Records as System.Collections.Specialized.INotifyCollectionChanged;
        if (_numberedRecords is not null)
            _numberedRecords.CollectionChanged += RecordsCollectionChangedForNumbers;

        AuswertungPro.Next.Application.Common.HaltungRunningNumberService.Assign(Records);
    }

    private void RecordsCollectionChangedForNumbers(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => AuswertungPro.Next.Application.Common.HaltungRunningNumberService.Assign(Records);

    partial void OnGridMinRowHeightChanged(double value)
    {
        var clamped = DataPageGridLayoutController.ClampGridMinRowHeight(value);
        if (Math.Abs(clamped - value) > 0.001d)
        {
            GridMinRowHeight = clamped;
            return;
        }

        PersistDataPageBasicUiSettings();
    }

    partial void OnGridZoomChanged(double value)
    {
        var clamped = DataPageGridLayoutController.ClampGridZoom(value);
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
        if (e.PropertyName != nameof(Selected))
            return;

        DataPageSelectionChangedController.Handle(
            Selected,
            new IRelayCommand?[]
            {
                RemoveCommand,
                MoveUpCommand,
                MoveDownCommand,
                OpenCostsCommand,
                RestoreCostsCommand,
                SuggestMeasuresCommand,
                OptimizeSanierungKiCommand
            },
            NormalizeSelectedFindings,
            SyncSelectedProtocolFromFindings,
            RefreshSelectedProtocolEntries);
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
        => _recordCollectionController.CanMoveUp();

    private bool CanMoveDown()
        => _recordCollectionController.CanMoveDown();

    private void Add()
    {
        _recordCollectionController.Add();
    }

    private void Remove()
    {
        _recordCollectionController.Remove();
    }

    private void MoveUp()
    {
        _recordCollectionController.MoveUp();
    }

    private void MoveDown()
    {
        _recordCollectionController.MoveDown();
    }

    /// <summary>
    /// Verschiebt die aktuell selektierte Haltung an die angegebene 1-basierte Position.
    /// Alle Zeilen ab dieser Position rutschen um eins nach unten.
    /// </summary>
    public bool MoveToPosition(int targetPosition)
    {
        return _recordCollectionController.MoveToPosition(targetPosition);
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
        _timers.ScheduleAutoSave(
            _sp.Settings.DataAutoSaveMode,
            markDirty: () => _shell.Project.Dirty = true,
            save: AutoSave);
    }

    private void AutoSaveOnTimerTick()
    {
        _timers.HandleAutoSaveTimerTick(
            _sp.Settings.DataAutoSaveMode,
            save: AutoSave,
            isProjectDirty: () => _shell.Project.Dirty);
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
        _videoPlaybackController.Play(record);
    }

    // Spielt das Gegeninspektions-Video (Feld Link_G) ab, relativ gegen den Projekt-Root aufgelöst.
    private void PlayGegenVideo(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = ResolveExistingPath(record.GetFieldValue("Link_G"));
        if (string.IsNullOrWhiteSpace(path))
        {
            _sp.Dialogs.Info("Für diese Haltung ist keine Gegeninspektion vorhanden.", "Gegeninspektion");
            return;
        }

        _videoPlaybackController.PlayResolved(record, path);
    }

    private void ShowPlayerWindow(DataPageVideoPlaybackRequest request)
    {
        var window = new PlayerWindow(
            request.Path,
            request.Options,
            damageOverlay: request.DamageOverlay,
            serviceProvider: _sp,
            haltungId: request.Record.Id.ToString(),
            haltungRecord: request.Record)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.Show();
    }

    private void OpenProtocol(HaltungRecord? record)
    {
        _protocolWindowController.Open(record);
    }

    private void ShowProtocolWindow(DataPageProtocolWindowRequest request)
    {
        var dlg = new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow(
            request.Record,
            request.Project,
            _sp,
            request.ResolvedVideoPath,
            request.ProjectFolder,
            request.MarkDirty);
        dlg.Owner = System.Windows.Application.Current?.MainWindow;
        dlg.ShowDialog();
    }

    public void SyncObservationsToHoldingFields(HaltungRecord? record, bool showStatus = false)
    {
        _observationSyncController.Sync(record, showStatus);
    }

    private void OpenVideoAiPipeline(HaltungRecord? record)
    {
        _videoAnalysisController.Open(record);
    }

    private PipelineResult? ShowVideoAnalysisPipelineWindow(
        PipelineRequest request,
        IVideoAnalysisPipelineService pipeline)
    {
        var win = new VideoAnalysisPipelineWindow(request, pipeline)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        return win.ShowDialog() == true ? win.Result : null;
    }

    /// <summary>
    /// Startet die KI-Videoanalyse fuer eine Haltung anhand ihres Namens erneut –
    /// genutzt von der Live-Control-Bruecke (MCP retry_holding).
    /// Die Suche laeuft sofort; das Analyse-Fenster wird per Dispatcher nachgeschoben,
    /// damit die Live-Control-Antwort nicht bis zum Schliessen des Fensters blockiert.
    /// </summary>
    public LiveControl.LiveControlRetryResult TryStartVideoAiPipelineByName(string haltungsname)
    {
        return _videoAnalysisController.TryStartByName(haltungsname);
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
            : _shell.GetProjectFolder();   // Projekt-ROOT (nicht GetDirectoryName der projekt.json)

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
        _videoRelinkController.Relink(record);
    }

    private bool CanOpenCosts(HaltungRecord? record)
        => DataPageCommandTargetController.HasTarget(record, Selected);

    private bool CanRestoreCosts(HaltungRecord? record)
        => DataPageCommandTargetController.HasTarget(record, Selected);

    private bool CanSuggestMeasures(HaltungRecord? record)
        => DataPageCommandTargetController.HasTarget(record, Selected);

    private void RestoreCosts(HaltungRecord? record)
    {
        _costRestoreController.Restore(record);
    }

    private void OpenCosts(HaltungRecord? record)
    {
        OpenSanierungsMatrix(record);
    }

    private void SuggestMeasures(HaltungRecord? record)
    {
        _measureSuggestionController.Suggest(record);
    }

    /// <summary>
    /// Batch: Fuer alle Haltungen mit Sanierungsbedarf (oder fehlenden Massnahmen)
    /// automatisch Sanierungsmassnahmen vorschlagen.
    /// </summary>
    public void SuggestAllMeasures()
    {
        _measureSuggestionController.SuggestAll();
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
        _sanierungWindowController.Open(record, focus);
    }

    private void ShowSanierungsmassnahmenWindow(DataPageSanierungWindowRequest request)
    {
        var costCalcVm = new CostCalculatorViewModel(
            request.Holding,
            null,
            request.RecommendedTemplates,
            _sp.Settings.LastProjectPath,
            request.ApplyCosts,
            haltungRecord: request.Record,
            projectRecords: Records);

        SanierungOptimizationViewModel? optimizationVm = null;
        if (request.RuntimeSettings is not null)
        {
            var aiService = _sp.CreateSanierungOptimization(request.RuntimeSettings);
            optimizationVm = new SanierungOptimizationViewModel(request.Record, aiService, request.RuleRecommendation);
            optimizationVm.TransferredToPrimary += _ => request.OnOptimizationTransferred();
        }

        var vm = new SanierungsmassnahmenViewModel(costCalcVm, optimizationVm, request.Record, request.Focus);
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
            : _shell.GetProjectFolder();   // Projekt-ROOT (nicht GetDirectoryName der projekt.json)

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
        _mediaSearchController.Open();
    }

    private DataPageMediaSearchResult? ShowMediaSearchWindow(IReadOnlyList<HaltungRecord> records, string? initial)
    {
        var win = new MediaSearchWindow(records.ToList(), initial, _sp);
        win.Owner = System.Windows.Application.Current?.MainWindow;

        return win.ShowDialog() == true
            ? new DataPageMediaSearchResult(win.Applied, win.AppliedVideoCount, win.AppliedPdfCount, win.AppliedFotoCount)
            : null;
    }

    private void OpenHydraulikPanel(HaltungRecord? record)
    {
        var request = DataPageHydraulikPanelController.BuildOpenRequest(record);
        ShowHydraulikPanel(request);
    }

    private void ShowHydraulikPanel(DataPageHydraulikPanelRequest request)
    {
        var vm = new HydraulikPanelViewModel(_sp.Settings);
        vm.LoadFromRecord(request.DnMillimeters, request.Material, request.WasserstandMillimeters);

        var win = new HydraulikPanelWindow(vm);
        win.Owner = System.Windows.Application.Current?.MainWindow;
        win.ShowDialog();
    }

    private void PrintAwuHaltungsprotokollPdf(HaltungRecord? record)
    {
        _printController.PrintAwuHaltungsprotokollPdf(
            _shell.Project,
            record,
            EnsureProtocolDocumentForPdf);
    }

    private ProtocolDocument EnsureProtocolDocumentForPdf(HaltungRecord record)
        => _protocolDocumentController.EnsureForPdf(record, _sp.Protocols, ResolveCodeTitle);

    private async void PrintHydraulikPdf(HaltungRecord? record)
    {
        await _printController.PrintHydraulikPdfAsync(record);
    }

    private async void PrintDossierPdf(HaltungRecord? record)
    {
        await _printController.PrintDossierPdfAsync(_shell.Project, record);
    }

    private void OpenOriginalPdf(HaltungRecord? record)
    {
        _originalPdfController.Open(record);
    }

    private SchachtRecord? FindSchachtByNummer(string? nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            return null;
        return _shell.Project.SchaechteData.FirstOrDefault(s =>
            string.Equals(s.GetFieldValue("Schachtnummer"), nummer, StringComparison.OrdinalIgnoreCase));
    }

    private string? ResolveExistingPath(string? raw)
        => DataPageProtocolPathResolver.ResolveExistingPath(raw, _sp.Settings.LastProjectPath);

    private void ShowSaveStatus(string? text)
    {
        _timers.ShowSaveStatus(text);
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
        DataPageGridLayoutController.Persist(
            _sp.Settings.DataPageLayout,
            GridMinRowHeight,
            GridZoom,
            IsColumnReorderEnabled,
            layout => _sp.Settings.DataPageLayout = layout,
            _sp.Settings.Save);
    }
}
