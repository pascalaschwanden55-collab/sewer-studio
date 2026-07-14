using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Collections;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

public partial class TrainingCenterViewModel : ObservableObject
{
    private readonly TrainingCenterStore _store;
    private readonly TrainingCenterImportService _import;
    private readonly ICodeCatalogProvider? _codeCatalog;
    private readonly IKnowledgeBaseDiagnosticsRunner _kbDiagnostics;
    private readonly TrainingCenterKnowledgeBaseDashboardController _kbDashboard;
    private readonly AppSettings? _settings;
    private readonly IUiThread _uiThread;

    /// <summary>Wiederverwendbarer HttpClient fuer KB-Operationen (Embedding-Requests).</summary>
    private System.Net.Http.HttpClient? _kbHttpClient;

    /// <summary>Optionale Referenz auf die Review Queue (gesetzt von Window).</summary>
    public InfraSelfImproving.ReviewQueueService? ReviewQueueServiceRef { get; set; }

    public ObservableCollection<TrainingCase> Cases { get; } = new();
    public ObservableCollection<TrainingSample> Samples { get; } = new();

    [ObservableProperty] private TrainingCase? _selectedCase;
    [ObservableProperty] private TrainingSample? _selectedSample;
    [ObservableProperty] private string _rootFolder = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // Live-Vorschau während Batch-Import
    [ObservableProperty] private string _liveFramePath = "";
    [ObservableProperty] private string _liveCaseInfo = "";
    [ObservableProperty] private string _liveCodeInfo = "";
    [ObservableProperty] private string _liveMeterInfo = "";
    private DateTime _lastLiveFrameUpdate = DateTime.MinValue;

    /// <summary>Setzt LiveFramePath mit Throttling (~5 fps max), um UI-Thread nicht zu ueberlasten.</summary>
    private void SetLiveFrameThrottled(string? path)
    {
        TrainingLiveFrameThrottleController.Apply(
            path,
            () => _lastLiveFrameUpdate,
            value => _lastLiveFrameUpdate = value,
            value => LiveFramePath = value);
    }

    // KB-Trainingsstand
    [ObservableProperty] private int _kbSampleCount;
    [ObservableProperty] private int _kbErrorCount;   // Approved aber Quality-Gate fehlgeschlagen
    [ObservableProperty] private int _kbNewCount;      // Nicht-approved (Status=New)
    [ObservableProperty] private int _kbEmbeddingCount;
    [ObservableProperty] private int _kbCodesCovered;
    [ObservableProperty] private string _kbReadinessLabel = "Unbekannt";
    [ObservableProperty] private System.Windows.Media.Brush _kbReadinessBrush
        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
    [ObservableProperty] private string _kbLastUpdate = "\u2014";
    [ObservableProperty] private string _kbTopCodesText = "";

    // KB-Qualitaet Dashboard
    [ObservableProperty] private string _kbCoverageGapsText = "";
    [ObservableProperty] private int _kbCoverageGapsCount;
    [ObservableProperty] private string _kbAccuracyText = "";
    [ObservableProperty] private int _kbStaleSampleCount;
    [ObservableProperty] private string _kbTrendText = "";
    [ObservableProperty] private string _kbTrendDirection = "";
    // Exact-Quoten der letzten Laeufe (0..1) fuer die Trend-Sparkline im KB-Tab.
    [ObservableProperty] private IReadOnlyList<double> _kbTrendSeries = [];

    // Review Queue (Self-Improving Loop)
    public ObservableCollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReviewCard))]
    [NotifyCanExecuteChangedFor(nameof(ApproveSelectedReviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(RejectSelectedReviewCommand))]
    private InfraSelfImproving.ReviewQueueItem? _selectedReviewItem;

    /// <summary>
    /// Optionale YOLO-Box die der Reviewer auf dem Review-Karten-Bild gezeichnet hat.
    /// Wird beim Approve an ApproveSelfTrainingAsync weitergereicht (B5).
    /// </summary>
    public BoundingBox? PendingBox { get; set; }

    /// <summary>
    /// Optionale SAM-Maske zur gezeichneten Review-Box. Wird beim Approve gespeichert.
    /// </summary>
    public TrainingSegmentationMask? PendingSamMask { get; set; }

    [ObservableProperty] private int _reviewQueueCount;
    [ObservableProperty] private string _reviewStatusText = "";

    /// <summary>Projektion des aktuell gewaehlten Kandidaten auf die Karte (null = nichts gewaehlt).</summary>
    public ReviewCardViewModel? SelectedReviewCard
        => SelectedReviewItem is null ? null : new ReviewCardViewModel(SelectedReviewItem);

    // ── Selbsttraining-Visualisierungen ──
    public ObservableCollection<SelfTrainingEntryResult> SelfTrainingResults { get; } = new();
    public ObservableCollection<CodeDistributionEntry> CodeDistribution { get; } = new();
    public ObservableCollection<string> SelfTrainingLogEntries { get; } = new();

    [ObservableProperty] private int _pipelineActiveStep; // 0-5 (BuildingTimeline..Completed)
    [ObservableProperty] private string _currentEntryCode = "";
    [ObservableProperty] private double _currentEntryMeter;
    [ObservableProperty] private string _currentComparisonText = "";
    [ObservableProperty] private string _currentTechniqueGrade = "";
    [ObservableProperty] private string _currentTechniqueDetails = "";

    // Aktives KI-Modell Anzeige
    [ObservableProperty] private string _activeModelName = "";
    [ObservableProperty] private bool _isModelActive;

    // Match-Rate Prozentsaetze
    [ObservableProperty] private double _exactPercent;
    [ObservableProperty] private double _partialPercent;
    [ObservableProperty] private double _mismatchPercent;
    [ObservableProperty] private double _noFindingsPercent;
    private readonly SelfTrainingMatchRateTracker _matchRateTracker = new();
    private readonly SelfTrainingCancellationController _selfTrainingCancellation = new();

    private void RefreshMatchRatePercents()
    {
        var p = _matchRateTracker.ComputePercents();
        SelfTrainingMatchRatePresentationController.Apply(
            p,
            CreateMatchRatePresentationUi());
    }

    private void AddSelfTrainingLog(string message)
    {
        TrainingCenterLogController.AppendSelfTrainingLog(
            message,
            OnUi,
            SelfTrainingLogEntries);
    }

    private void UpdateCodeDistribution(string code, MatchLevel level)
    {
        SelfTrainingCodeDistributionController.ApplyMatchOnUi(
            CodeDistribution,
            code,
            level,
            OnUi);
    }

    /// <summary>Wird vom SelfTrainingOrchestrator bei jedem Schritt aufgerufen.</summary>
    public void OnSelfTrainingStep(SelfTrainingStep step)
    {
        SelfTrainingStepWorkflow.Apply(
            SelfTrainingStepWorkflowRequestFactory.Create(new SelfTrainingStepWorkflowRequestFactoryRequest(
                Step: step,
                ActiveVisionModel: _activeVisionModel,
                OnUi: OnUi,
                SetPipelineActiveStep: value => PipelineActiveStep = value,
                SetCurrentEntryCode: value => CurrentEntryCode = value,
                SetCurrentEntryMeter: value => CurrentEntryMeter = value,
                SetProgressValue: value => ProgressValue = value,
                SetProgressMax: value => ProgressMax = value,
                SetActiveModelName: value => ActiveModelName = value,
                SetIsModelActive: value => IsModelActive = value,
                SetCurrentTechniqueGrade: value => CurrentTechniqueGrade = value,
                SetCurrentTechniqueDetails: value => CurrentTechniqueDetails = value,
                SetCurrentComparisonText: value => CurrentComparisonText = value,
                Log: AddSelfTrainingLog,
                SetLiveFrame: SetLiveFrameThrottled,
                MatchRateTracker: _matchRateTracker,
                RefreshMatchRatePercents: RefreshMatchRatePercents,
                Results: SelfTrainingResults,
                UpdateCodeDistribution: UpdateCodeDistribution)));
    }

    /// <summary>Setzt alle Selbsttraining-Visualisierungen zurueck.</summary>
    /// <param name="resetMatchRate">Match-Rate auf 0 setzen (nur bei echtem Selbsttraining, nicht bei Batch-Import).</param>
    private void ResetSelfTrainingVisuals(bool resetMatchRate = false)
    {
        SelfTrainingVisualResetController.Reset(
            new SelfTrainingVisualResetRequest(
                SelfTrainingResults,
                CodeDistribution,
                SelfTrainingLogEntries,
                value => PipelineActiveStep = value,
                value => CurrentEntryCode = value,
                value => CurrentEntryMeter = value,
                value => CurrentComparisonText = value,
                value => CurrentTechniqueGrade = value,
                value => CurrentTechniqueDetails = value,
                _matchRateTracker.Reset,
                RefreshMatchRatePercents),
            resetMatchRate);
    }

    private readonly List<string> _rootFolders = new();
    private CancellationTokenSource? _genCts;

    /// <summary>Fügt eine Zeile zum Log hinzu (Thread-safe via Dispatcher).</summary>
    private void Log(string message)
    {
        TrainingCenterLogController.AppendLog(
            message,
            OnUi,
            () => LogText,
            value => LogText = value,
            SelfTrainingLogEntries);
    }

    /// <summary>Aktualisiert die Live-Vorschau (Thread-safe).</summary>
    private void UpdateLivePreview(string caseInfo, string code, string meter, string? framePath)
    {
        TrainingLivePreviewApplyController.ApplyOnUi(
            caseInfo,
            code,
            meter,
            framePath,
            new TrainingLivePreviewApplyUi(
                value => LiveCaseInfo = value,
                value => LiveCodeInfo = value,
                value => LiveMeterInfo = value,
                value => CurrentComparisonText = value,
                value => CurrentEntryCode = value,
                SetLiveFrameThrottled,
                () => LiveFramePath,
                value => LiveFramePath = value),
            OnUi);
    }

    private void ClearLivePreview()
    {
        TrainingLivePreviewClearController.ApplyOnUi(
            value => LiveCaseInfo = value,
            value => LiveCodeInfo = value,
            value => LiveMeterInfo = value,
            value => CurrentComparisonText = value,
            value => CurrentEntryCode = value,
            SetLiveFrameThrottled,
            OnUi);
    }

    private Task RefreshKbStatusAsync() => _kbDashboard.RefreshStatusAsync();

    // ── Cases ────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        await TrainingCenterLoadWorkflow.RunAsync(
            TrainingCenterLoadRequestFactory.CreateWithDefaults(new TrainingCenterLoadDefaultRequestFactoryRequest(
                LoadStateAsync: _store.LoadAsync,
                RootFolders: _rootFolders,
                ReplaceCases: items => ObservableCollectionContentController.ReplaceWith(Cases, items),
                UpdateRootFolderDisplay: UpdateRootFolderDisplay,
                SetStatusText: value => StatusText = value,
                LoadSamplesAsync: LoadSamplesInternalAsync,
                RefreshKbStatusAsync: RefreshKbStatusAsync,
                LoadLastMatchRateAsync: LoadLastMatchRateAsync)));
    }

    /// <summary>
    /// Laedt die letzte Match-Rate aus der Selbsttraining-Historie,
    /// damit beim Oeffnen des Training Centers ein sinnvoller Wert angezeigt wird.
    /// </summary>
    private async Task LoadLastMatchRateAsync()
    {
        await SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(
            SelfTrainingLastMatchRateRefreshRequestFactory.CreateWithDefaults(
                new SelfTrainingLastMatchRateRefreshDefaultRequestFactoryRequest(
                    CreateMatchRatePresentationUi()))).ConfigureAwait(false);
    }

    private SelfTrainingMatchRatePresentationUi CreateMatchRatePresentationUi()
        => new(
            value => ExactPercent = value,
            value => PartialPercent = value,
            value => MismatchPercent = value,
            value => NoFindingsPercent = value);

    [RelayCommand]
    private void BrowseRootFolder()
    {
        TrainingCenterRootFolderWorkflow.ApplySelected(
            _rootFolders,
            TrainingCenterRootFolderDialogSelector.SelectFolders(),
            UpdateRootFolderDisplay);
    }

    [RelayCommand]
    private void ClearRootFolders()
    {
        TrainingCenterRootFolderWorkflow.Clear(
            _rootFolders,
            UpdateRootFolderDisplay);
    }

    private void UpdateRootFolderDisplay()
    {
        RootFolder = TrainingCenterDisplayFormatter.FormatRootFolders(_rootFolders);
    }

    [RelayCommand]
    private async Task DistributeHaltungAsync()
    {
        await TrainingCenterDistributionWorkflow.RunAsync(
            TrainingCenterDistributionRequestFactory.CreateWithDefaultSelectors(
                new TrainingCenterDistributionDefaultRequestFactoryRequest(
                    GetIsBusy: () => IsBusy,
                    SetIsBusy: value => IsBusy = value,
                    DistributeAsync: (pdfPath, videoFolder, outputFolder) =>
                        _import.DistributeByHaltungAsync(pdfPath, videoFolder, outputFolder),
                    RootFolders: _rootFolders,
                    UpdateRootFolderDisplay: UpdateRootFolderDisplay,
                    SetLogText: value => LogText = value,
                    SetStatusText: value => StatusText = value,
                    Log: Log)));
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        await TrainingCenterScanWorkflow.RunAsync(
            TrainingCenterScanRequestFactory.CreateWithDefaults(new TrainingCenterScanDefaultRequestFactoryRequest(
                GetIsBusy: () => IsBusy,
                SetIsBusy: value => IsBusy = value,
                RootFolders: _rootFolders,
                ScanInputsAsync: _import.ScanAsync,
                ReplaceCases: ReplaceScannedCases,
                AppendCases: AppendScannedCases,
                SetStatusText: value => StatusText = value,
                SaveStateAsync: AutoSaveStateAsync)));
    }

    private void ReplaceScannedCases(IReadOnlyList<TrainingCase> items)
        => ObservableCollectionContentController.ReplaceWith(Cases, items);

    private void AppendScannedCases(IReadOnlyList<TrainingCase> items)
        => ObservableCollectionContentController.Append(Cases, items);

    /// <summary>Speichert Faelle + Root-Ordner automatisch (ohne UI-Feedback).</summary>
    private async Task AutoSaveStateAsync()
    {
        try
        {
            await _store.SaveAsync(TrainingCenterSaveRequestFactory.BuildStateWithDefaults(Cases, _rootFolders));
        }
        catch { /* stilles Speichern */ }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await TrainingCenterSaveWorkflow.RunAsync(
            TrainingCenterSaveRequestFactory.CreateWithDefaults(new TrainingCenterSaveDefaultRequestFactoryRequest(
                GetIsBusy: () => IsBusy,
                SetIsBusy: value => IsBusy = value,
                Cases: Cases,
                RootFolders: _rootFolders,
                SaveStateAsync: _store.SaveAsync,
                SetStatusText: SetSaveStatusText)));
    }

    private void SetSaveStatusText(string value) => StatusText = value;

    private bool HasSelection() => SelectedCase is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Approve()
    {
        TrainingCaseCommandWorkflow.Run(
            TrainingCaseCommandRequestFactory.Create(new TrainingCaseCommandRequestFactoryRequest(
                SelectedCase,
                TrainingCaseDecision.Approve,
                SetCaseCommandStatusText)));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reject()
    {
        TrainingCaseCommandWorkflow.Run(
            TrainingCaseCommandRequestFactory.Create(new TrainingCaseCommandRequestFactoryRequest(
                SelectedCase,
                TrainingCaseDecision.Reject,
                SetCaseCommandStatusText)));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SetNew()
    {
        TrainingCaseCommandWorkflow.Run(
            TrainingCaseCommandRequestFactory.Create(new TrainingCaseCommandRequestFactoryRequest(
                SelectedCase,
                TrainingCaseDecision.SetNew,
                SetCaseCommandStatusText)));
    }

    private void SetCaseCommandStatusText(string value) => StatusText = value;

    partial void OnSelectedCaseChanged(TrainingCase? value)
    {
        TrainingSelectionCommandRefreshController.RefreshCaseSelection(
            new TrainingCaseSelectionCommandRefresh(
                ApproveCommand.NotifyCanExecuteChanged,
                RejectCommand.NotifyCanExecuteChanged,
                SetNewCommand.NotifyCanExecuteChanged,
                GenerateSamplesCommand.NotifyCanExecuteChanged));
    }

    // ── Samples ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSamplesAsync()
    {
        await LoadSamplesInternalAsync();
    }

    private async Task LoadSamplesInternalAsync()
    {
        await TrainingSampleLoadWorkflow.RunAsync(
            TrainingSampleLoadRequestFactory.Create(
                Samples,
                OnUi,
                _trainingSamples.LoadAsync)).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task GenerateSamplesAsync()
    {
        await TrainingCenterSampleGenerationWorkflow.RunAsync(
            TrainingCenterSampleGenerationRequestFactory.CreateWithDefaults(
                new TrainingCenterSampleGenerationDefaultRequestFactoryRequest(
                    SelectedCase,
                    GetIsBusy: () => IsBusy,
                    SetIsBusy: value => IsBusy = value,
                    ResetCancellation: ResetGenerationCancellation,
                    CodeCatalog: _codeCatalog,
                    AppendSamples: samples => TrainingSampleCollectionController.Append(Samples, samples),
                    SetStatusText: value => StatusText = value)));
    }

    private CancellationTokenSource ResetGenerationCancellationSource()
    {
        _genCts = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_genCts);
        return _genCts;
    }

    private CancellationToken ResetGenerationCancellation()
    {
        return ResetGenerationCancellationSource().Token;
    }

    internal void CancelOutstandingOperations()
    {
        _genCts = CancellationTokenSourceLifecycle.CancelDisposeAndClear(_genCts);
    }

    private bool HasSampleSelection() => SelectedSample is not null;

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task ApproveSampleAsync()
    {
        await RunSampleCommandAsync(TrainingSampleDecisionController.Approve);
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        await RunSampleCommandAsync(TrainingSampleDecisionController.Reject);
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RemoveSampleAsync()
    {
        await RunSampleCommandAsync(TrainingSampleDecisionController.Remove);
    }

    private async Task RunSampleCommandAsync(Func<TrainingSample, TrainingSampleDecisionResult> decide)
    {
        await TrainingSampleCommandWorkflow.RunAsync(
            TrainingSampleCommandRequestFactory.CreateWithDefaults(
                new TrainingSampleCommandRequestFactoryRequest(
                    SelectedSample,
                    decide,
                    () => _kbHttpClient,
                    value => _kbHttpClient = value,
                    SetSampleCommandStatusText,
                    PersistSamplesAsync)));
    }

    private void SetSampleCommandStatusText(string value) => StatusText = value;

    /// <summary>
    /// Entfernt ein Sample aus der Wissensdatenbank (Deindex), ohne Ollama zu benoetigen.
    /// Fehler werden still geschluckt — die Status-Aenderung bleibt persistiert.
    /// </summary>
    private void TryDeindexSample(string sampleId)
    {
        TrainingKnowledgeBaseSampleDeindexer.TryDeindexWithDefaults(
            sampleId,
            () => _kbHttpClient,
            value => _kbHttpClient = value);
    }

    [RelayCommand]
    private async Task ExportApprovedAsync()
    {
        await TrainingApprovedProtocolExportWorkflow.RunAsync(
            TrainingApprovedProtocolExportRequestFactory.CreateWithDefaults(
                new TrainingApprovedProtocolExportDefaultRequestFactoryRequest(
                    GetIsBusy: () => IsBusy,
                    SetIsBusy: value => IsBusy = value,
                    Samples: Samples,
                    IsExportEligible: sample => TrainingSampleExportEligibility.EvaluateAndUpdate(sample, _codeCatalog),
                    PersistSamplesAsync: () => PersistSamplesAsync(),
                    Log: Log,
                    SetStatusText: SetApprovedProtocolExportStatusText)));
    }

    private void SetApprovedProtocolExportStatusText(string value) => StatusText = value;

    /// <summary>
    /// Batch-Import: Scannt alle Ordner, generiert Samples, approved automatisch,
    /// indiziert in die Knowledge Base. Alles in einem Durchlauf.
    /// </summary>
    [RelayCommand]
    private async Task BatchImportAndIndexAsync()
    {
        await TrainingBatchImportCommandWorkflow.RunAsync(
            TrainingBatchImportCommandRequestFactory.CreateWithDefaults(new TrainingBatchImportCommandRunDefaultRequestFactoryRequest(
                GetIsBusy: () => IsBusy,
                RootFolders: _rootFolders,
                CreateCancellationSource: ResetGenerationCancellationSource,
                StoreCancellationSource: cts => _genCts = cts,
                ScanInputsAsync: _import.ScanAsync,
                Cases: Cases,
                CodeCatalog: _codeCatalog,
                SaveStateAsync: () => _store.SaveAsync(TrainingCenterSaveRequestFactory.BuildStateWithDefaults(Cases, _rootFolders)),
                GetSelfTrainingResultCount: () => SelfTrainingResults.Count,
                SetBusy: value => IsBusy = value,
                SetLogText: value => LogText = value,
                SetProgressValue: value => ProgressValue = value,
                SetProgressMax: value => ProgressMax = value,
                SetStatusText: value => StatusText = value,
                Log: Log,
                UpdateLivePreview: preview => UpdateLivePreview(
                    preview.CaseInfo,
                    preview.CodeInfo,
                    preview.MeterInfo,
                    preview.FramePath),
                OnUi: OnUi,
                AddResult: SelfTrainingResults.Add,
                UpdateCodeDistribution: UpdateCodeDistribution,
                SetKbSampleCount: value => KbSampleCount = value,
                SetKbCodesCovered: value => KbCodesCovered = value,
                Samples: Samples,
                RefreshKbStatusAsync: RefreshKbStatusAsync,
                ClearLivePreview: ClearLivePreview,
                ResetSelfTrainingVisuals: () => ResetSelfTrainingVisuals()))).ConfigureAwait(false);
    }

    [RelayCommand]
    private void CancelBatch()
    {
        TrainingBatchImportRunControlController.Cancel(
            () => CancellationTokenSourceLifecycle.CancelIfPresent(_genCts),
            value => StatusText = value);
    }

    [RelayCommand]
    private async Task CheckKnowledgeBaseAsync()
    {
        await TrainingKnowledgeBaseCheckWorkflow.RunAsync(
            TrainingKnowledgeBaseCheckRequestFactory.Create(
                new TrainingKnowledgeBaseCheckRequestFactoryRequest(
                    IsBusy,
                    value => IsBusy = value,
                    value => StatusText = value,
                    topCodes => _kbDiagnostics.ReadSummaryAsync(topCodes),
                    RefreshKbStatusAsync,
                    Log,
                    CancellationToken.None)));
    }

    partial void OnSelectedSampleChanged(TrainingSample? value)
    {
        TrainingSelectionCommandRefreshController.RefreshSampleSelection(
            new TrainingSampleSelectionCommandRefresh(
                ApproveSampleCommand.NotifyCanExecuteChanged,
                RejectSampleCommand.NotifyCanExecuteChanged,
                RemoveSampleCommand.NotifyCanExecuteChanged));
    }

    /// <summary>
    /// Beim Wechsel des Review-Kandidaten PendingBox zuruecksetzen (B5).
    /// Die visuelle Box wird zusaetzlich vom Code-behind via OnVmPropertyChanged geloescht.
    /// </summary>
    partial void OnSelectedReviewItemChanged(InfraSelfImproving.ReviewQueueItem? value)
    {
        TrainingReviewPendingGeometryController.Clear(
            value => PendingBox = value,
            value => PendingSamMask = value);
    }


    /// <summary>
    /// Speichert alle Samples und indexiert optional ein gerade geaendertes Sample in die KB.
    /// </summary>
    private async Task PersistSamplesAsync(TrainingSample? changedSample = null)
    {
        await TrainingSamplePersistenceWorkflowController.PersistAsync(
            TrainingSamplePersistenceRequestFactory.Create(
                Samples,
                changedSample,
                IncrementalKbUpdateWithReasonAsync,
                new TrainingSamplePersistenceRequestFactoryDefaults(
                    _trainingSamples.MergeOrUpdateAsync),
                CancellationToken.None)).ConfigureAwait(false);
    }

    /// <summary>
    /// Holt alle menschlich bestaetigten Gold-Samples (Status=Approved), die noch nicht in der KB
    /// stehen (KbIndexState != Indexed), nachtraeglich in die KnowledgeBase.db. Legt vorher ein
    /// reversibles Backup an. Schreibt den KbIndexState pro Sample zurueck in training_samples.json.
    ///
    /// Hintergrund: Der Codiermodus persistiert bestaetigte Befunde zwar als Gold, indexiert sie aber
    /// nicht zuverlaessig in die KB (das Index-Ergebnis wurde frueher nicht zurueckgeschrieben). Dieser
    /// Lauf holt genau diese Nachzuegler — "mehr konkrete, richtige Beispiele" in der durchsuchbaren KB.
    /// Rein additiv: Eval-Schutz und IsIndexWorthy greifen ueber den bestehenden Index-Pfad weiter.
    /// </summary>
    [RelayCommand]
    private async Task ReconcileGoldToKbAsync()
    {
        // Nebenlaeufigkeits-Guard: nie parallel zu Batch/Self-Training oder mehrfach gleichzeitig —
        // sonst koennten Backup, JSON-Status und KB-Index gegeneinander arbeiten.
        await TrainingGoldKbReconcileCommandWorkflow.RunAsync(
            TrainingGoldKbReconcileCommandRequestFactory.CreateWithDefaults(new TrainingGoldKbReconcileCommandDefaultRequestFactoryRequest(
                GetIsBusy: () => IsBusy,
                GetIsSelfTrainingRunning: () => IsSelfTrainingRunning,
                ResetCancellation: ResetGenerationCancellation,
                SetBusy: value => IsBusy = value,
                IndexAsync: IncrementalKbUpdateWithReasonAsync,
                Log: Log,
                SetStatus: SetStatus,
                OnUi: OnUi,
                ExportBackupAsync: _knowledgeBackup.ExportAsync))).ConfigureAwait(false);
    }


    // ── Review Queue (Self-Improving Loop) ──────────────────────────────

    /// <summary>Fuehrt UI-gebundene Aenderungen (ObservableCollection/Status) auf dem Dispatcher-Thread aus.</summary>
    private void OnUi(Action action)
    {
        _uiThread.Run(action);
    }

    // Thread-sicheres Setzen von StatusText: in async-Methoden mit ConfigureAwait(false)
    // laeuft die Fortsetzung auf einem Worker-Thread; ein direktes StatusText = ... waere ein
    // UI-Update vom falschen Thread (WPF-Crash-Risiko). Daher ueber den Dispatcher (OnUi).
    private void SetStatus(string text) => OnUi(() => StatusText = text);

    /// <summary>Loads pending review items into the queue.</summary>
    public void LoadReviewQueue(InfraSelfImproving.ReviewQueueService queueService)
    {
        TrainingReviewQueueLoadWorkflow.Run(
            TrainingReviewQueueLoadRequestFactory.Create(new TrainingReviewQueueLoadRequestFactoryRequest(
                queueService,
                ReviewQueue,
                value => ReviewQueueCount = value,
                value => ReviewStatusText = value,
                OnUi)));
    }


    /// <summary>
    /// Loest die SampleId eines Self-Training-Review-Items auf: bevorzugt die direkte SampleId,
    /// sonst (Altbestand ohne SampleId) ueber Fuzzy-Match CaseId/Code/Meter±0.2. Null = nicht gefunden.
    /// </summary>
    private async Task<string?> ResolveSelfTrainingSampleIdAsync(InfraSelfImproving.ReviewQueueItem item)
    {
        return await TrainingReviewSampleIdResolutionWorkflow.ResolveWithDefaultsAsync(item).ConfigureAwait(false);
    }

    /// <summary>
    /// Approve a review item (accept the suggested code).
    /// Optionaler <paramref name="box"/>-Parameter: vom Reviewer gezeichnete YOLO-Box (B5).
    /// </summary>
    public async Task ApproveReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default,
        BoundingBox? box = null,
        TrainingSegmentationMask? mask = null)
    {
        await RunReviewItemDecisionAsync(
            item,
            feedback,
            queueService,
            TrainingReviewItemDecision.Approve,
            correctedCode: "",
            correctedDescription: null,
            ct,
            box,
            mask).ConfigureAwait(false);
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default,
        string? correctedDescription = null)
    {
        await RunReviewItemDecisionAsync(
            item,
            feedback,
            queueService,
            TrainingReviewItemDecision.Reject,
            correctedCode,
            correctedDescription,
            ct,
            box: null,
            mask: null).ConfigureAwait(false);
    }

    private Task RunReviewItemDecisionAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        TrainingReviewItemDecision decision,
        string correctedCode,
        string? correctedDescription,
        CancellationToken ct,
        BoundingBox? box,
        TrainingSegmentationMask? mask)
        => TrainingReviewItemDecisionCommandWorkflow.RunAsync(
            TrainingReviewItemDecisionCommandRequestFactory.Create(new TrainingReviewItemDecisionCommandRequestFactoryRequest(
                Item: item,
                Feedback: feedback,
                QueueService: queueService,
                Decision: decision,
                CorrectedCode: correctedCode,
                CorrectedDescription: correctedDescription,
                CancellationToken: ct,
                Box: box,
                Mask: mask,
                ReviewQueue: ReviewQueue,
                ResolveSampleIdAsync: ResolveSelfTrainingSampleIdAsync,
                IndexSamplesAsync: (samples, token) => IncrementalKbUpdateWithReasonAsync(samples.ToList(), token),
                DeindexSample: TryDeindexSample,
                ReloadSamplesAsync: LoadSamplesInternalAsync,
                OnUi: OnUi,
                SetReviewQueueCount: value => ReviewQueueCount = value,
                SetReviewStatusText: value => ReviewStatusText = value,
                Log: Log)));

    // ── Review Queue Commands ────────────────────────────────────────────

    private bool HasSelectedReviewItem => SelectedReviewItem is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedReviewItem))]
    private async Task ApproveSelectedReviewAsync(CancellationToken ct)
    {
        await TrainingSelectedReviewCommandWorkflow.ApproveAsync(
            TrainingSelectedReviewCommandRequestFactory.CreateApproveWithDefaults(
                new TrainingSelectedReviewApproveFactoryRequest(
                    Item: SelectedReviewItem,
                    QueueService: ReviewQueueServiceRef,
                    GetPendingBox: () => PendingBox,
                    GetPendingMask: () => PendingSamMask,
                    ClearPendingReviewGeometry: ClearPendingReviewGeometry,
                    Settings: _settings,
                    ApproveReviewItemAsync: ApproveReviewItemAsync,
                    CancellationToken: ct,
                    Log: Log,
                    OnUi: OnUi,
                    SetReviewStatusText: value => ReviewStatusText = value))).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedReviewItem))]
    private async Task RejectSelectedReviewAsync(CancellationToken ct)
    {
        await TrainingSelectedReviewCommandWorkflow.RejectAsync(
            TrainingSelectedReviewCommandRequestFactory.CreateRejectWithDefaults(
                new TrainingSelectedReviewRejectFactoryRequest(
                    Item: SelectedReviewItem,
                    QueueService: ReviewQueueServiceRef,
                    Settings: _settings,
                    RejectReviewItemAsync: RejectReviewItemAsync,
                    CancellationToken: ct,
                    Log: Log,
                    OnUi: OnUi,
                    SetReviewStatusText: value => ReviewStatusText = value))).ConfigureAwait(false);
    }

    /// <summary>Wendet eine Review-Korrektur an: Original ablehnen+deindexieren, korrigiertes Sample anlegen+indexieren.</summary>
    public async Task ApplyReviewCorrectionAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        CancellationToken ct = default,
        string? correctedDescription = null)
    {
        await TrainingSelectedReviewCommandWorkflow.CorrectAsync(
            TrainingSelectedReviewCommandRequestFactory.CreateCorrectionWithDefaults(
                new TrainingSelectedReviewCorrectionFactoryRequest(
                    Item: item,
                    QueueService: ReviewQueueServiceRef,
                    CorrectedCode: correctedCode,
                    CorrectedDescription: correctedDescription,
                    Settings: _settings,
                    RejectReviewItemAsync: RejectReviewItemAsync,
                    CancellationToken: ct,
                    Log: Log,
                    OnUi: OnUi,
                    SetReviewStatusText: value => ReviewStatusText = value))).ConfigureAwait(false);
    }

    private void ClearPendingReviewGeometry()
    {
        TrainingReviewPendingGeometryController.Clear(
            value => PendingBox = value,
            value => PendingSamMask = value);
    }

    // ── Selbsttraining (Orchestrator) ──────────────────────────────────

    [ObservableProperty] private bool _isSelfTrainingRunning;
    private ISelfTrainingOrchestrator? _selfTrainingOrchestrator;
    private string _activeVisionModel = OllamaConfig.DefaultVisionModel;

    [RelayCommand]
    private async Task RunSelfTrainingAsync()
    {
        await SelfTrainingRunCommandWorkflow.RunAsync(
            SelfTrainingRunCommandRequestFactory.CreateWithDefaults(
                new SelfTrainingRunCommandDefaultRequestFactoryRequest(
                    IsBusy: IsBusy,
                    IsSelfTrainingRunning: IsSelfTrainingRunning,
                    Cases: Cases,
                    RootFolders: _rootFolders,
                    ScanInputsAsync: _import.ScanAsync,
                    SelectedCase: SelectedCase,
                    SetSelectedCase: value => SelectedCase = value,
                    ResetCancellation: _selfTrainingCancellation.Reset,
                    SetStatusText: value => StatusText = value,
                    SetBusy: value => IsBusy = value,
                    SetSelfTrainingRunning: value => IsSelfTrainingRunning = value,
                    SetLogText: value => LogText = value,
                    Log: Log,
                    GetKbHttpClient: () => _kbHttpClient,
                    SetKbHttpClient: value => _kbHttpClient = value,
                    AppSettings: _settings,
                    CodeCatalog: _codeCatalog,
                    SetActiveVisionModel: value => _activeVisionModel = value,
                    SetOrchestrator: value => _selfTrainingOrchestrator = value,
                    OnProgress: OnSelfTrainingStep,
                    IndexSamplesAsync: IncrementalKbUpdateWithReasonAsync,
                    ReviewQueueService: ReviewQueueServiceRef,
                    ReloadReviewQueue: LoadReviewQueue,
                    LoadSamplesInternalAsync: LoadSamplesInternalAsync,
                    RefreshKbStatusAsync: RefreshKbStatusAsync,
                    ResetVisuals: () => ResetSelfTrainingVisuals(resetMatchRate: true)))).ConfigureAwait(false);
    }

    /// <summary>
    /// Indexiert Samples inkrementell in die KB und liefert ein <see cref="KbIndexOutcome"/>, das
    /// erfolgreich indexierte von bewusst/dauerhaft uebersprungenen (Skipped: Eval-Schutz/nicht
    /// index-wuerdig) Samples unterscheidet. Alles, was in keiner der Listen steht, ist ein echter
    /// (transienter) Fehler -> der Aufrufer setzt KbIndexState.Error. Einziger KB-Index-Pfad fuer
    /// Review-Approval, Backfill und Self-Training.
    /// </summary>
    private async Task<KbIndexOutcome> IncrementalKbUpdateWithReasonAsync(List<TrainingSample> samples, CancellationToken ct)
    {
        return await TrainingKnowledgeBaseIndexWorkflow.RunWithDefaultsAsync(
            samples,
            ct,
            () => _kbHttpClient,
            value => _kbHttpClient = value,
            _settings,
            Log);
    }

    [RelayCommand]
    private void StopSelfTraining()
    {
        SelfTrainingRunControlController.Stop(
            _selfTrainingCancellation.Cancel,
            value => StatusText = value);
    }

    [RelayCommand]
    private void PauseSelfTraining()
    {
        SelfTrainingRunControlController.TogglePause(
            _selfTrainingOrchestrator,
            value => StatusText = value,
            Log);
    }

    // ── Protokoll-Startdaten (B6) ────────────────────────────────────────

    /// <summary>
    /// Reiht gepruefte Protokoll-Samples (Status=New, katalog-gueltig) als Startdaten-Kandidaten
    /// in die Review Queue ein. Keine KI-Analyse — Freigabe nur ueber ApproveReviewItemAsync.
    /// Bereits eingereihte Samples werden per SampleId dedupliziert.
    /// </summary>
    [RelayCommand]
    private async Task SuggestProtocolStartdataAsync()
    {
        await TrainingProtocolStartdataSuggestionWorkflow.RunAsync(
            TrainingProtocolStartdataSuggestionRequestFactory.CreateWithDefaults(
                new TrainingProtocolStartdataSuggestionRequestFactoryRequest(
                    ReviewQueueServiceRef,
                    _codeCatalog,
                    ReloadCurrentReviewQueue,
                    OnUi,
                    value => ReviewStatusText = value,
                    Log))).ConfigureAwait(false);
    }

    private void ReloadCurrentReviewQueue()
        => TrainingReviewQueueReloadController.Reload(
            ReviewQueueServiceRef,
            LoadReviewQueue);

    /// <summary>Anzahl der aktuell als Protokoll-Startdaten eingereihten Kandidaten.</summary>
    public int StartdataCandidateCount =>
        TrainingProtocolStartdataReviewItemSelector.Count(ReviewQueue);

    private List<InfraSelfImproving.ReviewQueueItem> GetProtocolStartdataReviewItems()
        => TrainingProtocolStartdataReviewItemSelector.SelectOnUi(ReviewQueue, OnUi);

    /// <summary>Gibt ALLE Protokoll-Startdaten-Kandidaten frei (nach expliziter Bestaetigung im View).</summary>
    public async Task ApproveAllStartdataAsync(CancellationToken ct = default)
    {
        await TrainingProtocolStartdataApprovalWorkflow.RunAsync(
            TrainingProtocolStartdataApprovalRequestFactory.CreateWithDefaults(
                new TrainingProtocolStartdataApprovalRequestFactoryRequest(
                    ReviewQueueServiceRef,
                    GetProtocolStartdataReviewItems,
                    _settings,
                    ApproveReviewItemAsync,
                    ct,
                    Log,
                    OnUi,
                    value => ReviewStatusText = value))).ConfigureAwait(false);
    }
}
