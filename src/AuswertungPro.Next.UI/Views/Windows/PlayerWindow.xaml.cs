using System;
using System.IO;
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
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            throw new FileNotFoundException("Video nicht gefunden", videoPath);

        InitializeComponent();
        WireCodingSidePanelEvents();
        WindowStateManager.Track(this);

        _videoPath = videoPath;
        _damageOverlay = damageOverlay;
        _options = PlayerWindowOptions.Normalize(options);
        _serviceProvider = serviceProvider;
        _haltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        _haltungRecord = haltungRecord;
        _initialOverlayText = initialOverlayText;

        var fileName = Path.GetFileName(videoPath);
        var displayName = string.IsNullOrWhiteSpace(fileName) ? "Video" : fileName;
        Title = $"Video - {displayName}";
        VideoNameText.Text = displayName;
        VideoPathText.Text = videoPath;

        Core.Initialize();

        _libVlc = PlayerLibVlcFactory.Create(_options);
        _player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = _options.EnableHardwareDecoding
        };
        VideoView.MediaPlayer = _player;

        _damageMarkerController = new DamageMarkerController(
            DamageMarkerCanvas,
            PositionSlider,
            _damageOverlay,
            _player,
            EnsurePlaying,
            UpdateUi,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas));

        _quickScanController = new QuickScanController(
            HeatmapCanvas,
            QuickScanButton,
            QuickScanStatusText,
            _player,
            _videoPath,
            EnsurePlaying,
            UpdateUi,
            () => PlayerSliderTrackBounds.Resolve(PositionSlider, DamageMarkerCanvas));

        _speedControls = new PlayerSpeedControls(
            RateText,
            Speed05Button,
            Speed1Button,
            Speed15Button,
            Speed2Button,
            Speed4Button,
            Speed8Button);

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














