using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.UI.Helpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Rectangle = System.Windows.Shapes.Rectangle;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using LibVLCSharp.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow : Window
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly DispatcherTimer _timer;
    private bool _isDragging;
    private bool _wasPlayingBeforeDrag;
    private DateTime _lastScrubSeek = DateTime.MinValue;
    private readonly DispatcherTimer _scrubTimer;
    private readonly string _videoPath;
    private readonly PlayerWindowOptions _options;
    private readonly string? _initialOverlayText;
    private readonly PlayerDamageOverlayData? _damageOverlay;
    private readonly DamageMarkerController _damageMarkerController;

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Quick-Scan state ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬
    private readonly QuickScanController _quickScanController;

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Live Detection state ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬
    private OllamaClient? _liveDetectionClient;
    private LiveDetectionService? _liveDetectionService;
    private DispatcherTimer? _detectionTimer;
    private CancellationTokenSource? _detectionCts;
    private bool _isDetecting;
    private bool _isDetectionInFlight;
    private bool _isManualMarkMode;
    private double _lastDetectionTimestamp;
    private readonly List<LiveFrameFinding> _currentFindings = new();
    private List<LiveFrameFinding>? _detectionPendingFindings; // Befunde warten auf User-Bestaetigung
    private byte[]? _detectionPendingFrameBytes;
    private double? _detectionPendingTimestampSec;
    private string _liveDetectionModelName = string.Empty;

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Protocol integration (optional, passed by caller) ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬
    private readonly ServiceProvider? _serviceProvider;
    private readonly string? _haltungId;
    private readonly Action<ProtocolEntry>? _onEntryCreated;
    private readonly HaltungRecord? _haltungRecord;

    // Guard-Flag: wird am Anfang von OnClosing gesetzt, bevor MediaPlayer freigegeben wird.
    // Alle DispatcherTimer-Tick-Handler pruefen dieses Flag als erste Aktion (early-out).
    private volatile bool _closing;
    private bool _playbackDisposed;

    private static PlayerWindow? _lastOpened;

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














