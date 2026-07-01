using System;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
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
                VideoPath: _playbackContext.VideoPath,
                EnsurePlaying: EnsurePlaying,
                UpdateUi: UpdateUi,
                ScrubSeekToSlider: ScrubSeekToSlider,
                ResolveSliderTrackBounds: () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas),
                MapCodingOverlayPoint: CodingNormToPixel));
        WirePositionSliderEvents();
        WireWindowLifecycleEvents();
        WireWindowSurfaceEvents();
        WireKeyboardEvents();

        // Erst ganz am Ende setzen: TryShowOverlayOnLast darf nie ein Fenster sehen,
        // dessen Konstruktor fehlgeschlagen ist (Media-Runtime waere dann nicht bereit).
        LastOpenedWindow.Set(this);
    }


}














