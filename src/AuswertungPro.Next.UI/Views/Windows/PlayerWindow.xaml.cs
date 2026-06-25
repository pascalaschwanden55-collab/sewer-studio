using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

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

        Core.Initialize();

        _libVlc = PlayerLibVlcFactory.Create(_options);
        _player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = _options.EnableHardwareDecoding
        };
        VideoView.MediaPlayer = _player;

        _playerTimelineHost = new PlayerTimelineHost(
            readTimeMilliseconds: () => _player.Time,
            readLengthMilliseconds: () => _player.Length,
            seekMilliseconds: milliseconds => _player.Time = milliseconds,
            setPositionRatio: position => _player.Position = position);

        _playerPlaybackControlHost = new PlayerPlaybackControlHost(
            readIsPlaying: () => _player.IsPlaying,
            setPause: pause => _player.SetPause(pause),
            play: () => _player.Play(),
            stop: () => _player.Stop(),
            readRate: () => _player.Rate,
            setRate: _player.SetRate,
            shouldStartPlayback: () =>
            {
                var state = _player.State;
                return state == VLCState.Stopped || state == VLCState.Ended;
            },
            playPath: path =>
            {
                using var media = new Media(_libVlc, path, FromType.FromPath);
                _player.Play(media);
            });

        _playerMarqueeOverlayHost = new PlayerMarqueeOverlayHost(
            setMarqueeInt: (option, value) => _player.SetMarqueeInt(option, value),
            setMarqueeString: (option, value) => _player.SetMarqueeString(option, value));

        _playerSnapshotCaptureHost = new PlayerSnapshotCaptureHost(
            takeSnapshot: (path, width, height) => _player.TakeSnapshot(0, path, width, height));

        _damageMarkerController = new DamageMarkerController(
            DamageMarkerCanvas,
            PositionSlider,
            _damageOverlay,
            _player,
            _playerTimelineHost,
            EnsurePlaying,
            UpdateUi,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas));

        _quickScanController = new QuickScanController(
            HeatmapCanvas,
            QuickScanButton,
            QuickScanStatusText,
            _player,
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
        // dessen Konstruktor fehlgeschlagen ist (_player/_libVlc waeren dann null).
        _lastOpened = this;
    }


}














