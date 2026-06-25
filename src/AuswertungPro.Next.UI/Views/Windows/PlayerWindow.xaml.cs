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

        _damageMarkerController = new DamageMarkerController(
            DamageMarkerCanvas,
            PositionSlider,
            _damageOverlay,
            _playerPlaybackControlHost,
            _playerTimelineHost,
            EnsurePlaying,
            UpdateUi,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas));

        _quickScanController = new QuickScanController(
            HeatmapCanvas,
            QuickScanButton,
            QuickScanStatusText,
            _playerPlaybackControlHost,
            _playerTimelineHost,
            _videoPath,
            EnsurePlaying,
            UpdateUi,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas));

        _positionControls = new PlayerPositionControls(
            PositionSlider,
            CurrentTimeText,
            DurationText);

        _speedControls = new PlayerSpeedControls(
            RateText,
            Speed05Button,
            Speed1Button,
            Speed15Button,
            Speed2Button,
            Speed4Button,
            Speed8Button);

        _markToolControls = new PlayerMarkToolControls(
            MarkToolPopup,
            CodingMarkToolPopup,
            ToolsDropdownPopup,
            TxtMarkToolName,
            TxtActiveToolLabel,
            DetectionOverlayGrid,
            DetectionCanvas,
            CodingOverlayPopup,
            CodingOverlayCanvas);

        _codingOverlayRenderController = new CodingOverlayRenderController(
            new CanvasOverlaySurface(CodingOverlayCanvas),
            new DelegateOverlayCoordinateMapper(CodingNormToPixel));
        _codingSessionViewModelOwner = new CodingSessionViewModelOwner(CodingVm_PropertyChanged);
        _codingSessionHost = new CodingSessionHost(() => _codingSessionViewModelOwner.ViewModel);
        _codingOverlayToolHost = new CodingOverlayToolHost(() => _codingOverlayRuntimeOwner.Service);

        _timer = CreateUpdateTimer();
        _scrubTimer = CreateScrubTimer();
        WirePositionSliderEvents();
        WireWindowLifecycleEvents();
        WireWindowSurfaceEvents();
        WireKeyboardEvents();

        // Erst ganz am Ende setzen: TryShowOverlayOnLast darf nie ein Fenster sehen,
        // dessen Konstruktor fehlgeschlagen ist (Media-Runtime waere dann nicht bereit).
        _lastOpened = this;
    }


}














