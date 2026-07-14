using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

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
        _codingCalibrationPointerController = new CodingCalibrationPointerController(
            _codingCalibrationState,
            new CodingCalibrationPointerControllerActions(
                CaptureMouse: () => CodingOverlayInputControls.CaptureCanvasMouse(CodingOverlayCanvas),
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(CodingOverlayCanvas),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                RenderPreview: _codingOverlayRenderController.RenderCalibrationPreview,
                ApplyPreview: preview => CodingCalibrationControls.ApplyPreview(TxtCodingCalibHint, preview),
                ApplyCalibration: ApplyCodingCalibration));
        WireCodingSidePanelEvents();
        InitializeCodingSidePanelControllers();
        InitializeCodingConfirmationPanelControls();
        PlayerWindowStateControls.Track(this);

        _playbackContext = PlayerWindowPlaybackContext.From(videoInfo, initialOverlayText, damageOverlay);
        var normalizedOptions = PlayerWindowOptions.Normalize(options);
        _protocolContext = PlayerWindowProtocolContext.From(
            serviceProvider,
            haltungId,
            onEntryCreated,
            haltungRecord);
        var playerSettings = _protocolContext.Settings ?? AppSettings.Load();
        WireCodingPhotoHoverPreview();
        _codingTrainingSamplesOwner = CodingTrainingSamplesOwner.CreateDefault(
            () => _codingSessionRuntimeOwner.Service,
            _protocolContext.Settings);

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
                    PersistSingleEventAsTrainingSample(codingEvent).SafeFireAndForget(operation),
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














