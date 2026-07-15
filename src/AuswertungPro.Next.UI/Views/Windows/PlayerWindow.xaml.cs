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
        InitializeCodingConfirmationPanelControls();
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
                SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail),
                SetAnalyzeButtonEnabled: enabled => CodingAnalyzeButtonControls.SetEnabled(BtnCodingAnalyze, enabled),
                SetYoloStatus: (status, color, model) => SetYoloStatus(status, color, model),
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
        _codingConfirmationDecisionController = new CodingConfirmationDecisionController(
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
                ApplyResumeStatus: status => SetCodingAiState(
                    status.StatusText,
                    PlayerStatusColors.Success,
                    status.DetailText)));
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














