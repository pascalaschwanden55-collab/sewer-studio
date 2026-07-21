using System;
using System.Windows;
using AuswertungPro.Next.Application.Ai.Evaluation;
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
        var liveDetectionStatusControllers = PlayerWindowLiveDetectionStatusInitializer.Create(
            new PlayerWindowLiveDetectionStatusControls(
                PulseRing: CodingAiPulseRing,
                Badge: AiStatusBadge,
                BadgeStatusText: AiStatusText,
                BadgeDot: AiStatusDot,
                YoloStatusBar: YoloStatusBar,
                YoloStatusText: TxtYoloStatus,
                YoloDot: YoloDot,
                YoloModelText: TxtYoloModel,
                CodingAiStatusText: TxtCodingAiStatus,
                CodingAiStageText: TxtCodingAiStage,
                CodingAiDot: CodingAiDot,
                DetectionStatusText: LiveDetectionStatusText,
                FindingSummaryPanel: FindingSummaryPanel,
                FindingSummaryText: FindingSummaryText),
            _codingAiPulseStateController,
            Dispatcher);
        _liveDetectionPulseController = liveDetectionStatusControllers.Pulse;
        _liveDetectionStatusController = liveDetectionStatusControllers.Status;
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
        _codingOverlayInputVisibilityController = new CodingOverlayInputVisibilityController(
            _codingOverlayStates.InputVisibilityState,
            new CodingOverlayInputVisibilityControllerBindings(
                IsPopupOpen: () => CodingOverlayInputControls.IsPopupOpen(CodingOverlayPopup),
                HasCurrentOverlay: () => _codingSessionHost.CurrentOverlay is not null,
                EndDrag: _codingSchemaManager.EndDrag,
                CancelDraw: () => _codingOverlayToolHost.CancelDraw(),
                SuspendCanvas: () => CodingOverlayInputControls.SuspendCanvas(CodingOverlayCanvas),
                ResumeCanvas: () => CodingOverlayInputControls.ResumeCanvas(CodingOverlayCanvas),
                OpenPopup: () => CodingOverlayInputControls.OpenPopup(CodingOverlayPopup),
                ClosePopup: () => CodingOverlayInputControls.ClosePopup(CodingOverlayPopup),
                UpdateViewport: UpdateCodingOverlayViewport,
                RedrawCanvas: RedrawCodingCanvas,
                UpdateCursor: UpdateCodingOverlayCursor));
        WireCodingSidePanelEvents();
        InitializeCodingSidePanelControllers();
        _codingEventListVisualController = new CodingEventListVisualController(
            LstCodingEvents,
            LstImportEvents,
            _codingProtocolMatchState);
        _codingEventsRefreshController = new CodingEventsRefreshController(
            _codingSessionHost,
            _codingSidePanelControllers.EventsList,
            _codingSidePanelControllers.Statistics,
            CodingSessionViewModel.GetDefectStatus,
            new CodingEventsRefreshControllerActions(
                ScheduleLoaded: action => PlayerDispatcherScheduler.ScheduleLoaded(Dispatcher, action),
                ColorizeListItems: _codingEventListVisualController.ColorizeCodingEvents));
        _codingStreckenschadenTrackingController = new CodingStreckenschadenTrackingController(
            _codingSessionHost,
            new CodingStreckenschadenTrackingControllerBindings(
                ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service,
                ResolveCode: _codingFindingContext.ResolveCode,
                LookupLabel: _codingFindingContext.LookupLabel,
                AttachAnalyzedFramePhoto: AttachAnalyzedFramePhoto,
                ResolveCurrentVideoTime: () => _playerTimelineHost.CurrentTimeOrZero,
                RefreshEvents: RefreshCodingEventsList));
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
        _codingPhotoAttachmentController = new CodingPhotoAttachmentController(
            new CodingPhotoAttachmentControllerBindings(
                GetPreferredFrameBytesAsync: TryExtractAnalyzedFrameBytesAsync,
                GetBufferedFrameBytes: () => _liveDetectionController.PendingConfirmationFrameBytes,
                AttachAnalyzedFramePhoto: (entry, frameBytes) =>
                    CodingAnalyzedFramePhotoAttacher.AttachWithStore(
                        entry,
                        frameBytes,
                        _playbackContext.VideoPath,
                        _protocolContext.CodingFramePhotos),
                CaptureSnapshot: CodingCaptureSnapshot,
                GetCurrentPlayerTimestamp: GetCurrentPlayerTimestamp,
                ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service,
                ShowOverlay: ShowOverlay,
                RefreshEvents: RefreshCodingEventsList));
        _codingPipelineHealthController = new CodingPipelineHealthController(
            _codingAiRuntimeOwner.Controller,
            new CodingPipelineHealthControllerActions(
                CreateRuntime: () => CodingAiRuntimeCreationWorkflow.Create(
                    CodeCatalog,
                    _protocolContext.PipelineConfig, _protocolContext.SidecarTelemetry),
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
            CaptureCurrentFrameAsync, _protocolContext.TrainingSamples);

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
                MapCodingOverlayPoint: CodingNormToPixel, ProcessOutputs: serviceProvider?.ProcessOutputs, Dialogs: _protocolContext.Dialogs));
        var liveDetectionControllers = PlayerWindowLiveDetectionControllerSetFactory.Create(
            new PlayerWindowLiveDetectionControllerSetDependencies(
                RuntimeController: _liveDetectionController,
                ShutdownState: _shutdownState,
                GetTotalEvents: () => _codingSessionHost.EventCollection?.Count ?? 0,
                PlaybackControlHost: _playerPlaybackControlHost,
                StatusController: _liveDetectionStatusController,
                Controls: new PlayerWindowLiveDetectionLifecycleControls(
                    DetectionCanvas: DetectionCanvas,
                    DetectionOverlay: DetectionOverlayGrid,
                    StatusBadge: AiStatusBadge,
                    FindingSummaryPanel: FindingSummaryPanel,
                    DetectionStatusText: LiveDetectionStatusText,
                    LiveDetectionToggle: LiveDetectionButton),
                TimerTick: DetectionTimer_Tick,
                RunFirstDetection: () =>
                    RunDetectionAsync().SafeFireAndForget("LiveDetection")));
        _liveDetectionStopController = liveDetectionControllers.Stop;
        _liveDetectionLifecycleController = liveDetectionControllers.Lifecycle;
        _liveDetectionMarkToolController = PlayerWindowLiveDetectionMarkToolControllerFactory.Create(
            new PlayerWindowLiveDetectionMarkToolControllerDependencies(
                MarkToolControls: _markToolControls,
                DetectionController: _liveDetectionController,
                PlaybackControlHost: _playerPlaybackControlHost,
                RuntimeStates: _codingRuntimeStates,
                SchemaStates: _codingSchemaStates,
                SessionRuntime: codingSessionRuntime,
                ResolveVideoPath: () => _playbackContext.VideoPath,
                ResolveSettings: () => _protocolContext.Settings,
                ResolveTrainingSamples: () => _protocolContext.TrainingSamples,
                UpdateCodingOverlayViewport: UpdateCodingOverlayViewport));
        var eingabemarkerControllers =
            PlayerWindowCodingEingabemarkerControllerSetFactory.Create(
                new PlayerWindowCodingEingabemarkerControllerSetDependencies(
                    Controls: new PlayerWindowCodingEingabemarkerControls(
                        CodingOverlayPopup: CodingOverlayPopup,
                        CodingOverlayCanvas: CodingOverlayCanvas,
                        Toggle: BtnEingabemarker,
                        InputPopup: EingabemarkerPopup,
                        InputText: TxtEingabemarker,
                        QuickSelection: CmbEingabemarker),
                    PlaybackControlHost: _playerPlaybackControlHost,
                    MarkToolController: _liveDetectionMarkToolController,
                    StatusController: _liveDetectionStatusController,
                    SessionHost: _codingSessionHost,
                    SessionServiceOwner: _codingSessionRuntimeOwner,
                    OsdMeterController: _codingOsdMeterController,
                    TimelineHost: _playerTimelineHost,
                    FindingContext: _codingFindingContext,
                    TrainingPersistence: _codingTrainingPersistenceContext,
                    Dispatcher: Dispatcher,
                    Actions: new PlayerWindowCodingEingabemarkerActions(
                        UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
                        CapturePhoto: CodingCaptureSnapshot,
                        RefreshEvents: RefreshCodingEventsList,
                        UpdateToolBadge: UpdateToolBadge,
                        RunAiFallbackAsync: keyword => RunCodingAnalysisAsync(
                            $"Eingabemarker: {keyword}",
                            disableAnalyzeButton: true,
                            keywordHint: keyword,
                            codeHint: null),
                        ClearDetectionOverlays: ClearDetectionOverlays)));
        _codingEingabemarkerInteractionController = eingabemarkerControllers.Interaction;
        _codingEingabemarkerSubmissionController = eingabemarkerControllers.Submission;
        _codingEingabemarkerInputController = eingabemarkerControllers.Input;
        _liveDetectionMarkSegmentationController =
            PlayerWindowLiveDetectionMarkSegmentationControllerFactory.Create(
                new PlayerWindowLiveDetectionMarkSegmentationDependencies(
                    AiController: _codingAiRuntimeOwner.Controller,
                    OverlayToolHost: _codingOverlayToolHost,
                    OverlayCanvas: CodingOverlayCanvas,
                    ResolveContentRect: GetCodingContentRect));
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
                    runWithSuspendedOverlay: callback => _codingOverlayInputVisibilityController.Run(callback),
                    applyChanges: applyChanges)));
        _codingInlineDefectController = new CodingInlineDefectController(
            new CodingInlineDefectControllerBindings(
                HasCodingViewModel: () => _codingSessionHost.HasViewModel,
                ResolveSelectedDefect: () => _codingSessionHost.SelectedDefect,
                ResolveSelectedListEvent: () => LstCodingEvents.SelectedItem as CodingEvent,
                ExecuteAcceptDefect: () => { _codingSessionHost.ExecuteAcceptDefect(); },
                SelectDefect: _codingSessionHost.SelectDefect,
                PausePlayback: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
                TryEdit: codingEvent => CodingCodeExplorerEditWorkflow.Execute(
                    new CodingCodeExplorerEditWorkflowRequest(
                        codingEvent,
                        _codingSessionHost.VideoPath,
                        _codingSessionHost.CurrentVideoTime,
                        this),
                    CreateCodingCodeExplorerEditActions()),
                ResolveCodingSessionService: () => _codingSessionRuntimeOwner.Service,
                ExecuteEditDefect: () => { _codingSessionHost.ExecuteEditDefect(); },
                ResolveEventCollection: () => _codingSessionHost.EventCollection,
                ClearSelectedDefect: _codingSessionHost.ClearSelectedDefect,
                PersistAcceptedTrainingSample: codingEvent =>
                    _codingTrainingPersistenceContext.PersistSingleEventAsync(codingEvent)
                        .SafeFireAndForget("TrainingSaveAcceptInline"),
                PersistEditedTrainingSample: codingEvent =>
                    _codingTrainingPersistenceContext.PersistSingleEventAsync(codingEvent)
                        .SafeFireAndForget("TrainingSaveEditInline"),
                UpdateInlineDefectDetail: UpdateInlineDefectDetail,
                HideInlineDefectDetail: HideInlineDefectDetail,
                RefreshEvents: RefreshCodingEventsList,
                FadeOutAiOverlayAfterAction: FadeOutAiOverlayAfterAction));
        _codingProtocolMatchController = new CodingProtocolMatchController(
            new CodingProtocolMatchControllerBindings(
                ResolveSelectedImportEvent: () => LstImportEvents.SelectedItem,
                HasCodingSessionService: () => _codingSessionRuntimeOwner.Service is not null,
                SeekMilliseconds: _playerTimelineHost.SeekMilliseconds,
                MoveToMeter: meter => _codingSessionRuntimeOwner.Service!.MoveToMeter(meter),
                MarkNavigationPending: _codingNavigationPendingState.MarkPending,
                SyncVideoToCodingMeter: SyncVideoToCodingMeter,
                HasCodingViewModel: () => _codingSessionHost.HasViewModel,
                RunMatch: () => CodingProtocolMatchRunner.Run(
                    _codingImportReferenceEvents.Events,
                    _codingSessionHost.Events,
                    _codingProtocolMatchState.Buckets),
                StoreMatch: _codingProtocolMatchState.Store,
                ApplySummary: routing => CodingProtocolMatchSummaryControls.Apply(
                    TxtCodingProtocolMatchSummary,
                    BtnAcceptGreenCodingMatches,
                    routing),
                RefreshEvents: RefreshCodingEventsList,
                ScheduleHighlights: () => PlayerDispatcherScheduler.ScheduleLoaded(
                    Dispatcher,
                    _codingEventListVisualController.ApplyProtocolMatchHighlights)));
        _codingModeExitController = PlayerWindowCodingModeExitControllerFactory.Create(
            new PlayerWindowCodingModeExitControllerDependencies(
                RuntimeStates: _codingRuntimeStates,
                SchemaStates: _codingSchemaStates,
                OverlayStates: _codingOverlayStates,
                AiStates: _codingAiStates,
                ProtocolStates: _codingProtocolStates,
                SessionRuntime: codingSessionRuntime,
                OsdMeterController: _codingOsdMeterController,
                TimelineHost: _playerTimelineHost,
                DetectionController: _liveDetectionController,
                StreckenschadenTrackingController: _codingStreckenschadenTrackingController,
                BoundaryContext: _codingBoundaryContext,
                LiveDetectionPulseController: _liveDetectionPulseController,
                PipelineHealthController: _codingPipelineHealthController,
                ProtocolMatchController: _codingProtocolMatchController,
                OverlayInputVisibilityController: _codingOverlayInputVisibilityController,
                Controls: new PlayerWindowCodingModeExitControls(
                    ImportEventsList: LstImportEvents,
                    CodingConfirmationPanel: CodingConfirmationPanel,
                    DetectionConfirmationPanel: DetectionConfirmationPanel,
                    DetectionCanvas: DetectionCanvas,
                    DetectionOverlay: DetectionOverlayGrid,
                    CodingOverlayPopup: CodingOverlayPopup,
                    CodingOverlayCanvas: CodingOverlayCanvas,
                    CodingSidePanel: CodingSidePanel,
                    CodingSidePanelColumn: CodingSidePanelColumn,
                    CodingToolbar: CodingToolbar,
                    CodingTimelinePanel: CodingTimelinePanel,
                    CodingCalibrationHint: CodingCalibrationHint,
                    CodingMeasurementPanel: CodingMeasurementPanel,
                    OsdMeterBadge: OsdMeterBadge,
                    LiveDetectionButton: LiveDetectionButton,
                    LiveDetectionStatusText: LiveDetectionStatusText,
                    ActiveToolLabel: TxtActiveToolLabel,
                    CodingLiveAiToggle: BtnCodingLiveAi,
                    CodingAiStageText: TxtCodingAiStage),
                Actions: new PlayerWindowCodingModeExitActions(
                    CloseOpenStreckenschaeden: CloseOpenStreckenschaeden,
                    HideInlineDefectDetail: HideInlineDefectDetail,
                    ResetFrameReadiness: ResetFrameReadiness)));
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
                ResumeDetection: ResumeDetection, TeacherAnnotations: _protocolContext.TeacherAnnotations, CodeUsage: _protocolContext.CodeUsage, VsaYoloClasses: _protocolContext.VsaYoloClasses));
        _liveDetectionConfirmationTrainingController = liveDetectionTrainingControllers.Confirmation;
        _liveDetectionManualMarkTrainingController = liveDetectionTrainingControllers.ManualMark;
        _codingConfirmationController = PlayerWindowCodingConfirmationControllerFactory.Create(
            new PlayerWindowCodingConfirmationControllerDependencies(
                PendingState: _codingPendingConfirmationState,
                SessionRuntimeOwner: _codingSessionRuntimeOwner,
                SessionHost: _codingSessionHost,
                TrainingPersistence: _codingTrainingPersistenceContext,
                RefreshEvents: RefreshCodingEventsList,
                ConfirmationPanel: _codingConfirmationPanelControls,
                EventsList: _codingSidePanelControllers.EventsList,
                CurrentStatusText: TxtCodingAiStatus,
                LiveAiToggle: BtnCodingLiveAi,
                AiRuntimeOwner: _codingAiRuntimeOwner,
                PlaybackControlHost: _playerPlaybackControlHost,
                StatusController: _liveDetectionStatusController));
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














