using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
using AuswertungPro.Next.Application.Player;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views.Windows;

public sealed record PlayerWindowOptions(
    bool EnableHardwareDecoding,
    bool DropLateFrames,
    bool SkipFrames,
    int FileCachingMs,
    int NetworkCachingMs,
    int CodecThreads,
    string VideoOutput) : IVideoPlaybackOptions
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

public partial class PlayerWindow : Window, IVlcSurface
{
    private const float MinRate = 0.25f;
    private const float MaxRate = 8.0f;

    private readonly IVideoPlaybackController _videoPlayback;
    private readonly MediaPlayer _player;
    private readonly string _videoPath;
    private readonly PlayerWindowOptions _options;
    private readonly string? _initialOverlayText;
    private readonly PlayerDamageOverlayData? _damageOverlay;
    private readonly List<(DamageMarkerInfo Info, FrameworkElement Container, FrameworkElement TickOrRange, TextBlock Label)> _damageMarkers = new();

    // Ã¢"â‚¬Ã¢"â‚¬ Quick-Scan state Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬
    private CancellationTokenSource? _quickScanCts;
    private bool _isQuickScanning;
    private readonly List<(QuickScanSegment Seg, Rectangle Rect)> _heatmapRects = new();

    // Ã¢"â‚¬Ã¢"â‚¬ Live Detection state Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬
    private OllamaClient? _liveDetectionClient;
    private LiveDetectionService? _liveDetectionService;
    private DispatcherTimer? _detectionTimer;
    private CancellationTokenSource? _detectionCts;
    private bool _isDetecting;

    // 2026-04-26: Window-Lifecycle-Flag. Wird im Closed-Handler auf true
    // gesetzt. Alle Timer-Ticks und fire-and-forget-Tasks pruefen das,
    // damit sie nicht auf disposed Felder zugreifen und die App killen.
    private volatile bool _isWindowClosed;
    private bool _isDetectionInFlight;
    private bool _isManualMarkMode;
    private double _lastDetectionTimestamp;
    private readonly List<LiveFrameFinding> _currentFindings = new();
    private List<LiveFrameFinding>? _detectionPendingFindings; // Befunde warten auf User-Bestaetigung
    private byte[]? _detectionPendingFrameBytes;
    private double? _detectionPendingTimestampSec;
    private string _liveDetectionModelName = string.Empty;

    // Ã¢"â‚¬Ã¢"â‚¬ Protocol integration (optional, passed by caller) Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬
    // Phase 5.1.B Etappe 4 Sub-D: ServiceProvider-Pass-through entfernt — ProtocolObservationsWindow
    // zieht ihre Services nun selbst aus dem DI-Container.
    private readonly string? _haltungId;
    private readonly Action<ProtocolEntry>? _onEntryCreated;
    private readonly HaltungRecord? _haltungRecord;

    private static PlayerWindow? _lastOpened;

    public PlayerWindow(
        string videoPath,
        PlayerWindowOptions? options = null,
        string? initialOverlayText = null,
        PlayerDamageOverlayData? damageOverlay = null,
        string? haltungId = null,
        Action<ProtocolEntry>? onEntryCreated = null,
        HaltungRecord? haltungRecord = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _videoPath = videoPath;
        _damageOverlay = damageOverlay;
        _options = PlayerWindowOptions.Normalize(options);
        _haltungId = haltungId;
        _onEntryCreated = onEntryCreated;
        _haltungRecord = haltungRecord;
        _initialOverlayText = initialOverlayText;
        _lastOpened = this;
        Loaded += (_, _) => EnsureVisibleOnScreen();

        // Overlay suspendieren wenn ein FREMDES Fenster den Fokus bekommt (z.B. Snipping Tool).
        // Nicht bei eigenen Child-Dialogen (MessageBox, VsaCodeExplorer) — die verwenden
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

        _videoPlayback = CreatePlaybackController(_videoPath, _options);
        _player = (MediaPlayer)_videoPlayback.NativePlayer;
        _videoPlayback.PositionUpdateRequested += (_, __) => UpdateUi();
        _videoPlayback.ScrubRequested += (_, __) => ScrubSeekToSlider();

        PositionSlider.AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, __) =>
            {
                _videoPlayback.BeginDrag(PositionSlider.Value, PositionSlider.Maximum);
                ApplySeekSnapshot(_videoPlayback.PreviewSeek(PositionSlider.Value, PositionSlider.Maximum));
            }),
            true);
        PositionSlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, __) =>
            {
                _videoPlayback.CompleteDrag(PositionSlider.Value, PositionSlider.Maximum);
                UpdateUi();
            }),
            true);

        PositionSlider.PreviewMouseLeftButtonUp += (_, __) =>
        {
            if (!_videoPlayback.IsDragging)
                SeekToSlider();
        };

        PositionSlider.LostMouseCapture += (_, __) =>
        {
            if (_videoPlayback.IsDragging)
            {
                _videoPlayback.CancelDrag(PositionSlider.Value, PositionSlider.Maximum);
                UpdateUi();
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
        // Training-Overlay folgt ebenfalls Fenster-/Video-Resize
        SizeChanged += (_, __) => { if (_isTrainingMode) UpdateTrainingOverlayViewport(); };
        LocationChanged += (_, __) => { if (_isTrainingMode) UpdateTrainingOverlayViewport(); };

        Closed += (_, __) =>
        {
            // SOFORT als erstes setzen: alle Timer-Ticks und fire-and-forget-
            // Tasks die jetzt noch in der Pipeline sind sehen das Flag und
            // brechen ab, bevor sie auf disposed Felder zugreifen.
            _isWindowClosed = true;

            // KRITISCH: Closed-Handler darf NIE eine Exception nach aussen
            // propagieren. Sonst kommt sie als DispatcherUnhandledException hoch
            // und kann (je nach Race mit MainWindow-Restore) die ganze App killen.
            // User-Klage 2026-04-25: "Codierfenster geschlossen -> ganze App zu".
            //
            // Strategie: jeder einzelne Cleanup-Schritt in einen lokalen try/catch.
            // Failures werden geloggt, niemals geworfen.
            void Safe(string step, Action a)
            {
                try { a(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayerWindow.Closed] {step}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // 1. MainWindow-Reparatur (falls WPF auf this umgebogen)
            Safe("MainWindow-Restore", () =>
            {
                var app = System.Windows.Application.Current;
                if (app != null && ReferenceEquals(app.MainWindow, this))
                {
                    foreach (Window w in app.Windows)
                    {
                        if (!ReferenceEquals(w, this))
                        {
                            app.MainWindow = w;
                            break;
                        }
                    }
                }
            });

            Safe("LastOpened-Reset", () =>
            {
                if (ReferenceEquals(_lastOpened, this))
                    _lastOpened = null;
            });

            // 2. Codier-Modus sauber beenden (Timer + CTS stoppen vor VLC-Dispose)
            Safe("Coding-Mode-Cleanup", () =>
            {
                _isCodingMode = false;
                StopCodingOsdTimer();
                _codingAnalysisCts?.Cancel();
                _codingAnalysisCts?.Dispose();
                _codingAnalysisCts = null;
                _codingLiveDetection = null;
                StopCodingAiPulse();
            });

            Safe("QuickScan-Cancel", () => _quickScanCts?.Cancel());
            Safe("LiveDetection-Stop", () => StopLiveDetection());

            // 3. Trainings-Modus + zugehoerige Ressourcen
            Safe("Training-Mode-Cleanup", () =>
            {
                _isTrainingMode = false;
                _trainingSamCts?.Cancel();
                _trainingSamCts?.Dispose();
                _trainingKbCtx?.Dispose();
                _trainingHttp?.Dispose();
                _trainingKbCtx = null;
                _trainingKbManager = null;
                _trainingEmbedder = null;
                _trainingHttp = null;
                _trainingSamCts = null;
                _trainingSidecar = null;
                _trainingLastSamResult = null;
            });

            // 4. VLC + Player Cleanup
            Safe("VLC-Cleanup", () => Cleanup());

            // 5. Hauptfenster wieder aktivieren
            Safe("MainWindow-Activate", () =>
            {
                var main = System.Windows.Application.Current?.MainWindow;
                if (main != null && !ReferenceEquals(main, this))
                {
                    if (main.WindowState == WindowState.Minimized)
                        main.WindowState = WindowState.Normal;
                    main.Activate();
                }
            });
        };

        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(PlayerWindow_PreviewKeyDown), true);
    }

    public static bool TryShowOverlayOnLast(string text, TimeSpan duration)
    {
        if (_lastOpened is null)
            return false;
        _lastOpened.ShowOverlay(text, duration);
        return true;
    }

    public static bool TryGetCurrentTime(out TimeSpan time)
    {
        time = default;
        if (_lastOpened is null)
            return false;

        return _lastOpened.TryGetCurrentTimeInternal(out time);
    }

    public static bool TrySeekTo(TimeSpan time)
    {
        if (_lastOpened is null)
            return false;

        return _lastOpened.TrySeekToInternal(time);
    }

    // Phase 6.1.A: TryTakeSnapshot + TakeSnapshotSafe nach PlayerWindow.Snapshot.cs migriert.

    private void ShowOverlay(string text, TimeSpan duration)
    {
        if (_player is null)
            return;

        try
        {
            _player.SetMarqueeInt(VideoMarqueeOption.Enable, 1);
            _player.SetMarqueeInt(VideoMarqueeOption.X, 16);
            _player.SetMarqueeInt(VideoMarqueeOption.Y, 16);
            _player.SetMarqueeInt(VideoMarqueeOption.Size, 24);
            _player.SetMarqueeInt(VideoMarqueeOption.Color, 0xFFFFFF);
            _player.SetMarqueeInt(VideoMarqueeOption.Opacity, 200);
            _player.SetMarqueeString(VideoMarqueeOption.Text, text);

            var t = new DispatcherTimer { Interval = duration };
            t.Tick += (_, __) =>
            {
                t.Stop();
                try { _player.SetMarqueeInt(VideoMarqueeOption.Enable, 0); } catch { }
            };
            t.Start();
        }
        catch
        {
            // ignore overlay errors
        }
    }

    private bool TryGetCurrentTimeInternal(out TimeSpan time)
    {
        return _videoPlayback.TryGetCurrentTime(out time);
    }

    private bool TrySeekToInternal(TimeSpan time)
    {
        var success = _videoPlayback.TrySeekTo(time);
        if (success)
            UpdateUi();
        return success;
    }

    // Audit R-C3 2026-04-25: Doppel-ESC innerhalb 1.5s als Notbremse fuer
    // den Codier-Modus, falls Overlay-Canvas in unerwarteten Zustand klemmt.
    // Phase 6.1.G: Hotkeys (PlayerWindow_PreviewKeyDown + TogglePlayPause +
    // _lastEscapePress) nach PlayerWindow.Hotkeys.cs migriert.

    // Phase 6.1.A: FormatMs nach PlayerWindow.Helpers.cs migriert.
    // Phase 6.1.D Sub-B/C/D: EnsurePlaying, ChangeSpeed, JumpSeconds, Play(string),
    //   OnClosing, Cleanup, UpdateUi nach PlayerWindow.VideoPlayback.cs migriert.

    // Phase 6.1.D Sub-A: Play/Pause/Stop/Speed-Click + Slider-Seek + Rate-Label
    // (12 Methoden) nach PlayerWindow.VideoPlayback.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Damage marker overlay Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    // Phase 6.1.G: BuildDamageMarkers, CreatePointMarker, CreateRangeMarker
    // nach PlayerWindow.DamageMarkers.cs migriert.

    private (double offsetX, double trackWidth) GetSliderTrackBounds()
    {
        if (PositionSlider.Template?.FindName("PART_Track", PositionSlider) is Track track
            && track.IsVisible
            && track.ActualWidth > 0)
        {
            var thumbHalf = (track.Thumb?.ActualWidth ?? 18) / 2.0;
            var ptStart = track.TranslatePoint(new Point(thumbHalf, 0), DamageMarkerCanvas);
            var ptEnd = track.TranslatePoint(new Point(track.ActualWidth - thumbHalf, 0), DamageMarkerCanvas);
            return (ptStart.X, ptEnd.X - ptStart.X);
        }

        // Fallback: assume 9px thumb offset on each side
        return (9, Math.Max(DamageMarkerCanvas.ActualWidth - 18, 1));
    }

    // Phase 6.1.G: RepositionDamageMarkers + SeekToMeter nach
    // PlayerWindow.DamageMarkers.cs migriert.

    // Phase 6.1.D Sub-D: EnsureVisibleOnScreen nach PlayerWindow.VideoPlayback.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Quick-Scan Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        if (_isQuickScanning)
        {
            _quickScanCts?.Cancel();
            QuickScanButton.IsChecked = false;
            return;
        }

        AiRuntimeConfig cfg;
        try
        {
            cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        }
        catch
        {
            MessageBox.Show("KI-Konfiguration konnte nicht geladen werden.", "Schnell-Scan",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            QuickScanButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Schnell-Scan",
                MessageBoxButton.OK, MessageBoxImage.Information);
            QuickScanButton.IsChecked = false;
            return;
        }

        var ffmpegPath = cfg.FfmpegPath ?? FfmpegLocator.ResolveFfmpeg();
        using var client = new OllamaClient(cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
            keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);
        var service = new QuickScanService(client, cfg.VisionModel, ffmpegPath);

        _quickScanCts = new CancellationTokenSource();
        _isQuickScanning = true;

        HeatmapCanvas.Children.Clear();
        _heatmapRects.Clear();

        QuickScanStatusText.Visibility = Visibility.Visible;
        QuickScanStatusText.Text = "Starte...";

        var progress = new Progress<QuickScanProgress>(p =>
        {
            QuickScanStatusText.Text = p.Status;
            if (p.LatestSegment is { } seg)
                AddHeatmapSegment(seg, p.FramesTotal * 5.0); // estimate duration
        });

        try
        {
            var result = await service.ScanAsync(_videoPath, progress, _quickScanCts.Token);

            // Rebuild heatmap with exact duration
            HeatmapCanvas.Children.Clear();
            _heatmapRects.Clear();
            foreach (var seg in result.Segments)
                AddHeatmapSegment(seg, result.VideoDurationSeconds);

            QuickScanStatusText.Text = result.Error ?? $"Fertig: {result.FramesAnalyzed} Frames analysiert";
        }
        catch (OperationCanceledException)
        {
            QuickScanStatusText.Text = "Abgebrochen";
        }
        catch (Exception ex)
        {
            QuickScanStatusText.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _isQuickScanning = false;
            QuickScanButton.IsChecked = false;
            _quickScanCts?.Dispose();
            _quickScanCts = null;

            // Hide status after 5 seconds
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            hideTimer.Tick += (_, _) =>
            {
                hideTimer.Stop();
                if (!_isQuickScanning)
                    QuickScanStatusText.Visibility = Visibility.Collapsed;
            };
            hideTimer.Start();
        }
    }

    // Phase 6.1.G: AddHeatmapSegment + RepositionHeatmap nach
    // PlayerWindow.Heatmap.cs migriert.

    // Phase 6.1.A: SeverityToColor nach PlayerWindow.Helpers.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Live Detection Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    // Phase 6.1.A: CompactModelName nach PlayerWindow.Helpers.cs migriert.

    // Phase 6.1.E: SetLiveDetectionBadge + SetYoloStatus nach
    // PlayerWindow.LiveDetection.cs migriert.

    // Phase 6.1.F Sub-A: SetCodingAiState + StartCodingAiPulse + StopCodingAiPulse
    // nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.E: Live-Detection-Lifecycle (LiveDetection_Click,
    // StartLiveDetectionAsync, StopLiveDetection, DetectionTimer_Tick,
    // RunDetectionAsync, CaptureCurrentFrameAsync, UpdateDetectionStatus)
    // nach PlayerWindow.LiveDetection.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Detection Overlay Rendering (ring-sector pattern from LiveFrameWindow) Ã¢"â‚¬Ã¢"â‚¬

    // Phase 6.1.E Sub-B: RenderDetectionOverlay + RenderRingSectorOverlay +
    // RenderRingSectorFinding nach PlayerWindow.LiveDetection.cs migriert.

    // Phase 6.1.A: MapDetectionSeverityColor + ParseClockHour + BuildRingSectorGeometry +
    // DegToRad nach PlayerWindow.Helpers.cs migriert.
    // Phase 6.1.C: BuildDetectionLabel nach PlayerWindow.Helpers.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Manual Marking Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    // Phase 6.1.G: Manual-Mark-Tool (ManualMark_Click, ToolsDropdown_Click,
    // MarkTool_*_Click, ActivateMarkTool, EnsureMarkOverlayReady,
    // DeactivateMarkTool, HandleMarkDrawingComplete, ShowSamPreviewAtMarkAsync,
    // GetCodingOverlayRenderSize, RenderSamPromptBox, SaveMarkAsTrainingAsync,
    // DetectionCanvas_MouseLeftButtonDown, ClickToClockPosition)
    // nach PlayerWindow.MarkTool.cs migriert.




    // Phase 6.1.F Sub-B: CodingModeExit_Click + _codingNavPending +
    // CodingVm_PropertyChanged + UpdateCodingUi nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    // Phase 6.1.F Sub-A: UpdateCodingCurrentCode + SyncVideoToCodingMeter
    // nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>
    /// Hält die Overlay-Zeichenfläche exakt auf VideoView-Größe.
    /// Wichtig für Popup-Overlay über VLC (HwndHost/Airspace).
    /// </summary>
    /// <summary>
    /// Berechnet die tatsaechliche Video-Renderflaeche innerhalb des VideoView-Containers.
    /// VLC behaelt das Aspect-Ratio bei und zentriert das Video (Letterboxing/Pillarboxing).
    /// </summary>
    private (double OffX, double OffY, double W, double H) GetVideoViewRenderRect()
    {
        double containerW = VideoView.ActualWidth;
        double containerH = VideoView.ActualHeight;

        if (double.IsNaN(containerW) || containerW <= 1 ||
            double.IsNaN(containerH) || containerH <= 1)
            return (0, 0, containerW, containerH);

        try
        {
            if (_player?.Media is { } media)
            {
                foreach (var track in media.Tracks)
                {
                    if (track.TrackType == TrackType.Video && track.Data.Video.Width > 0)
                    {
                        uint vidW = track.Data.Video.Width;
                        uint vidH = track.Data.Video.Height;
                        // SAR (nicht-quadratische Pixel) beruecksichtigen
                        if (track.Data.Video.SarNum > 0 && track.Data.Video.SarDen > 0)
                            vidW = (uint)(vidW * track.Data.Video.SarNum / track.Data.Video.SarDen);

                        double videoAspect = (double)vidW / vidH;
                        double containerAspect = containerW / containerH;

                        if (videoAspect > containerAspect)
                        {
                            // Video breiter → volle Breite, Letterboxing oben/unten
                            double renderW = containerW;
                            double renderH = containerW / videoAspect;
                            return (0, (containerH - renderH) / 2, renderW, renderH);
                        }
                        else
                        {
                            // Video hoeher → volle Hoehe, Pillarboxing links/rechts
                            double renderH = containerH;
                            double renderW = containerH * videoAspect;
                            return ((containerW - renderW) / 2, 0, renderW, renderH);
                        }
                    }
                }
            }
        }
        catch { /* Kein Video-Track verfuegbar */ }

        return (0, 0, containerW, containerH);
    }

    private void UpdateCodingOverlayViewport()
    {
        var (offX, offY, w, h) = GetVideoViewRenderRect();

        if (double.IsNaN(w) || w <= 1 || double.IsNaN(h) || h <= 1)
            return;

        if (Math.Abs(CodingOverlayCanvas.Width - w) > 0.5)
            CodingOverlayCanvas.Width = w;
        if (Math.Abs(CodingOverlayCanvas.Height - h) > 0.5)
            CodingOverlayCanvas.Height = h;

        // Popup-Offset setzen damit Canvas ueber dem tatsaechlichen Video liegt
        CodingOverlayPopup.HorizontalOffset = offX;
        CodingOverlayPopup.VerticalOffset = offY;
    }

    // --- Coding Navigation ---

    // Phase 6.1.F Sub-B: CodingNext_Click + CodingPrevious_Click + CodingToolRect_Click
    // nach PlayerWindow.CodingMode.cs migriert.


    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        double w = CodingOverlayCanvas.ActualWidth, h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            UpdateCodingOverlayViewport();
            w = CodingOverlayCanvas.ActualWidth;
            h = CodingOverlayCanvas.ActualHeight;
            if (w <= 0 || h <= 0)
                return new NormalizedPoint(0.5, 0.5);
        }
        return new NormalizedPoint(pixel.X / w, pixel.Y / h);
    }

    private Point CodingNormToPixel(NormalizedPoint norm)
        => new(norm.X * CodingOverlayCanvas.ActualWidth, norm.Y * CodingOverlayCanvas.ActualHeight);

    private void ClearTransientCodingCanvas(bool clearManualOverlay, bool clearSamMasks = true)
    {
        // SAM-Mask-Tag heisst tatsaechlich "sam_mask_<idx>" (Kleinschreibung,
        // siehe SamMaskRenderer.MaskTag = "sam_mask"). Frueher gefilterten Strings
        // "SamMask_"/"SamLabel_" matchten gar nichts — die Masken wurden also
        // nicht durch ClearTransientCodingCanvas geloescht, sondern blieben bis
        // zum naechsten ClearMasks(...) Aufruf bestehen. Dieser Fix loescht sie
        // jetzt korrekt wenn clearSamMasks=true.
        var remove = CodingOverlayCanvas.Children
            .OfType<FrameworkElement>()
            .Where(el => el.Tag is string tag &&
                         (tag == "tool_badge" ||
                          tag == "overlay_preview" ||
                          tag == "overlay_measure" ||
                          (clearSamMasks && tag.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag, StringComparison.Ordinal)) ||
                          (clearManualOverlay && tag == "overlay_manual")))
            .ToList();

        foreach (var el in remove)
            CodingOverlayCanvas.Children.Remove(el);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay, bool preserveSamMasks = false)
    {
        // preserveSamMasks=true: SAM-Mask-Overlay (Cyan-Konturen nach BBox-Markierung)
        // bleibt erhalten. Notwendig nach ShowSamPreviewAtMarkAsync, sonst werden
        // die gerade gerenderten Masken durch nachfolgendes RedrawCodingCanvas
        // sofort wieder geloescht (User-Beschwerde "wird nicht segmentiert").
        UpdateCodingOverlayViewport();
        ClearTransientCodingCanvas(clearManualOverlay: true, clearSamMasks: !preserveSamMasks);
        RenderAiOverlays();
        RenderReferenceDn();

        if (_codingSchemaManager.IsActive)
            RenderActiveCodingSchema();
        else if (includeManualOverlay && _codingVm?.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

        UpdateToolBadge();
    }


    private void UpdateCodingOverlayInfo(OverlayGeometry? overlay)
    {
        if (overlay == null)
        {
            TxtCodingQ1.Text = "Q1: -"; TxtCodingQ2.Text = "Q2: -";
            TxtCodingClock.Text = "Uhr: -"; TxtCodingArc.Text = "Bogen: -";
            CodingMeasurementPanel.Visibility = Visibility.Collapsed;
            return;
        }

        TxtCodingQ1.Text = overlay.Q1Mm.HasValue ? $"Q1: {overlay.Q1Mm:F1} mm" : "Q1: -";
        TxtCodingQ2.Text = overlay.Q2Mm.HasValue ? $"Q2: {overlay.Q2Mm:F1} mm" : "Q2: -";
        TxtCodingClock.Text = overlay.ClockFrom.HasValue
            ? $"Uhr: {overlay.ClockFrom:F1}" + (overlay.ClockTo.HasValue ? $" -> {overlay.ClockTo:F1}" : "")
            : "Uhr: -";
        TxtCodingArc.Text = overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue
            ? $"Fuellung: {overlay.FillPercent:F1}%"
            : overlay.ArcDegrees.HasValue
                ? (overlay.ToolType == OverlayToolType.PipeBend
                    ? $"Winkel: {overlay.ArcDegrees:F1}\u00B0"
                    : $"Bogen: {overlay.ArcDegrees:F0} deg")
                : "Bogen: -";

        CodingMeasurementPanel.Visibility = Visibility.Visible;
        var parts = new List<string>();

        // Werkzeug-spezifische Anzeige
        if (overlay.ToolType is OverlayToolType.PipeBend or OverlayToolType.PipeDirection)
        {
            if (overlay.ArcDegrees.HasValue) parts.Add($"Winkel:{overlay.ArcDegrees:F1}\u00B0");
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.Level)
        {
            if (overlay.FillPercent.HasValue)
            {
                var label = overlay.Points.Count >= 3
                    ? "Einragung"
                    : overlay.LevelSubMode switch
                    {
                        LevelMode.Water => "Wasser",
                        LevelMode.Obstacle => "Hindernis",
                        _ => "Sediment"
                    };
                parts.Add($"{label}:{overlay.FillPercent:F1}%");
            }
            if (overlay.ClockFrom.HasValue && overlay.Points.Count >= 3)
                parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.LateralCircle)
        {
            if (overlay.Q1Mm.HasValue) parts.Add($"DN:{overlay.Q1Mm:F0}mm");
            if (overlay.DnRatioPercent.HasValue) parts.Add($"{overlay.DnRatioPercent:F0}%");
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.Ruler)
        {
            if (overlay.Q1Mm.HasValue) parts.Add($"Laenge:{overlay.Q1Mm:F1}mm");
        }
        else
        {
            if (overlay.Q1Mm.HasValue) parts.Add($"Q1:{overlay.Q1Mm:F1}mm");
            if (overlay.FillPercent.HasValue) parts.Add($"QV:{overlay.FillPercent:F1}%");
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
            if (overlay.ArcDegrees.HasValue) parts.Add($"{overlay.ArcDegrees:F0}deg");
        }
        TxtCodingMeasurement.Text = string.Join("  |  ", parts);
    }

    // --- Coding Code-Auswahl ---


    /// <summary>Doppelklick auf Import-Eintrag: Video zum Zeitpunkt springen.</summary>
    /// <summary>Maske angeklickt — selektieren, hervorheben, Befundliste synchronisieren.</summary>

    // Phase 6.1.A: CaptureSnapshotAsync nach PlayerWindow.Snapshot.cs migriert.

    // Phase 6.1.F Sub-M: PauseAndAskConfirmation + ConfirmAccept/Edit/Reject + CloseConfirmationAndResume + CloseConfirmationPanel + ResumeAfterConfirmation + UpdateToolBadge + RenderAiOverlays + FadeOutAiOverlayAfterAction + AnalyzeWithOverlayHintAsync nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>
    /// Berechnet den Meterstand aus der aktuellen Videoposition (linear interpoliert).
    /// Fallback wenn kein OSD-Wert verfuegbar.
    /// </summary>
    private double? GetMeterFromVideoPosition()
    {
        if (_player == null || _player.Length <= 0) return null;
        if (_codingVm == null || _codingVm.EndMeter <= 0) return null;
        return Math.Round((_player.Time / (double)_player.Length) * _codingVm.EndMeter, 2);
    }

}














