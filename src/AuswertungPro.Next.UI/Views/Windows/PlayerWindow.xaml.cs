using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

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
        // Frueh pruefen, bevor irgendein Zustand (insb. _lastOpened) gesetzt wird:
        // wirft der Konstruktor spaeter, bliebe sonst ein halb-konstruiertes Fenster zurueck.
        var videoInfo = PlayerVideoPathGuard.Validate(videoPath);

        var codingSessionRuntime = CodingSessionRuntimeFactory.Create(
            CodingVm_PropertyChanged,
            () => _codingOverlayRuntimeOwner.Service);
        _codingSessionViewModelOwner = codingSessionRuntime.ViewModelOwner;
        _codingSessionHost = codingSessionRuntime.SessionHost;
        _codingOverlayToolHost = codingSessionRuntime.OverlayToolHost;

        InitializeComponent();
        WireCodingSidePanelEvents();
        InitializeCodingSidePanelControllers();
        InitializeCodingConfirmationPanelControls();
        WindowStateManager.Track(this);

        _videoPath = videoInfo.VideoPath;
        _damageOverlay = damageOverlay;
        _options = PlayerWindowOptions.Normalize(options);
        _serviceProvider = serviceProvider;
        _dependencies = PlayerWindowDependencies.From(serviceProvider);
        _haltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        _haltungRecord = haltungRecord;
        _initialOverlayText = initialOverlayText;

        PlayerWindowHeaderControls.ApplyVideoInfo(this, VideoNameText, VideoPathText, videoInfo);

        _playerMediaRuntime = PlayerMediaRuntimeFactory.Create(_options);
        _playerMediaRuntime.AttachVideoView(VideoView);

        var playerMediaHosts = _playerMediaRuntime.Hosts;
        _playerTimelineHost = playerMediaHosts.TimelineHost;
        _playerPlaybackControlHost = playerMediaHosts.PlaybackControlHost;
        _playerMarqueeOverlayHost = playerMediaHosts.MarqueeOverlayHost;
        _playerSnapshotCaptureHost = playerMediaHosts.SnapshotCaptureHost;

        var controllerSet = PlayerWindowControllerSetFactory.Create(
            new PlayerWindowControllerSetControls(
                DamageMarkerCanvas: DamageMarkerCanvas,
                PositionSlider: PositionSlider,
                HeatmapCanvas: HeatmapCanvas,
                QuickScanButton: QuickScanButton,
                QuickScanStatusText: QuickScanStatusText,
                CurrentTimeText: CurrentTimeText,
                DurationText: DurationText,
                RateText: RateText,
                Speed05Button: Speed05Button,
                Speed1Button: Speed1Button,
                Speed15Button: Speed15Button,
                Speed2Button: Speed2Button,
                Speed4Button: Speed4Button,
                Speed8Button: Speed8Button,
                MarkToolPopup: MarkToolPopup,
                CodingMarkToolPopup: CodingMarkToolPopup,
                ToolsDropdownPopup: ToolsDropdownPopup,
                MarkToolName: TxtMarkToolName,
                ActiveToolLabel: TxtActiveToolLabel,
                DetectionOverlayGrid: DetectionOverlayGrid,
                DetectionCanvas: DetectionCanvas,
                CodingOverlayPopup: CodingOverlayPopup,
                CodingOverlayCanvas: CodingOverlayCanvas),
            new PlayerWindowControllerSetDependencies(
                DamageOverlay: _damageOverlay,
                PlaybackControlHost: _playerPlaybackControlHost,
                TimelineHost: _playerTimelineHost,
                VideoPath: _videoPath,
                EnsurePlaying: EnsurePlaying,
                UpdateUi: UpdateUi,
                ResolveSliderTrackBounds: () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas),
                MapCodingOverlayPoint: CodingNormToPixel));

        _damageMarkerController = controllerSet.DamageMarkerController;
        _quickScanController = controllerSet.QuickScanController;
        _positionControls = controllerSet.PositionControls;
        _speedControls = controllerSet.SpeedControls;
        _markToolControls = controllerSet.MarkToolControls;
        _codingOverlayRenderController = controllerSet.CodingOverlayRenderController;

        var playerTimers = CreatePlayerTimers();
        _timer = playerTimers.UpdateTimer;
        _scrubTimer = playerTimers.ScrubTimer;
        WirePositionSliderEvents();
        WireWindowLifecycleEvents();
        WireWindowSurfaceEvents();
        WireKeyboardEvents();

        // Erst ganz am Ende setzen: TryShowOverlayOnLast darf nie ein Fenster sehen,
        // dessen Konstruktor fehlgeschlagen ist (Media-Runtime waere dann nicht bereit).
        _lastOpened = this;
    }


}














