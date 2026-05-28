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

public sealed record PlayerWindowOptions(
    bool EnableHardwareDecoding,
    bool DropLateFrames,
    bool SkipFrames,
    int FileCachingMs,
    int NetworkCachingMs,
    int CodecThreads,
    string VideoOutput)
{
    public static PlayerWindowOptions Default => new(
        EnableHardwareDecoding: true,
        DropLateFrames: true,
        SkipFrames: true,
        FileCachingMs: 3000,
        NetworkCachingMs: 3000,
        CodecThreads: 4,
        VideoOutput: "direct3d11");

    public static PlayerWindowOptions Normalize(PlayerWindowOptions? options)
    {
        var candidate = options ?? Default;
        var output = string.IsNullOrWhiteSpace(candidate.VideoOutput)
            ? "direct3d11"
            : candidate.VideoOutput.Trim().ToLowerInvariant();
        if (output is not ("direct3d11" or "direct3d9" or "any"))
            output = "direct3d11";

        return new PlayerWindowOptions(
            EnableHardwareDecoding: candidate.EnableHardwareDecoding,
            DropLateFrames: candidate.DropLateFrames,
            SkipFrames: candidate.SkipFrames,
            FileCachingMs: Math.Clamp(candidate.FileCachingMs, 100, 10000),
            NetworkCachingMs: Math.Clamp(candidate.NetworkCachingMs, 100, 10000),
            CodecThreads: Math.Clamp(candidate.CodecThreads, 1, 16),
            VideoOutput: output);
    }
}

public sealed record DamageMarkerInfo(
    string Code,
    string? Description,
    double MeterStart,
    double? MeterEnd,
    bool IsStreckenschaden);

public sealed record PlayerDamageOverlayData(
    double PipeLengthMeters,
    IReadOnlyList<DamageMarkerInfo> Markers);

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
    private readonly List<(DamageMarkerInfo Info, FrameworkElement Container, FrameworkElement TickOrRange, TextBlock Label)> _damageMarkers = new();

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Quick-Scan state ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬
    private CancellationTokenSource? _quickScanCts;
    private bool _isQuickScanning;
    private readonly List<(QuickScanSegment Seg, Rectangle Rect)> _heatmapRects = new();

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
        InitializeComponent();
        WindowStateManager.Track(this);

        _videoPath = videoPath;
        _damageOverlay = damageOverlay;
        _options = PlayerWindowOptions.Normalize(options);
        _serviceProvider = serviceProvider;
        _haltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        _haltungRecord = haltungRecord;
        _initialOverlayText = initialOverlayText;
        _lastOpened = this;
        Loaded += (_, _) => EnsureVisibleOnScreen();

        // Overlay suspendieren wenn ein FREMDES Fenster den Fokus bekommt (z.B. Snipping Tool).
        // Nicht bei eigenen Child-Dialogen (MessageBox, VsaCodeExplorer) â€” die verwenden
        // SuspendCodingOverlayInput/ResumeCodingOverlayInput direkt.
        Deactivated += (_, _) =>
        {
            // Nur suspendieren wenn kein eigener Dialog den Fokus hat
            if (_codingOverlaySuspendDepth > 0) return;
            _deactivatedByExternalWindow = true;
            SuspendCodingOverlayInput();
        };
        Activated += (_, _) =>
        {
            if (!_deactivatedByExternalWindow) return;
            _deactivatedByExternalWindow = false;
            ResumeCodingOverlayInput();
        };

        var fileName = Path.GetFileName(videoPath);
        var displayName = string.IsNullOrWhiteSpace(fileName) ? "Video" : fileName;
        Title = $"Video - {displayName}";
        VideoNameText.Text = displayName;
        VideoPathText.Text = videoPath;

        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            throw new FileNotFoundException("Video nicht gefunden", videoPath);

        Core.Initialize();

        _libVlc = CreateLibVlc(_options);
        _player = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = _options.EnableHardwareDecoding
        };
        VideoView.MediaPlayer = _player;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, __) => UpdateUi();

        // Scrub timer: fires pending seek when dragging (throttled)
        _scrubTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _scrubTimer.Tick += (_, __) =>
        {
            _scrubTimer.Stop();
            if (_isDragging)
                ScrubSeekToSlider();
        };

        PositionSlider.AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, __) =>
            {
                _wasPlayingBeforeDrag = _player.IsPlaying;
                _isDragging = true;
                // Pause during drag so frames render cleanly
                if (_wasPlayingBeforeDrag)
                    _player.SetPause(true);
                ScrubSeekToSlider();
            }),
            true);
        PositionSlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, __) =>
            {
                _scrubTimer.Stop();
                SeekToSlider();
                _isDragging = false;
                // Resume playback if it was playing before drag
                if (_wasPlayingBeforeDrag)
                    _player.SetPause(false);
            }),
            true);

        PositionSlider.PreviewMouseLeftButtonUp += (_, __) =>
        {
            if (!_isDragging)
                SeekToSlider();
        };

        PositionSlider.LostMouseCapture += (_, __) =>
        {
            if (_isDragging)
            {
                _scrubTimer.Stop();
                SeekToSlider();
                _isDragging = false;
                if (_wasPlayingBeforeDrag)
                    _player.SetPause(false);
            }
        };

        Closing += OnClosing;
        Loaded += (_, __) =>
        {
            Play(_videoPath);
            UpdateCodingOverlayViewport();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
            if (!string.IsNullOrWhiteSpace(_initialOverlayText))
                ShowOverlay(_initialOverlayText!, TimeSpan.FromSeconds(6));

            BuildDamageMarkers();

            Focusable = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }));
        };

        DamageMarkerCanvas.SizeChanged += (_, __) => RepositionDamageMarkers();
        HeatmapCanvas.SizeChanged += (_, __) => RepositionHeatmap();
        DetectionCanvas.MouseLeftButtonDown += DetectionCanvas_MouseLeftButtonDown;
        VideoView.SizeChanged += (_, __) => UpdateCodingOverlayViewport();
        SizeChanged += (_, __) => UpdateCodingOverlayViewport();
        LocationChanged += (_, __) => UpdateCodingOverlayViewport();

        Closed += (_, __) =>
        {
            if (ReferenceEquals(_lastOpened, this))
                _lastOpened = null;

            // Codier-Modus sauber beenden: Timer + Hintergrund-Tasks stoppen
            // MUSS vor Cleanup() passieren, da sonst Timer auf disposed VLC zugreifen
            _isCodingMode = false;
            StopCodingOsdTimer();
            _codingAnalysisCts?.Cancel();
            _codingAnalysisCts?.Dispose();
            _codingAnalysisCts = null;
            _codingLiveDetection = null;
            StopCodingAiPulse();

            _quickScanCts?.Cancel();
            StopLiveDetection();
            Cleanup();

            // Hauptfenster sichtbar machen und aktivieren
            var main = System.Windows.Application.Current?.MainWindow;
            if (main != null && !ReferenceEquals(main, this))
            {
                if (main.WindowState == WindowState.Minimized)
                    main.WindowState = WindowState.Normal;
                main.Activate();
            }
        };

        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(PlayerWindow_PreviewKeyDown), true);
    }


}














