using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow : Window
{
    public PlayerWindow(
        string videoPath,
        PlayerWindowOptions? options = null,
        string? initialOverlayText = null,
        PlayerDamageOverlayData? damageOverlay = null,
        ServiceProvider? serviceProvider = null,
        string? haltungId = null,
        Action<ProtocolEntry>? onEntryCreated = null,
        HaltungRecord? haltungRecord = null)
    {
        // Frueh pruefen, bevor irgendein Zustand gesetzt wird:
        // wirft der Konstruktor spaeter, bliebe sonst ein halb-konstruiertes Fenster zurueck.
        var videoInfo = PlayerVideoPathGuard.Validate(videoPath);

        var codingSessionRuntime = CodingSessionRuntimeFactory.Create(
            CodingVm_PropertyChanged,
            () => _codingOverlayRuntimeOwner.Service);
        _codingSessionViewModelOwner = codingSessionRuntime.ViewModelOwner;
        _codingSessionHost = codingSessionRuntime.SessionHost;
        _codingOverlayToolHost = codingSessionRuntime.OverlayToolHost;
        _codingFindingContext = CodingFindingContext.CreateDefault(
            () => _codingSessionRuntimeOwner.Service?.ActiveSession?.Events,
            () => _codingSessionHost.Events,
            () => _codingImportReferenceEvents.Events,
            message => PlayerTrace.WriteLine(message));
        _codingAnalysisContext = CodingAnalysisContext.CreateDefault(
            () => _codingSessionRuntimeOwner.Service?.ActiveSession?.Events,
            () => _codingSessionHost.Events,
            () => _codingImportReferenceEvents.Events,
            () => _codingOverlayToolHost.Calibration,
            () => _codingOverlayRenderState.VideoAspect,
            path => TakeSnapshotSafe(path));
        _codingBoundaryContext = new CodingBoundaryContext(
            new CodingBoundaryContextSources(
                HasCodingViewModel: () => _codingSessionHost.HasViewModel,
                ViewEvents: () => _codingSessionHost.EventCollection,
                SessionEvents: () => _codingSessionRuntimeOwner.Service?.ActiveSession?.Events ?? [],
                ImportEvents: () => _codingImportReferenceEvents.Events,
                CodingSessionService: () => _codingSessionRuntimeOwner.Service,
                FirstCleanFrameSeconds: () => _codingFrameReadinessController.FirstCleanFrameSeconds,
                OsdMeter: () => _codingOsdMeterController.LastMeter,
                ViewModelEndMeter: () => _codingSessionHost.EndMeter,
                FallbackVideoTime: () => _playerTimelineHost.CurrentTimeOrZero),
            new CodingBoundaryEventWorkflowActions(
                VsaCodeResolver.LookupLabel,
                message => PlayerTrace.WriteLine(message),
                TryExtractFrameAtSecondsAsync,
                (entry, frameBytes) => AttachBoundaryAnalyzedFramePhoto(entry, frameBytes),
                () => TryAutoCalibrationFromCurrentFrame().SafeFireAndForget("TryAutoCalibration"),
                RefreshCodingEventsList));

        InitializeComponent();
        _liveDetectionPulseController = new LiveDetectionPulseController(
            _codingAiPulseStateController,
            new LiveDetectionPulseControllerActions(
                StartAnimation: () => LiveDetectionPulseControls.Start(CodingAiPulseRing),
                StopAnimation: () => LiveDetectionPulseControls.Stop(CodingAiPulseRing)));
        _liveDetectionStatusController = new LiveDetectionStatusController(
            new LiveDetectionStatusControllerActions(
                HasDispatcherAccess: () => PlayerDispatcherScheduler.HasAccess(Dispatcher),
                DispatchToUi: action => PlayerDispatcherScheduler.Invoke(Dispatcher, action),
                ShowLiveDetectionBadge: (status, color, stage) =>
                    LiveDetectionStatusControls.ShowLiveDetectionBadge(
                        AiStatusBadge,
                        AiStatusText,
                        AiStatusDot,
                        status,
                        color,
                        stage),
                ShowYoloStatus: (status, color, model) => LiveDetectionStatusControls.ShowYoloStatus(
                    YoloStatusBar,
                    TxtYoloStatus,
                    YoloDot,
                    TxtYoloModel,
                    status,
                    color,
                    model),
                ShowCodingAiState: (status, color, stage) => LiveDetectionStatusControls.ShowCodingAiState(
                    TxtCodingAiStatus,
                    TxtCodingAiStage,
                    CodingAiDot,
                    status,
                    color,
                    stage),
                StartPulse: _liveDetectionPulseController.Start,
                StopPulse: _liveDetectionPulseController.Stop,
                ShowDetectionStatus: result => LiveDetectionStatusControls.ShowDetectionStatus(
                    LiveDetectionStatusText,
                    FindingSummaryPanel,
                    FindingSummaryText,
                    result)));
        _codingSchemaOverlayController = new CodingSchemaOverlayController(
            _codingSchemaManager,
            _codingSchemaTypeState,
            _codingSessionHost,
            _codingOverlayToolHost,
            new CodingSchemaOverlayControllerActions(
                CaptureMouse: () => CodingOverlayInputControls.CaptureCanvasMouse(CodingOverlayCanvas),
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(CodingOverlayCanvas),
                UpdateOverlayInfo: UpdateCodingOverlayInfo,
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                RenderActiveCodingSchema: RenderActiveCodingSchema,
                RedrawCodingCanvas: RedrawCodingCanvas));
        _codingManualCalibrationController = new CodingManualCalibrationController(
            _codingCalibrationState,
            _codingActiveToolNameState,
            _codingSchemaManager,
            _codingSessionHost,
            _codingOverlayToolHost,
            new CodingManualCalibrationControllerActions(
                CloseToolsDropdown: () => CodingOverlayInputControls.ClosePopup(ToolsDropdownPopup),
                ApplyActiveToolSelection: label => CodingOverlayInputControls.ApplyActiveToolSelection(
                    TxtActiveToolLabel,
                    BtnCodingCreateEvent,
                    label),
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null),
                ApplyToggleControls: state => CodingCalibrationControls.ApplyToggle(
                    CodingCalibrationHint,
                    TxtCodingCalibHint,
                    state),
                UpdateOverlayCursor: UpdateCodingOverlayCursor,
                RedrawCodingCanvas: RedrawCodingCanvas,
                MapToPixel: CodingNormToPixel,
                ShowInvalidHint: text => CodingCalibrationControls.ShowHint(TxtCodingCalibHint, text),
                ApplyManualResult: result => CodingCalibrationControls.ApplyManualResult(
                    TxtCodingCalibStatus,
                    TxtCodingCalibHint,
                    result),
                HideHint: () => CodingCalibrationControls.HideHint(CodingCalibrationHint),
                EnableCodingSchemaOverlay: () => UpdateCodingSchemaOverlay(enableCreateEvent: true)));
        _codingCalibrationPointerController = new CodingCalibrationPointerController(
            _codingCalibrationState,
            new CodingCalibrationPointerControllerActions(
                CaptureMouse: () => CodingOverlayInputControls.CaptureCanvasMouse(CodingOverlayCanvas),
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(CodingOverlayCanvas),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                RenderPreview: (start, current) =>
                    _codingOverlayRenderController.RenderCalibrationPreview(start, current),
                ApplyPreview: preview => CodingCalibrationControls.ApplyPreview(TxtCodingCalibHint, preview),
                ApplyCalibration: (start, end) => _codingManualCalibrationController.Apply(start, end)));
        WireCodingSidePanelEvents();
        InitializeCodingSidePanelControllers();
        _codingEventsRefreshController = new CodingEventsRefreshController(
            _codingSessionHost,
            _codingSidePanelControllers.EventsList,
            _codingSidePanelControllers.Statistics,
            CodingSessionViewModel.GetDefectStatus,
            new CodingEventsRefreshControllerActions(
                ScheduleLoaded: action => PlayerDispatcherScheduler.ScheduleLoaded(Dispatcher, action),
                ColorizeListItems: ColorizeCodingEventListItems));
        PlayerCodingConfirmationPanelInitializer.Initialize(
            _codingConfirmationPanelControls,
            CodingConfirmationPanel,
            ConfirmAmpel,
            TxtConfirmCode,
            TxtConfirmConfidence,
            TxtConfirmDescription,
            TxtConfirmDetail);
        PlayerWindowStateControls.Track(this);

        _playbackContext = PlayerWindowPlaybackContext.From(videoInfo, initialOverlayText, damageOverlay);
        var normalizedOptions = PlayerWindowOptions.Normalize(options);
        _protocolContext = PlayerWindowProtocolContext.From(
            serviceProvider,
            haltungId,
            onEntryCreated,
            haltungRecord);
        _codingPipelineHealthController = new CodingPipelineHealthController(
            _codingAiRuntimeOwner.Controller,
            new CodingPipelineHealthControllerActions(
                CreateRuntime: () => CodingAiRuntimeCreationWorkflow.Create(
                    CodeCatalog,
                    _protocolContext.PipelineConfig),
                CreateHealthMonitor: runtime => CodingAiHealthMonitorCreationWorkflow.Create(
                    runtime,
                    aiEnabled: () => _codingAiRuntimeOwner.Controller.AiEnabled,
                    qwenAvailable: () => _codingAiRuntimeOwner.Controller.QwenAvailable),
                IsClosing: () => _shutdownState.IsClosing,
                DispatcherHasShutdownStarted: () => PlayerDispatcherScheduler.HasShutdownStarted(Dispatcher),
                HasDispatcherAccess: () => PlayerDispatcherScheduler.HasAccess(Dispatcher),
                IsCodingMode: () => _codingModeState.IsCodingMode,
                DispatchToUi: action => PlayerDispatcherScheduler.ScheduleNormal(Dispatcher, action),
                SetCodingAiState: (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail),
                SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                SetYoloStatus: (status, color, model) => _liveDetectionStatusController.SetYoloStatus(status, color, model),
                UpdatePipelineHealthDetails: details => LiveDetectionStatusControls.ShowPipelineHealthDetails(
                    Hd_Sidecar,
                    Hd_Token,
                    Hd_Yolo,
                    Hd_Dino,
                    Hd_Sam,
                    Hd_Mode,
                    details)));
        var playerSettings = _protocolContext.Settings ?? AppSettings.Load();
        WireCodingPhotoHoverPreview();
        _codingTrainingPersistenceContext = CodingTrainingPersistenceContext.CreateDefault(
            () => _codingSessionRuntimeOwner.Service,
            _protocolContext.Settings,
            () => _codingSessionHost.HasViewModel,
            () => _liveDetectionController.PendingConfirmationFrameBytes,
            () => _codingSessionHost.HaltungName ?? "unknown",
            () => _protocolContext.HaltungRecord?.GetFieldValue("Datum_Jahr"),
            CaptureCurrentFrameAsync);

        PlayerWindowHeaderControls.ApplyVideoInfo(this, VideoNameText, VideoPathText, videoInfo);

        _playerMediaRuntime = PlayerMediaRuntimeFactory.Create(normalizedOptions);
        _playerMediaRuntime.AttachVideoView(VideoView);
        _playerMediaHosts = _playerMediaRuntime.Hosts;
        _playerControllers = PlayerWindowControllerSetInitializer.Create(
            this,
            new PlayerWindowControllerSetDependencies(
                DamageOverlay: _playbackContext.DamageOverlay,
                PlaybackControlHost: _playerPlaybackControlHost,
                TimelineHost: _playerTimelineHost,
                PlayerSettings: playerSettings,
                VideoPath: _playbackContext.VideoPath,
                EnsurePlaying: EnsurePlaying,
                UpdateUi: UpdateUi,
                ShowUnsupportedRate: PlayerPlaybackDialogWorkflow.ShowUnsupportedRate,
                ResolveSliderTrackBounds: () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas),
                MapCodingOverlayPoint: CodingNormToPixel));
        _liveDetectionStopController = new LiveDetectionStopController(
            new LiveDetectionStopControllerSources(
                StopRuntime: _liveDetectionController.Stop,
                ShouldUpdateUi: () => !_shutdownState.IsUnavailable,
                HideOverlay: () => !_liveDetectionController.IsManualMarkMode,
                GetTotalEvents: () => _codingSessionHost.EventCollection?.Count ?? 0,
                HasPlayer: () => !_shutdownState.IsPlaybackDisposed,
                IsPlaybackDisposed: () => _shutdownState.IsPlaybackDisposed,
                IsPlayerPlaying: () => !_shutdownState.IsPlaybackDisposed && _playerPlaybackControlHost.IsPlaying,
                IsDetecting: () => _liveDetectionController.IsDetecting),
            new LiveDetectionStopControllerActions(
                SetStoppedStatus: () => _liveDetectionStatusController.SetYoloStatus(
                    "Gestoppt",
                    PlayerStatusColors.Muted),
                ClearOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                    DetectionCanvas,
                    DetectionOverlayGrid,
                    hideOverlay),
                ShowStoppedDetectionStatus: totalEvents => LiveDetectionStatusControls.ShowStoppedDetectionStatus(
                    AiStatusBadge,
                    FindingSummaryPanel,
                    LiveDetectionStatusText,
                    totalEvents),
                SetPause: _playerPlaybackControlHost.SetPause,
                ScheduleHideStatusTimer: actions => LiveDetectionHideStatusTimerWorkflow.Schedule(actions),
                HideDetectionStatus: () => LiveDetectionStatusControls.HideDetectionStatus(LiveDetectionStatusText)));
        _liveDetectionLifecycleController = new LiveDetectionLifecycleController(
            new LiveDetectionLifecycleControllerActions(
                IsDetecting: () => _liveDetectionController.IsDetecting,
                StopLiveDetection: _liveDetectionStopController.Stop,
                UncheckToggle: () => LiveDetectionToggleControls.Uncheck(LiveDetectionButton),
                StartWithDisplayAsync: LiveDetectionStartupDisplayWorkflow.StartAsync,
                StartRuntime: _liveDetectionController.StartRuntime,
                ShowOverlay: () => LiveDetectionOverlayControls.Show(DetectionOverlayGrid),
                ApplyActiveStatus: status =>
                {
                    _liveDetectionStatusController.SetLiveDetectionBadge(
                        status.BadgeText,
                        status.StatusColor,
                        status.BadgeDetails);
                    _liveDetectionStatusController.SetYoloStatus(
                        status.YoloText,
                        status.StatusColor,
                        status.ModelLabel);
                },
                ShowWaitingForFrame: () => LiveDetectionStatusControls.ShowWaitingForFrame(LiveDetectionStatusText),
                TimerTick: DetectionTimer_Tick,
                RunFirstDetection: () => RunDetectionAsync().SafeFireAndForget("LiveDetection")));
        _liveDetectionMarkToolController = new LiveDetectionMarkToolController(
            new LiveDetectionMarkToolControllerBindings(
                ToggleManualMarkPopup: _markToolControls.ToggleManualMarkPopup,
                ToggleToolsDropdown: _markToolControls.ToggleToolsDropdown,
                CreateActivationActions: ensureOverlayReady => new LiveDetectionManualMarkActivationWorkflowActions(
                    BeginActivation: _markToolControls.BeginActivation,
                    SetMarkToolType: _liveDetectionController.SetMarkToolType,
                    SetPause: _playerPlaybackControlHost.SetPause,
                    CancelSchema: _codingSchemaManager.Cancel,
                    ClearSchemaType: _codingSchemaTypeState.Clear,
                    SetManualMarkMode: _liveDetectionController.SetManualMarkMode,
                    ActivatePointTool: _markToolControls.ActivatePointTool,
                    EnsureOverlayReady: ensureOverlayReady,
                    SetActiveTool: selectedTool => _codingOverlayToolHost.SetActiveTool(selectedTool),
                    ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                    OpenCodingOverlay: _markToolControls.OpenCodingOverlay,
                    UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
                    EnableCodingOverlayInput: _markToolControls.EnableCodingOverlayInput),
                CreateOverlayReadyRequest: () => new LiveDetectionMarkOverlayReadyStateRequest(
                    _codingOverlayRuntimeOwner.HasService,
                    _codingSessionHost.HasViewModel,
                    _playbackContext.VideoPath,
                    _protocolContext.Settings,
                    _codingSessionRuntimeOwner.Service,
                    _codingOverlayRuntimeOwner.Service),
                OverlayReadyActions: new LiveDetectionMarkOverlayReadyApplyActions(
                    SetSessionService: _codingSessionRuntimeOwner.Set,
                    SetOverlayService: _codingOverlayRuntimeOwner.Set,
                    SetViewModel: viewModel => _codingSessionViewModelOwner.Set(
                        viewModel,
                        observePropertyChanged: false)),
                CreateDeactivationRequest: () => new LiveDetectionManualMarkDeactivationWorkflowRequest(
                    _codingModeState.IsCodingMode,
                    _liveDetectionController.IsDetecting),
                DeactivationActions: new LiveDetectionManualMarkDeactivationWorkflowActions(
                    SetMarkToolType: _liveDetectionController.SetMarkToolType,
                    SetManualMarkMode: _liveDetectionController.SetManualMarkMode,
                    ResetToolLabel: _markToolControls.ResetToolLabel,
                    DeactivateDetectionSide: _markToolControls.DeactivateDetectionSide,
                    CancelSchema: _codingSchemaManager.Cancel,
                    CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                    SetActiveTool: tool => _codingOverlayToolHost.SetActiveTool(tool),
                    DeactivateCodingOverlay: _markToolControls.DeactivateCodingOverlay)));
        _liveDetectionMarkSegmentationController = new LiveDetectionMarkSegmentationController(
            new LiveDetectionMarkSegmentationControllerBindings(
                HasBoxSegmentation: () => _codingAiRuntimeOwner.Controller.BoxSegmentation is not null,
                SegmentBoxAsync: (frameBytes, box, dn, calibration) =>
                    _codingAiRuntimeOwner.Controller.BoxSegmentation!.SegmentBoxAsync(
                        frameBytes,
                        box,
                        dn,
                        calibration,
                        System.Threading.CancellationToken.None),
                GetCalibration: () => _codingOverlayToolHost.Calibration,
                GetContentRect: GetCodingContentRect,
                ShowBendMarker: (x, y, rect) => CodingBendMarkerOverlayController.Show(
                    CodingOverlayCanvas,
                    x,
                    y,
                    rect),
                RenderMasks: (samResponse, quantifications, rect) => CodingSamMaskOverlayController.RenderMasks(
                    CodingOverlayCanvas,
                    samResponse,
                    quantifications,
                    rect),
                TraceError: message => PlayerTrace.WriteLine(message)));
        _codingApplyController = new CodingApplyController(
            new CodingApplyControllerBindings(
                HasCodingViewModel: () => _codingSessionHost.HasViewModel,
                GetHaltungRecord: () => _protocolContext.HaltungRecord,
                GetEventCollection: () => _codingSessionHost.EventCollection,
                GetEvents: () => _codingSessionHost.Events,
                IsCodingMode: () => _codingModeState.IsCodingMode,
                GetBaselineSignature: () => _codingBaselineSignatureState.BaselineSignature,
                ConfirmEmptyProtocol: CodingApplyEmptyProtocolDialogWorkflow.Execute,
                AssignProtocol: document => _protocolContext.HaltungRecord!.Protocol = document,
                MarkProjectDirty: CodingProjectPersistenceWorkflow.MarkProjectDirty,
                SyncCodingToPrimaryDamages: SyncCodingToPrimaryDamages,
                PersistCodingEventsAsTrainingSamples: _codingTrainingPersistenceContext.PersistEvents,
                SetBaselineSignature: _codingBaselineSignatureState.Set,
                // Ohne Projektpfad darf beim Codieren kein Speichern-unter-Dialog erscheinen.
                SaveProjectAfterCoding: CodingProjectPersistenceWorkflow.TrySaveProjectIfReady,
                ShowOverlay: ShowOverlay,
                ConfirmUnappliedChanges: applyChanges => CodingUnappliedChangesCloseDialogWorkflow.Execute(
                    runWithSuspendedOverlay: callback => RunWithSuspendedCodingOverlayInput(callback),
                    applyChanges: applyChanges)));
        _codingModeExitController = new CodingModeExitController(
            new CodingModeExitControllerBindings(
                IsCodingMode: () => _codingModeState.IsCodingMode,
                SetCodingMode: _codingModeState.Set,
                CreateFinalizationRequest: () => new CodingModeExitFinalizationWorkflowRequest(
                    _codingSessionHost.EventCollection,
                    _codingOsdMeterController.LastMeter,
                    _codingSessionHost.EndMeter,
                    _playerTimelineHost.DurationTimeOrZero,
                    _liveDetectionController.PendingConfirmationFrameBytes),
                FinalizationActions: new CodingModeExitFinalizationWorkflowActions(
                    CloseTrackedStreckenschaeden,
                    CloseOpenStreckenschaeden,
                    (meter, _, frameBytes) => _codingBoundaryContext.EnsureEnd(meter, frameBytes)),
                CreateTeardownRequest: () => new CodingModeExitTeardownWorkflowRequest(
                    HasCodingLiveAiTimers: _codingLiveAiTimerOwner.HasController,
                    HasCodingViewModel: _codingSessionHost.HasViewModel,
                    IsLiveDetectionRunning: _liveDetectionController.IsDetecting),
                TeardownActions: new CodingModeExitTeardownWorkflowActions(
                    StopCodingOsdTimer: StopCodingOsdTimer,
                    DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                    StopCodingLiveAiTimers: _codingLiveAiTimerOwner.Stop,
                    StopCodingAiPulse: _liveDetectionPulseController.Stop,
                    StopPipelineHealthMonitor: _codingPipelineHealthController.Stop,
                    DisposeAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation,
                    ClearImportReferenceEvents: () => CodingImportReferenceStateResetter.ClearEvents(_codingImportReferenceEvents.Events),
                    ResetProtocolMatchState: () =>
                    {
                        _codingProtocolMatchState.Reset();
                    },
                    UpdateProtocolMatchSummary: () => UpdateCodingProtocolMatchSummary(_codingProtocolMatchState.LastMatch),
                    ClearImportEventsListSource: () => CodingImportReferenceControls.ClearItemsSource(LstImportEvents),
                    HideConfirmationPanels: () => CodingModeChromeControls.HideConfirmationPanels(
                        CodingConfirmationPanel,
                        DetectionConfirmationPanel),
                    ClearPendingConfirmation: _codingPendingConfirmationState.Clear,
                    ClearDetectionConfirmationBuffer: _liveDetectionController.ClearConfirmationBuffer,
                    ClearDetectionOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                        DetectionCanvas,
                        DetectionOverlayGrid,
                        hideOverlay),
                    HideCodingSurface: () => CodingModeChromeControls.HideCodingSurface(
                        CodingOverlayPopup,
                        CodingOverlayCanvas,
                        CodingSidePanel,
                        CodingSidePanelColumn,
                        CodingToolbar,
                        CodingTimelinePanel,
                        CodingCalibrationHint,
                        CodingMeasurementPanel),
                    HideInlineDefectDetail: HideInlineDefectDetail,
                    HideOsdBadge: () => CodingOsdBadgeControls.Hide(OsdMeterBadge),
                    ShowLiveDetectionEntry: isDetecting => CodingModeChromeControls.ShowLiveDetectionEntry(
                        LiveDetectionButton,
                        LiveDetectionStatusText,
                        isDetecting),
                    ClearActiveCodingToolName: _codingActiveToolNameState.Clear,
                    ResetCodingIndicators: () => CodingModeChromeControls.ResetCodingIndicators(
                        TxtActiveToolLabel,
                        BtnCodingLiveAi,
                        TxtCodingAiStage),
                    CancelCodingSchema: _codingSchemaManager.Cancel,
                    ClearCodingSchemaType: _codingSchemaTypeState.Clear,
                    DetachCodingViewModelPropertyChanged: _codingSessionViewModelOwner.DetachPropertyChanged,
                    ClearCodingSessionReferences: () =>
                    {
                        _codingSessionViewModelOwner.Clear();
                        _codingSessionRuntimeOwner.Clear();
                        _codingOverlayRuntimeOwner.Clear();
                    },
                    ClearCodingCalibrationState: _codingCalibrationState.Reset,
                    ResetFrameReadiness: ResetFrameReadiness,
                    ResetCodingOverlaySuspendState: _codingOverlayInputVisibilityState.ResetSuspendState)));
        _playerSliderInputController = new PlayerSliderInputController(_playerControllers);
        var liveDetectionTrainingControllers = LiveDetectionTrainingControllerSetFactory.Create(
            new LiveDetectionTrainingControllerSetDependencies(
                DetectionController: _liveDetectionController,
                TimelineHost: _playerTimelineHost,
                Owner: this,
                VideoPath: _playbackContext.VideoPath,
                ResolveAutomaticMeter: () => _codingOsdMeterController.LastMeter ?? GetMeterFromVideoPosition(),
                CreateCorrectionViewModel: CreateVsaCodeExplorerViewModel,
                CreateManualSelectionActions: CreateCodingCodeExplorerSeedSelectionActions,
                ResolveDisplayedMeterText: () => TxtCodingMeter?.Text,
                ResolveCodingSessionService: () => _codingSessionHost.HasViewModel
                    ? _codingSessionRuntimeOwner.Service
                    : null,
                CaptureCurrentFrameAsync: CaptureCurrentFrameAsync,
                RefreshCodingEvents: RefreshCodingEventsList,
                ShowOsdMeterStatus: ShowOsdMeterStatus,
                ResumeDetection: ResumeDetection));
        _liveDetectionConfirmationTrainingController = liveDetectionTrainingControllers.Confirmation;
        _liveDetectionManualMarkTrainingController = liveDetectionTrainingControllers.ManualMark;
        var codingConfirmationDecisionController = new CodingConfirmationDecisionController(
            _codingPendingConfirmationState,
            new CodingConfirmationDecisionControllerActions(
                ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service,
                ResolveCodingEvents: () => _codingSessionHost.EventCollection,
                PersistTrainingSample: (codingEvent, operation) =>
                    _codingTrainingPersistenceContext.PersistSingleEventAsync(codingEvent).SafeFireAndForget(operation),
                RefreshCodingEvents: RefreshCodingEventsList,
                HideConfirmationPanel: _codingConfirmationPanelControls.Hide,
                SelectEvent: _codingSidePanelControllers.EventsList.SelectEvent,
                IsLiveAiEnabled: () => PlayerToggleButtonControls.IsChecked(BtnCodingLiveAi),
                ResolveModelName: () => _codingAiRuntimeOwner.Controller.ModelName,
                SetPause: _playerPlaybackControlHost.SetPause,
                ApplyResumeStatus: status => _liveDetectionStatusController.SetCodingAiState(
                    status.StatusText,
                    PlayerStatusColors.Success,
                    status.DetailText)));
        _codingConfirmationController = new CodingConfirmationController(
            _codingPendingConfirmationState,
            new CodingConfirmationControllerBindings(
                ResolveCurrentStatusText: () => TxtCodingAiStatus.Text,
                ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service,
                SetPause: _playerPlaybackControlHost.SetPause,
                ApplyConfirmationPanel: _codingConfirmationPanelControls.Apply,
                ShowStatus: (status, color, detail) =>
                    _liveDetectionStatusController.SetCodingAiState(status, color, detail),
                Accept: codingConfirmationDecisionController.Accept,
                Edit: codingConfirmationDecisionController.Edit,
                Reject: codingConfirmationDecisionController.Reject));
        _codingNavigationController = new CodingNavigationController(
            _codingSessionHost,
            _codingNavigationPendingState,
            _codingOsdMeterController,
            _playerTimelineHost,
            new CodingNavigationControllerActions(
                ApplyMeterTimeline: meter => CodingMeterTimelineControls.Apply(TxtCodingMeter, PipeTimeline, meter),
                UpdateOverlayInfo: UpdateCodingOverlayInfo,
                ApplyCurrentCodeState: state => CodingCurrentCodeBadgeControls.Apply(
                    CodingCurrentCodeBadge,
                    TxtCodingCurrentCode,
                    state),
                UpdateStatistics: UpdateCodingStatistics,
                PausePlayback: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                ReadOsdMeterAsync: CodingReadOsdMeterAsync,
                TraceError: message => PlayerTrace.WriteLine(message)));
        _playerPlaybackController = new PlayerPlaybackController(
            _playbackContext.VideoPath,
            _playerPlaybackControlHost,
            _playerTimelineHost,
            () => _positionSliderStateController.IsDragging,
            () => _codingModeState.IsCodingMode,
            new PlayerPlaybackControllerActions(
                _playerTimerController.StartUpdateTimer,
                _playerControlInputController.UpdateRateLabel,
                ClearDetectionOverlays,
                _positionControls.ApplyPlaybackState,
                UpdateCodingCurrentCode));
        _playerControlInputController.Initialize();
        WirePositionSliderEvents();
        WireWindowLifecycleEvents();
        WireWindowSurfaceEvents();
        WireKeyboardEvents();

        // Erst ganz am Ende setzen: TryShowOverlayOnLast darf nie ein Fenster sehen,
        // dessen Konstruktor fehlgeschlagen ist (Media-Runtime waere dann nicht bereit).
        LastOpenedWindow.Set(this);
    }


}














