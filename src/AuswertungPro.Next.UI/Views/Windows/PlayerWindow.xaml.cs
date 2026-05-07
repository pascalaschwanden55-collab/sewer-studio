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



    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        // Phase 5.1.B Etappe 3.M: via DI-Container.
        AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog = null;
        try { catalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>(); } catch { }

        if (catalog is null)
        {
            MessageBox.Show(
                "Schadenscode-Katalog nicht verfuegbar.\n" +
                "Bitte die App neu starten oder KI-Einstellungen pruefen.",
                "Markieren", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Zeit = TimeSpan.FromSeconds(timestampSec),
        };

        if (!string.IsNullOrWhiteSpace(suggestedCode))
            entry.Code = suggestedCode;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        if (!string.IsNullOrWhiteSpace(clockPosition))
            entry.CodeMeta.Parameters["vsa.uhr.von"] = clockPosition;

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry,
            _codingLastOsdMeter ?? GetMeterFromVideoPosition(),
            TimeSpan.FromSeconds(timestampSec));

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            _onEntryCreated?.Invoke(entry);
            ShowOverlay($"Beobachtung erfasst: {entry.Code}", TimeSpan.FromSeconds(4));
        }
    }

    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
    // CODIER-MODUS (integriert im PlayerWindow)
    // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

    private bool _isCodingMode;
    private CodingSessionViewModel? _codingVm;
    private ICodingSessionService? _codingSessionService;
    private IOverlayToolService? _codingOverlayService;
    private readonly SchemaOverlayManager _codingSchemaManager = new();
    private SchemaType? _codingSchemaType;

    // Kalibrierung
    private bool _codingIsCalibrating;
    private NormalizedPoint? _codingCalibStart;

    // Overlay-Vorschau
    private System.Windows.Shapes.Line? _codingPreviewLine;

    // Externes Fenster hat Fokus bekommen (nicht eigener Dialog)
    private bool _deactivatedByExternalWindow;

    // Referenz-DN Toggle
    private bool _showReferenceDn;

    // KI Live-Analyse
    private LiveDetectionService? _codingLiveDetection;
    private EnhancedVisionAnalysisService? _codingEnhancedVision;

    /// <summary>
    /// Kandidaten-Tracker: Schaeden die in der Tiefe erkannt wurden, aber noch
    /// nicht bestaetigt sind. Erst wenn die Kamera naeher kommt (Box wird groesser)
    /// wird der Kandidat zum Befund.
    /// Key: YOLO-Klassenname, Value: (erster Frame-Zeitpunkt, Box-Flaeche-Norm, Confidence)
    /// </summary>
    private readonly Dictionary<string, (double TimeSec, double AreaNorm, double Confidence, string Label)>
        _codingDepthCandidates = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _codingAnalysisCts;
    private bool _codingIsAnalyzing;
    private string _codingAiModelName = string.Empty;
    private bool _codingAiPulseRunning;

    // Live-KI Timer (automatische Analyse alle 5s)
    private DispatcherTimer? _codingLiveAiTimer;
    private DispatcherTimer? _codingLiveAiBlinkTimer;
    private bool _codingLiveAiBlinkState;
    private QualityGateService? _codingQualityGate;

    // Eingabemarker-Zustand
    private enum EingabemarkerPhase { Inactive, Drawing, Input, Analyzing }
    private EingabemarkerPhase _eingabemarkerPhase = EingabemarkerPhase.Inactive;
    private Point _eingabemarkerDragStart; // Canvas-Koordinaten
    private Rect _eingabemarkerRectNorm;   // Normiertes Rechteck (0-1)
    private System.Windows.Shapes.Rectangle? _eingabemarkerPreviewRect;

    // Multi-Model Pipeline (YOLO → DINO → SAM) fuer Einzelframe-Analyse
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameMultiModelService? _codingMultiModel;
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient? _codingVisionClient;

    // Import-Beobachtungen (Referenz-Spalte, nur-lesen)
    private readonly ObservableCollection<CodingEvent> _codingImportEvents = new();

    // Bestaetigungs-Panel: aktuell wartendes Event
    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    // OSD-Meter Timer (liest Meterstand kontinuierlich)
    private DispatcherTimer? _codingOsdTimer;
    private bool _codingOsdReading;
    private int _codingOverlaySuspendDepth;
    private bool _codingOverlayWasOpenBeforeSuspend;

    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            MessageBox.Show(
                "Codier-Modus benoetigt eine Haltung.\n" +
                "Bitte das Video ueber die Datenseite mit einer Haltung oeffnen.",
                "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isTrainingMode)
        {
            MessageBox.Show(
                "Bitte zuerst den Trainings-Modus beenden.",
                "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        EnterCodingMode();
    }

    // Phase 6.1.F Sub-E: EnterCodingMode + LoadExistingProtocolEventsAsImport + ExitCodingMode nach PlayerWindow.CodingMode.cs migriert.



    private void CodingApply_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null || _haltungRecord == null) return;

        // ProtocolDocument aus allen Events aufbauen
        var doc = _haltungRecord.Protocol ?? new ProtocolDocument();
        doc.Current ??= new ProtocolRevision();
        doc.Current.Entries ??= new List<ProtocolEntry>();

        // 1) Aktuelle Coding-Events als "Soll-Zustand" (korrigierte Werte) aufbauen
        var eventEntries = _codingVm.Events
            .Select(ev => ev.Entry)
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Code))
            .GroupBy(e => e.EntryId)
            .Select(g => g.Last())
            .ToDictionary(e => e.EntryId, e => e);

        // 2) Vorhandene Protokoll-Eintraege updaten oder als geloescht markieren
        var existingById = doc.Current.Entries.ToDictionary(e => e.EntryId, e => e);
        foreach (var existing in doc.Current.Entries)
        {
            if (eventEntries.TryGetValue(existing.EntryId, out var updated))
            {
                CopyProtocolEntryValues(updated, existing);
                existing.IsDeleted = false;
            }
            else
            {
                existing.IsDeleted = true;
            }
        }

        // 3) Neue Eintraege aus Coding-Events anhaengen
        foreach (var kv in eventEntries)
        {
            if (!existingById.ContainsKey(kv.Key))
                doc.Current.Entries.Add(kv.Value);
        }

        _haltungRecord.Protocol = doc;
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;

        // Primaere Schaeden ins DataGrid uebertragen
        SyncCodingToPrimaryDamages(doc);

        // Feedback-Loop: CodingEvents → TrainingSamples persistieren
        // (Im PlayerWindow wird CompleteSession() nicht aufgerufen,
        //  daher muss die Training-Persistierung hier erfolgen.)
        PersistCodingEventsAsTrainingSamples();

        var message = _codingVm.Events.Count == 0
            ? "Primaere Schaeden geleert"
            : $"{_codingVm.Events.Count} Ereignisse in Primaere Schaeden uebernommen";
        ShowOverlay(message, TimeSpan.FromSeconds(4));
    }

    private static void CopyProtocolEntryValues(ProtocolEntry source, ProtocolEntry target)
    {
        target.Code = source.Code;
        target.Beschreibung = source.Beschreibung;
        target.MeterStart = source.MeterStart;
        target.MeterEnd = source.MeterEnd;
        target.IsStreckenschaden = source.IsStreckenschaden;
        target.Mpeg = source.Mpeg;
        target.Zeit = source.Zeit;
        target.Source = source.Source;
        target.CodeMeta = source.CodeMeta;
        target.Ai = source.Ai;
        target.FotoPaths = source.FotoPaths?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Konvertiert die KI-Events aus dem Codiermodus in TrainingSamples
    /// und speichert sie via TrainingSamplesStore.
    /// Schliesst den Feedback-Loop im PlayerWindow (analog zu CodingSessionService.CompleteSession).
    /// </summary>
    /// <summary>
    /// Speichert ein einzelnes CodingEvent sofort als TrainingSample.
    /// Wird nach jeder Codierung aufgerufen — nicht erst beim Beenden.
    /// </summary>
    private void PersistSingleEventAsTrainingSample(CodingEvent ev)
    {
        if (ev.Entry == null || string.IsNullOrWhiteSpace(ev.Entry.Code)) return;
        try
        {
            var caseId = _codingVm?.HaltungName ?? "unknown";
            var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
            var sample = AuswertungPro.Next.Application.Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);
            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            AuswertungPro.Next.Application.Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(new List<AuswertungPro.Next.Domain.Ai.Training.TrainingSample> { sample })
                .SafeFireAndForget("TrainingSaveSingle");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Training] Einzelspeicherung Fehler: {ex.Message}");
        }
    }

    private void PersistCodingEventsAsTrainingSamples()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0) return;
        try
        {
            var caseId = _codingVm.HaltungName ?? "unknown";
            var samples = new System.Collections.Generic.List<AuswertungPro.Next.Domain.Ai.Training.TrainingSample>();
            foreach (var ev in _codingVm.Events)
            {
                var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
                var sample = AuswertungPro.Next.Application.Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);

                // Alle Fotos als zusaetzliche Lernbilder referenzieren
                // (Foto 1 = FramePath, Foto 2+ = AdditionalFrames)
                if (ev.Entry.FotoPaths.Count > 1)
                {
                    sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                    for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                        sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
                }

                samples.Add(sample);
            }
            if (samples.Count > 0)
                AuswertungPro.Next.Application.Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(samples)
                    .SafeFireAndForget("TrainingSave");
        }
        catch (Exception ex)
        {
            // Uebernahme darf nie blockiert werden, aber Fehler loggen
            System.Diagnostics.Debug.WriteLine($"[Training] Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Stellt sicher, dass Haltungslaenge_m gesetzt ist.
    /// Fallback-Kette: Haltungslaenge_m → Laenge_m → DamageOverlay → Protokoll BCE → manuelle Eingabe.
    /// </summary>
    private void EnsureHaltungslaenge(HaltungRecord record)
    {
        // Bereits vorhanden?
        if (HasValidLength(record, "Haltungslaenge_m"))
            return;

        // Fallback 1: Laenge_m
        if (HasValidLength(record, "Laenge_m"))
        {
            record.SetFieldValue("Haltungslaenge_m",
                record.GetFieldValue("Laenge_m"),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 2: DamageOverlay (wurde beim Oeffnen aus dem Protokoll berechnet)
        if (_damageOverlay != null && _damageOverlay.PipeLengthMeters > 0)
        {
            record.SetFieldValue("Haltungslaenge_m",
                _damageOverlay.PipeLengthMeters.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Domain.Models.FieldSource.Legacy, userEdited: false);
            return;
        }

        // Fallback 3: Protokoll BCE-Eintrag (Rohrende) → hoechster Meter
        if (record.Protocol?.Current?.Entries is { Count: > 0 } entries)
        {
            var maxMeter = entries
                .Where(e => e.MeterStart.HasValue && e.MeterStart.Value > 0)
                .Select(e => e.MeterStart!.Value)
                .DefaultIfEmpty(0)
                .Max();

            if (maxMeter > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    maxMeter.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Legacy, userEdited: false);
                return;
            }
        }

        // Fallback 4: Benutzer manuell fragen
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Haltungslaenge konnte nicht ermittelt werden.\n" +
            "Bitte Haltungslaenge in Meter eingeben (z.B. 45.3):",
            "Haltungslaenge eingeben", "");

        if (!string.IsNullOrWhiteSpace(input))
        {
            var normalized = input.Trim().Replace(',', '.');
            if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                record.SetFieldValue("Haltungslaenge_m",
                    val.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Domain.Models.FieldSource.Manual, userEdited: true);
            }
        }
    }

    // Phase 6.1.C: HasValidLength nach PlayerWindow.Helpers.cs migriert.

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

    private async void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;

        // Video pausieren
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

            var timelineMeter = _codingVm.CurrentMeter;
            if (_player.Length > 0 && _codingVm.EndMeter > 0)
            {
                timelineMeter = Math.Round((_player.Time / (double)_player.Length) * _codingVm.EndMeter, 2);
            }

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = Math.Round(Math.Max(0, osdMeter ?? _codingLastOsdMeter ?? timelineMeter), 2);

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Manual,
                MeterStart = meterValue,
                MeterEnd = meterValue,
                Zeit = videoZeit
            };

            if (_codingVm.CurrentOverlay != null)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta();
                if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                    entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                    entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                    entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.Q2Mm.HasValue)
                    entry.CodeMeta.Parameters["vsa.q2"] = _codingVm.CurrentOverlay.Q2Mm.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.ArcDegrees.HasValue
                    && (_codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend
                        || _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeDirection))
                    entry.CodeMeta.Parameters["vsa.winkel"] = _codingVm.CurrentOverlay.ArcDegrees.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.FillPercent.HasValue)
                {
                    var key = _codingVm.CurrentOverlay.ToolType == OverlayToolType.Level
                              && _codingVm.CurrentOverlay.Points.Count >= 3
                        ? "vsa.querschnitt.prozent"
                        : "vsa.fuellgrad.prozent";
                    entry.CodeMeta.Parameters[key] = _codingVm.CurrentOverlay.FillPercent.Value.ToString("F1");
                }
            }

            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
                entry, meterValue, videoZeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoZeit)
            {
                Owner = this,
                // Live-Snapshot: Aktuelles VLC-Bild statt ffmpeg-Extraktion
                LiveSnapshotProvider = () =>
                {
                    var snapPath = Path.Combine(Path.GetTempPath(),
                        $"coding_live_{Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };

            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                entry.Code = result.Code;
                entry.Beschreibung = result.Beschreibung;
                entry.CodeMeta = result.CodeMeta;
                entry.MeterStart = result.MeterStart;
                entry.MeterEnd = result.MeterEnd;
                entry.Zeit = result.Zeit;
                entry.IsStreckenschaden = result.IsStreckenschaden;
                entry.FotoPaths = result.FotoPaths;

                // Kein automatischer Snapshot hier — Foto wird manuell per "Foto"-Button
                // oder automatisch durch die KI-Analyse eingefuegt, wenn ein sinnvoller
                // Frame vorliegt (nicht die Dateneinblendung am Videoanfang).

                var createdEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

                // Manuell codiert = direkt akzeptiert (User hat selbst entschieden)
                createdEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = entry.Code,
                    Confidence = 1.0,
                    Reason = "Manuell codiert",
                    Decision = CodingUserDecision.Accepted
                };

                // Sperrliste: KI soll diesen Befund nicht erneut melden
                _rejectedFindings.Add(MakeRejectionKey(entry.Code, entry.MeterStart ?? 0));

                // KI-Overlays raeumen — manuell codiert heisst erledigt
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                DetectionCanvas.Children.Clear();

                RefreshCodingEventsList();
                LstCodingEvents.SelectedItem = createdEvent;

                _codingSchemaManager.Cancel();
                _codingVm.CurrentOverlay = null;
                RedrawCodingCanvas(includeManualOverlay: false);
                TxtCodingSelectedCode.Text = "";
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }
    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        // Nur verwenden wenn Code manuell gesetzt (nicht ueber CodingSelectCode_Click,
        // denn dort wird AddEvent bereits direkt aufgerufen)
        if (_codingVm == null || string.IsNullOrWhiteSpace(_codingVm.SelectedCode)) return;

        // Videozeit vom Player uebernehmen
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);

        // Foto vom Video-Frame
        var entry = new ProtocolEntry
        {
            Code = _codingVm.SelectedCode,
            Beschreibung = _codingVm.SelectedCodeDescription,
            MeterStart = _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            Zeit = TimeSpan.FromMilliseconds(_player.Time),
            Source = ProtocolEntrySource.Manual
        };

        if (_codingVm.CurrentOverlay != null)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q2Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q2"] = _codingVm.CurrentOverlay.Q2Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ArcDegrees.HasValue
                && (_codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend
                    || _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeDirection))
                entry.CodeMeta.Parameters["vsa.winkel"] = _codingVm.CurrentOverlay.ArcDegrees.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.FillPercent.HasValue)
            {
                var key = _codingVm.CurrentOverlay.ToolType == OverlayToolType.Level
                          && _codingVm.CurrentOverlay.Points.Count >= 3
                    ? "vsa.querschnitt.prozent"
                    : "vsa.fuellgrad.prozent";
                entry.CodeMeta.Parameters[key] = _codingVm.CurrentOverlay.FillPercent.Value.ToString("F1");
            }
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath != null)
            entry.FotoPaths.Add(fotoPath);

        var manualEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

        // Manuell codiert = direkt akzeptiert (User hat selbst entschieden)
        manualEvent.AiContext = new CodingEventAiContext
        {
            SuggestedCode = entry.Code,
            Confidence = 1.0,
            Reason = "Manuell codiert",
            Decision = CodingUserDecision.Accepted
        };

        // Sperrliste: KI soll diesen Befund nicht erneut melden
        _rejectedFindings.Add(MakeRejectionKey(entry.Code, entry.MeterStart ?? 0));

        // KI-Overlays raeumen — manuell codiert heisst erledigt
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        DetectionCanvas.Children.Clear();

        // Nach Meter sortiert anzeigen
        RefreshCodingEventsList();

        // Reset
        _codingSchemaManager.Cancel();
        _codingVm.CurrentOverlay = null;
        _codingVm.SelectedCode = "";
        _codingVm.SelectedCodeDescription = "";
        RedrawCodingCanvas(includeManualOverlay: false);
        TxtCodingSelectedCode.Text = "";
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
    }

    // --- Coding Foto-Aufnahme vom Video ---

    // Phase 6.1.A: CodingCaptureSnapshot nach PlayerWindow.Snapshot.cs migriert.

    // --- Coding PDF-Export ---

    private void CodingOfferPdfExport(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var result = MessageBox.Show(
            $"Codier-Session abgeschlossen ({doc.Current.Entries.Count} Ereignisse).\n\n" +
            "MÃƒÂ¶chten Sie jetzt ein PDF-Protokoll mit Grafik und Fotos erstellen?",
            "PDF-Protokoll erstellen",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "PDF-Protokoll speichern",
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Protokoll_{_haltungRecord.GetFieldValue("Haltungsname") ?? "Haltung"}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dlg.ShowDialog() != true) return;

            // Projektordner ermitteln (fuer Logo-Suche und relative Pfade)
            var projectRoot = "";
            if (!string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath))
                projectRoot = Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath) ?? "";

            // Logo suchen
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                IncludePhotos = true,
                IncludeHaltungsgrafik = true,
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
            var pdf = App.Resolve<AuswertungPro.Next.Application.Reports.ProtocolPdfExporter>().BuildHaltungsprotokollPdf(
                project!, _haltungRecord, doc, projectRoot, options);
            File.WriteAllBytes(dlg.FileName, pdf);

            // PDF oeffnen
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
            catch { }

            ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- Coding: Doppelklick zum Bearbeiten ---

    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        // Video pausieren waehrend Bearbeitung
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
        {
            Owner = this,
            LiveSnapshotProvider = () =>
            {
                var snapPath = Path.Combine(Path.GetTempPath(),
                    $"coding_live_{Guid.NewGuid():N}.png");
                return TakeSnapshotSafe(snapPath) ? snapPath : null;
            }
        };

        bool? dialogResult;
        try
        {
            dialogResult = dlg.ShowDialog();
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (dialogResult == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            // Meter aktualisieren falls geaendert
            codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
            _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);

            // Events-Liste neu binden um Anzeige zu aktualisieren
            RefreshCodingEventsList();
        }
    }

    /// <summary>
    /// Erstellt Foto vom aktuellen Video-Frame fuer das ausgewaehlte Event (max 2 Fotos).
    /// </summary>
    private void CodingTakePhotoForSelectedEvent()
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        var entry = codingEvent.Entry;
        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath == null)
        {
            ShowOverlay("Foto konnte nicht aufgenommen werden", TimeSpan.FromSeconds(3));
            return;
        }

        if (entry.FotoPaths.Count >= 2)
        {
            entry.FotoPaths[1] = fotoPath;
            ShowOverlay($"Foto 2 ersetzt: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }
        else
        {
            entry.FotoPaths.Add(fotoPath);
            ShowOverlay($"Foto {entry.FotoPaths.Count}: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }

        RefreshCodingEventsList();
    }

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ce)
            CodingEvents_DoubleClick(sender, null!); // Gleiche Logik wie Doppelklick
    }

    private void CodingEventShowPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        var entry = codingEvent.Entry;
        if (entry.FotoPaths.Count == 0)
        {
            ShowOverlay("Keine Fotos vorhanden. Doppelklick zum Bearbeiten.", TimeSpan.FromSeconds(3));
            return;
        }

        // Einfaches Foto-Vorschau-Fenster
        var win = new Window
        {
            Title = $"Fotos - {entry.Code} @ {codingEvent.MeterAtCapture:F2}m",
            Width = 640, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var projectFolder = !string.IsNullOrEmpty(App.Resolve<AppSettings>().LastProjectPath)
            ? Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath) ?? ""
            : "";

        foreach (var fotoPath in entry.FotoPaths)
        {
            var resolved = Path.IsPathRooted(fotoPath) && File.Exists(fotoPath)
                ? fotoPath
                : (File.Exists(Path.Combine(projectFolder, fotoPath)) ? Path.Combine(projectFolder, fotoPath) : null);

            if (resolved == null) continue;

            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(resolved, UriKind.Absolute);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = 360;
                bi.EndInit();
                bi.Freeze();

                var img = new System.Windows.Controls.Image
                {
                    Source = bi,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(4),
                    MaxHeight = 360
                };
                panel.Children.Add(img);
            }
            catch { /* Bild nicht ladbar */ }
        }

        win.Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        WindowStateManager.Track(win);
        win.Show();
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (_player != null && codingEvent.VideoTimestamp.TotalMilliseconds > 0)
            _player.Time = (long)codingEvent.VideoTimestamp.TotalMilliseconds;
    }

    /// <summary>
    /// Streckenschaden schliessen: Erstellt einen identischen Eintrag mit aktuellem Meterstand
    /// als Ende-Markierung. VSA-Konvention: gleicher Code, MeterEnd = aktuelle Position.
    /// </summary>
    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent startEvent) return;
        if (_codingSessionService == null || _codingVm == null) return;

        // Aktuellen Meterstand als Endpunkt
        double currentMeter = _codingVm.CurrentMeter;
        if (currentMeter <= (startEvent.MeterAtCapture + 0.01))
        {
            MessageBox.Show(
                "Der aktuelle Meterstand muss groesser sein als der Anfang des Streckenschadens.",
                "Streckenschaden", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Start-Event als Streckenschaden markieren
        startEvent.Entry.IsStreckenschaden = true;
        startEvent.Entry.MeterEnd = currentMeter;

        // Ende-Event erstellen (identischer Code)
        var endEntry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
        {
            Code = startEvent.Entry.Code,
            Beschreibung = startEvent.Entry.Beschreibung + " (Ende)",
            MeterStart = currentMeter,
            IsStreckenschaden = true,
            Source = startEvent.Entry.Source,
            CodeMeta = startEvent.Entry.CodeMeta
        };

        var endEvent = _codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;

        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites Events.Add() — sonst doppelt!
        RefreshCodingEventsList();

        // Status
        SetCodingAiState(
            $"Streckenschaden geschlossen: {startEvent.Entry.Code} {startEvent.MeterAtCapture:F2}m – {currentMeter:F2}m",
            Color.FromRgb(0x22, 0xC5, 0x5E), "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        MessageBoxResult confirm;
        try
        {
            confirm = MessageBox.Show($"Ereignis '{codingEvent.Entry.Code}' loeschen?", "Loeschen",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (confirm != MessageBoxResult.Yes) return;

        _codingSessionService?.RemoveEvent(codingEvent.EventId);
        _codingVm?.Events.Remove(codingEvent);
        if (_codingVm != null && ReferenceEquals(_codingVm.SelectedDefect, codingEvent))
            _codingVm.SelectedDefect = null;
        CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }

    // Phase 6.1.F Sub-D: RefreshCodingEventsList nach PlayerWindow.CodingMode.cs migriert.

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Defekt-Detail-Panel, Aktionsbuttons, Statistik
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Vorheriges vergroessertes Item zuruecksetzen (nicht wenn Maske die Selektion steuert)
        if (!_enlargeSuppressShrink)
            ShrinkEnlargedListItem();

        if (LstCodingEvents.SelectedItem is CodingEvent ev)
        {
            if (_codingVm != null) _codingVm.SelectedDefect = ev;
            UpdateCodingDefectDetailPanel(ev);
            UpdateInlineDefectDetail(ev);

            // Maske im Bild hervorheben die zum selektierten Befund gehoert
            SyncMaskToBefundListe(ev);
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
            HideInlineDefectDetail();
        }
    }

    /// <summary>Hebt die Maske hervor die zum selektierten CodingEvent gehoert.</summary>
    private void SyncMaskToBefundListe(CodingEvent ev)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks) return;
        var evCode = ev.Entry.Code?.ToUpperInvariant() ?? "";
        if (string.IsNullOrEmpty(evCode)) return;

        // Maske finden die zum Code passt
        for (int i = 0; i < masks.Count; i++)
        {
            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{i}";
            // Nur noch sichtbare Masken beruecksichtigen
            if (!CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(el => tag.Equals(el.Tag as string)))
                continue;

            var maskCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(masks[i].Label)?.ToUpperInvariant() ?? "";
            if (evCode == maskCode || evCode.StartsWith(maskCode) || maskCode.StartsWith(evCode))
            {
                _selectedMaskIndex = i;
                HighlightSelectedMask(i);
                return;
            }
        }
    }

    /// <summary>Mittlere Spalte: kompakte Defekt-Details inline anzeigen.</summary>
    private void UpdateInlineDefectDetail(CodingEvent ev)
    {
        TxtInlineDetailCode.Text = ev.Entry.Code;
        TxtInlineDetailDesc.Text = ev.Entry.Beschreibung;
        TxtInlineDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtInlineDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtInlineDetailConfidence.Foreground =
                ViewModels.Windows.CodingSessionViewModel.GetConfidenceBrush(conf);
        }
        else
        {
            TxtInlineDetailConfidence.Text = "\u2013";
            TxtInlineDetailConfidence.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
        }

        // Accept/Reject immer verfuegbar — auch fuer manuell erstellte Events
        BtnInlineAccept.Visibility = Visibility.Visible;
        BtnInlineReject.Visibility = Visibility.Visible;

        var status = ViewModels.Windows.CodingSessionViewModel.GetDefectStatus(ev);
        TxtInlineDetailStatus.Text = CodingStatusToDisplayText(status);

        // Mittlere Spalte einblenden
        CodingDefectDetailInline.Visibility = Visibility.Visible;
        ColDefectDetail.Width = new GridLength(180);
    }

    private void HideInlineDefectDetail()
    {
        CodingDefectDetailInline.Visibility = Visibility.Collapsed;
        ColDefectDetail.Width = new GridLength(0);
    }

    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
        {
            // Run/Inline-Elemente sind kein Visual — LogicalTreeHelper als Fallback
            dep = dep is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }

        if (dep is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>Doppelklick auf Import-Eintrag: Video zum Zeitpunkt springen.</summary>
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    /// <summary>Context-Menü: Zum Zeitpunkt springen.</summary>
    private void ImportSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void SeekToImportEvent(CodingEvent importEvent)
    {
        if (_player != null && importEvent.VideoTimestamp.TotalMilliseconds > 0)
            _player.Time = (long)importEvent.VideoTimestamp.TotalMilliseconds;
        else if (_codingSessionService != null && importEvent.MeterAtCapture > 0)
        {
            _codingSessionService.MoveToMeter(importEvent.MeterAtCapture);
            _codingNavPending = true;
            SyncVideoToCodingMeter();
        }
    }

    /// <summary>
    /// Context-Menü: Import-Eintrag als Training-Sample bestätigen.
    /// Springt zum Zeitpunkt, macht einen Snapshot und erstellt eine Lehrer-Annotation.
    /// </summary>
    private async void ImportConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;

        // 1. Zum Zeitpunkt springen
        SeekToImportEvent(importEvent);
        await Task.Delay(200); // Kurz warten bis Frame gerendert ist

        // 2. Frame capturen
        if (!TryTakeSnapshot(out var snapshotPath) || !System.IO.File.Exists(snapshotPath))
        {
            MessageBox.Show("Frame konnte nicht aufgenommen werden.\nBitte prüfen Sie ob das Video läuft.",
                "Import bestätigen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 3. Bild in teacher_images kopieren
        var imagesDir = AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        // 4. Lehrer-Annotation erstellen
        var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = Domain.Models.OverlayToolType.None,
            FullFramePath = destFrame,
        };

        await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

        // 5. Visuelles Feedback
        try { System.IO.File.Delete(snapshotPath); } catch { }
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = $"✓ {importEvent.Entry.Code} @ {importEvent.MeterAtCapture:F1}m bestätigt";
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) => { OsdMeterBadge.Visibility = Visibility.Collapsed; resetTimer.Stop(); };
        resetTimer.Start();
    }

    // Phase 6.1.F Sub-C: CodingAcceptDefect_Click + CodingEditDefect_Click +
    // CodingRejectDefect_Click nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Defekt-Detail-Panel mit Werten des ausgewaehlten Events befuellen.</summary>
    /// Details werden jetzt oben im KI-BEFUNDE Panel angezeigt — unteres Panel bleibt collapsed.
    // Phase 6.1.F Sub-D: UpdateCodingDefectDetailPanel + ColorizeCodingEventListItems + FindCodingChild + UpdateCodingStatistics + ShrinkEnlargedListItem nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.C: CodingStatusToDisplayText nach PlayerWindow.Helpers.cs migriert.


    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>

    /// <summary>Statistiken im Seitenpanel aktualisieren (direkt berechnet).</summary>

    // --- Coding: Existierende Protokoll-Eintraege laden ---

    /// <summary>
    /// Laedt existierende Protokoll-Eintraege aus der Haltung (Import/DataGrid) in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEntries()
    {
        if (_codingVm == null || _haltungRecord == null) return;

        var entries = _haltungRecord.Protocol?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        if (entries == null || entries.Count == 0) return;

        foreach (var entry in entries.OrderBy(e => e.MeterStart ?? 0))
        {
            var codingEvent = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            };
            _codingVm.Events.Add(codingEvent);
        }
    }

    // --- Coding: Primaere Schaeden synchronisieren ---

    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var entries = doc.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries == null || entries.Count == 0)
        {
            _haltungRecord.SetFieldValue("Primaere_Schaeden", "", FieldSource.Manual, userEdited: true);
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            return;
        }

        // Zeilen fuer Primaere_Schaeden aufbauen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            if (!seen.Add($"{code.ToUpperInvariant()}|{meterKey}")) continue;

            var parts = new List<string>();
            if (meter.HasValue) parts.Add($"{meter.Value:0.00}m");
            parts.Add(code);
            if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
                parts.Add(entry.Beschreibung.Trim().Replace("\r", "").Replace("\n", " "));

            if (entry.CodeMeta?.Parameters != null)
            {
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q1", out var q1) && !string.IsNullOrWhiteSpace(q1))
                    parts.Add($"Q1={q1}");
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q2", out var q2) && !string.IsNullOrWhiteSpace(q2))
                    parts.Add($"Q2={q2}");
            }

            lines.Add(string.Join(" ", parts));
        }

        var primaryText = string.Join("\n", lines);
        _haltungRecord.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
    }

    // --- Coding: Protokoll-Vorschau (nachtraeglich bearbeitbar) ---

    private void ShowCodingProtocolPreview(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var result = MessageBox.Show(
            $"{doc.Current.Entries.Count} Beobachtungen protokolliert.\n\n" +
            "Protokoll jetzt anzeigen und bearbeiten?\n" +
            "(Aenderungen werden in Primaere Schaeden uebernommen)",
            "Codier-Session abgeschlossen",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
        if (project == null) return;

        var projectFolder = !string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath)
            ? Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath)
            : null;

        var dlg = new Views.ProtocolObservationsWindow(
            _haltungRecord, project, _videoPath, projectFolder,
            markDirty: () =>
            {
                _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            });
        dlg.Owner = this;
        dlg.ShowDialog();

        // Nach Bearbeitung: Primaere Schaeden erneut synchronisieren
        if (_haltungRecord.Protocol != null)
            SyncCodingToPrimaryDamages(_haltungRecord.Protocol);

        // PDF anbieten
        CodingOfferPdfExport(_haltungRecord.Protocol ?? doc);
    }

    // --- Coding: OSD-Timer (liest Meterstand kontinuierlich) ---

    // Phase 6.1.F Sub-A: StartCodingOsdTimer + StopCodingOsdTimer
    // nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.F Sub-F: InitCodingAi + CodingAnalyzeFrame_Click + RunCodingAnalysisAsync nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    // ═══════════════════════════════════════════════
    // Eingabemarker: Klick → Stichwort → KI
    // ═══════════════════════════════════════════════

    /// <summary>Eingabemarker Button: Video pausieren, Rechteck-Zeichenmodus aktivieren.</summary>
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
    {
        if (BtnEingabemarker.IsChecked == true)
        {
            // Aktivieren: Video pausieren, CodingOverlayPopup oeffnen (VLC Airspace)
            _player.SetPause(true);
            _eingabemarkerPhase = EingabemarkerPhase.Drawing;
            EnsureMarkOverlayReady();
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Cross;
            SetCodingAiState("Eingabemarker: Rechteck um die Beobachtung ziehen",
                Color.FromRgb(0x3B, 0x82, 0xF6), "Klicken + Ziehen = Bereich markieren");
        }
        else
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Eingabemarker abbrechen und Zustand zuruecksetzen.</summary>
    private void CancelEingabemarker()
    {
        _eingabemarkerPhase = EingabemarkerPhase.Inactive;
        BtnEingabemarker.IsChecked = false;
        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        if (_eingabemarkerPreviewRect != null)
        {
            CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect);
            _eingabemarkerPreviewRect = null;
        }
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    /// <summary>MouseDown auf CodingOverlayCanvas im Eingabemarker-Drawing-Modus: Drag starten.</summary>
    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;

        _eingabemarkerDragStart = canvasPos;
        CodingOverlayCanvas.CaptureMouse();

        // Vorschau-Rechteck erstellen
        _eingabemarkerPreviewRect = new System.Windows.Shapes.Rectangle
        {
            Stroke = System.Windows.Media.Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
            Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(_eingabemarkerPreviewRect, canvasPos.X);
        Canvas.SetTop(_eingabemarkerPreviewRect, canvasPos.Y);
        _eingabemarkerPreviewRect.Width = 0;
        _eingabemarkerPreviewRect.Height = 0;
        CodingOverlayCanvas.Children.Add(_eingabemarkerPreviewRect);
    }

    /// <summary>MouseMove waehrend Eingabemarker Rechteck-Drag: Vorschau aktualisieren.</summary>
    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing || _eingabemarkerPreviewRect == null) return;

        double x = Math.Min(_eingabemarkerDragStart.X, canvasPos.X);
        double y = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y);
        double w = Math.Abs(canvasPos.X - _eingabemarkerDragStart.X);
        double h = Math.Abs(canvasPos.Y - _eingabemarkerDragStart.Y);

        Canvas.SetLeft(_eingabemarkerPreviewRect, x);
        Canvas.SetTop(_eingabemarkerPreviewRect, y);
        _eingabemarkerPreviewRect.Width = w;
        _eingabemarkerPreviewRect.Height = h;
    }

    /// <summary>MouseUp: Rechteck finalisieren → Phase wechseln → Popup anzeigen.</summary>
    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;
        CodingOverlayCanvas.ReleaseMouseCapture();

        double canvasW = CodingOverlayCanvas.ActualWidth;
        double canvasH = CodingOverlayCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { CancelEingabemarker(); return; }

        // Normiertes Rechteck berechnen
        double x1 = Math.Min(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y1 = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;
        double x2 = Math.Max(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y2 = Math.Max(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;

        // Mindestgroesse pruefen
        if ((x2 - x1) < 0.02 || (y2 - y1) < 0.02) { CancelEingabemarker(); return; }

        _eingabemarkerRectNorm = new Rect(x1, y1, x2 - x1, y2 - y1);

        // Phase wechseln: KEINE Canvas-Klicks mehr → Popup sicher bedienbar
        _eingabemarkerPhase = EingabemarkerPhase.Input;
        CodingOverlayCanvas.IsHitTestVisible = false; // Canvas ignoriert jetzt Klicks
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;

        // Popup in der Toolbar anzeigen (kein VLC Airspace Problem)
        EingabemarkerPopup.Visibility = Visibility.Visible;

        // Freitext-Feld fokussieren
        TxtEingabemarker.Text = "";
        CmbEingabemarker.SelectedIndex = -1;
        Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

        SetCodingAiState("Beschreibung eingeben oder Stichwort waehlen, dann Enter",
            Color.FromRgb(0x3B, 0x82, 0xF6), "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

    /// <summary>Enter in der Stichwort-ComboBox → KI-Analyse starten.</summary>
    private void CmbEingabemarker_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter) return;
        SubmitEingabemarker();
    }

    /// <summary>Auswahl in der Schnellauswahl-ComboBox → Text uebernehmen und absenden.</summary>
    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nur wenn Popup sichtbar und etwas ausgewaehlt wurde
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker();
        }
    }

    /// <summary>Freitext oder Stichwort absenden → Code ableiten oder KI-Analyse starten.</summary>
    private async void SubmitEingabemarker()
    {
        string keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        // VSA-Hauptcode ableiten: Exakte Stichwörter ODER Freitext-Heuristik
        // Freitext wie "beule unten", "riss bei 3 uhr" wird durch InferCodeFromLabel erkannt
        string? codeHint = keyword.ToUpperInvariant() switch
        {
            "ROHRANFANG" => "BCD",
            "ROHRENDE" => "BCE",
            "ANSCHLUSS" => "BCA",
            "BOGEN" => "BCC",
            "RISS" => "BAB",
            "BRUCH" => "BAC",
            "VERFORMUNG" => "BAA",
            "OBERFLAECHENSCHADEN" => "BAF",
            "VERSATZ" or "VERSCHIEBUNG" => "BAJ",
            "WURZELN" or "BEWUCHS" => "BBA",
            "ABLAGERUNG" => "BBC",
            "INKRUSTATION" => "BBB",
            "WASSERSTAND" => "BDD",
            "ABBRUCH" => "BDC",
            // Kein exaktes Stichwort → Freitext-Heuristik (z.B. "beule unten", "riss bei 3 uhr")
            _ => AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(keyword)
        };

        try
        {
            // Duplikat-Check VOR der Analyse
            if (_codingVm != null && codeHint != null)
            {
                double checkMeter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                // BCD/BCE/BDC: Einmal-Codes — Meter egal
                bool isEinmalCode = codeHint is "BCD" or "BCE" or "BDC";
                var existingDup = _codingVm.Events.FirstOrDefault(e =>
                    CodesMatchForDedup(e.Entry.Code, codeHint) &&
                    (isEinmalCode || Math.Abs(e.MeterAtCapture - checkMeter) < 1.0));
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m — Duplikat",
                        Color.FromRgb(0xF5, 0x9E, 0x0B), "");
                    return;
                }
            }

            // Bekannter Hauptcode → Event SOFORT erzeugen (kein Warten auf Qwen)
            if (codeHint != null && _codingVm != null && _codingSessionService != null)
            {
                double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var entry = new ProtocolEntry
                {
                    Source = ProtocolEntrySource.Ai,
                    Code = codeHint,
                    Beschreibung = label,
                    MeterStart = meter,
                    Zeit = videoTime
                };

                // Foto vom aktuellen Frame
                var fotoPath = CodingCaptureSnapshot(entry);
                if (fotoPath != null) entry.FotoPaths.Add(fotoPath);

                var ev = _codingSessionService.AddEvent(entry, _codingVm.CurrentOverlay);
                ev.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = codeHint,
                    Confidence = 1.0,
                    Reason = $"Eingabemarker: {keyword}",
                    Decision = CodingUserDecision.Accepted
                };
                // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
                // KEIN explizites _codingVm.Events.Add() — sonst doppelt!
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev);
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "");
            }
            else
            {
                // Kein Hauptcode erkannt → Qwen analysieren lassen
                SetCodingAiState($"KI analysiert: \"{keyword}\" ...",
                    Color.FromRgb(0xF5, 0x9E, 0x0B), "Qwen analysiert");
                await RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null);
            }
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44), "");
        }
        finally
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Detection-Overlays aufraumen (Boxen, Labels, Findings-Liste).</summary>
    // Phase 6.1.E: ClearDetectionOverlays nach PlayerWindow.LiveDetection.cs migriert.


    /// <summary>
    /// <summary>
    /// Sammelt alle Import-Eintraege als Erwartungshorizont fuer die KI-Analyse.
    /// Die KI erhaelt die bekannten VSA-Codes und kann sie zuweisen statt "???".
    /// </summary>
    // ── Multi-Model Rendering (YOLO → DINO → SAM) ────────────────────

    /// <summary>
    /// Rendert Multi-Model Ergebnisse: SAM-Masken (gruene Konturen) + Label-Badges mit Messungen.
    /// </summary>
    /// <summary>Aktuelles Multi-Model-Ergebnis fuer Klick-Interaktion.</summary>
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameResult? _currentMmResult;
    /// <summary>Ferne Detektionen (innerhalb Rohrkreis) — grau als Vorschau angezeigt.</summary>
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameResult? _previewMmResult;

    // Phase 6.1.F Sub-G: ShowMultiModelResults + AddMultiModelFindingsAsEvents + ShowCodingAiResults nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Aktuell selektierte Maske (Klick auf Overlay).</summary>
    private int _selectedMaskIndex = -1;

    /// <summary>
    /// Sperrliste: vom Benutzer abgelehnte Befunde (Code + Meter-Bereich).
    /// Verhindert dass die Auto-Analyse denselben Befund erneut einfuegt.
    /// Wird pro Session gefuehrt, Reset bei neuem Video.
    /// </summary>
    private readonly HashSet<string> _rejectedFindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Erzeugt einen Sperrlisten-Key: Code + gerundeter Meterstand (±0.5m Toleranz).</summary>
    // Phase 6.1.A: MakeRejectionKey nach PlayerWindow.Helpers.cs migriert.

    /// <summary>Maske angeklickt — selektieren, hervorheben, Befundliste synchronisieren.</summary>
    private void OnMaskOverlayClicked(int maskIndex)
    {
        // Wenn gleiche Maske nochmal geklickt → zur naechsten wechseln (Cycle)
        if (maskIndex == _selectedMaskIndex)
        {
            maskIndex = FindNextVisibleMaskIndex(maskIndex);
            if (maskIndex < 0) return;
        }

        _selectedMaskIndex = maskIndex;

        // Visuelle Hervorhebung: selektierte Maske dicker, andere normal
        HighlightSelectedMask(maskIndex);

        if (_currentMmResult?.QuantifiedMasks is { } masks && maskIndex < masks.Count)
        {
            var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(masks[maskIndex].Label);
            SetCodingAiState(
                $"Befund {maskIndex + 1}/{masks.Count}: {vsaCode ?? masks[maskIndex].Label}",
                Color.FromRgb(0x38, 0xBD, 0xF8),
                "Delete = verwerfen | O = OK | Leertaste = weiter");

            // Befundliste synchronisieren: passenden Eintrag selektieren
            SyncBefundListeToMask(maskIndex, vsaCode);
        }
    }

    /// <summary>Findet die naechste sichtbare Maske nach dem gegebenen Index (Cycle).</summary>
    private int FindNextVisibleMaskIndex(int afterIndex)
    {
        int total = _currentMmResult?.QuantifiedMasks.Count ?? 0;
        for (int offset = 1; offset < total; offset++)
        {
            int candidate = (afterIndex + offset) % total;
            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{candidate}";
            if (CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(e => tag.Equals(e.Tag as string)))
                return candidate;
        }
        return afterIndex; // Nur eine Maske uebrig
    }

    /// <summary>Selektierte Maske visuell hervorheben (dickere Kontur, Blink-Animation, andere gedimmt).</summary>
    private void HighlightSelectedMask(int selectedIndex)
    {
        var selectedTag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{selectedIndex}";

        foreach (var el in CodingOverlayCanvas.Children.OfType<System.Windows.Shapes.Path>())
        {
            if (el.Tag is not string tag || !tag.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag))
                continue;

            bool isSelected = tag == selectedTag;

            if (el.Stroke is not null)
            {
                // Kontur-Path
                el.StrokeThickness = isSelected ? 5 : 2;
                el.Opacity = isSelected ? 1.0 : 0.4;
            }
            else
            {
                // Fill-Path
                el.Opacity = isSelected ? 1.0 : 0.2;
            }

            // Blink-Animation auf selektierter Maske
            el.BeginAnimation(UIElement.OpacityProperty, null); // Alte Animation stoppen
            if (isSelected)
            {
                var blink = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 0.3,
                    Duration = TimeSpan.FromMilliseconds(300),
                    AutoReverse = true,
                    RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2)
                };
                el.BeginAnimation(UIElement.OpacityProperty, blink);
            }
        }
    }

    /// <summary>Synchronisiert die Befundliste (LstCodingEvents) mit der selektierten Maske — mit Flash-Animation.</summary>
    private void SyncBefundListeToMask(int maskIndex, string? vsaCode)
    {
        if (LstCodingEvents.Items.Count == 0 || string.IsNullOrEmpty(vsaCode)) return;

        // Suche den Event in der Liste der zum Maske-Code passt
        for (int i = LstCodingEvents.Items.Count - 1; i >= 0; i--)
        {
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;
            if (string.Equals(ev.Entry.Code, vsaCode, StringComparison.OrdinalIgnoreCase)
                || (ev.Entry.Code?.StartsWith(vsaCode, StringComparison.OrdinalIgnoreCase) == true))
            {
                _enlargeSuppressShrink = true;
                LstCodingEvents.SelectedIndex = i;
                LstCodingEvents.ScrollIntoView(LstCodingEvents.Items[i]);
                _enlargeSuppressShrink = false;

                // Ballon-Effekt: vergroessert bis abgehandelt oder anderes Event gewaehlt
                EnlargeListItem(i);
                return;
            }
        }
    }

    /// <summary>Aktuell vergroessertes ListBox-Item (bleibt gross bis abgehandelt oder anderes gewaehlt).</summary>
    private System.Windows.Controls.ListBoxItem? _enlargedListItem;
    /// <summary>Unterdrueckt Shrink in SelectionChanged wenn Maske die Selektion steuert.</summary>
    private bool _enlargeSuppressShrink;

    /// <summary>Vergroessert ein ListBox-Item persistent (Ballon-Effekt) + blauer Hintergrund.</summary>
    private void EnlargeListItem(int index)
    {
        // Vorheriges zuruecksetzen
        ShrinkEnlargedListItem();

        if (index < 0 || index >= LstCodingEvents.Items.Count) return;

        // Container holen — ggf. erst nach ScrollIntoView verfuegbar
        LstCodingEvents.UpdateLayout();
        var container = LstCodingEvents.ItemContainerGenerator
            .ContainerFromIndex(index) as System.Windows.Controls.ListBoxItem;
        if (container == null) return;

        _enlargedListItem = container;

        // Hintergrund blau — deutlich sichtbar
        container.Background = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
        container.FontWeight = System.Windows.FontWeights.Bold;

        // Vergroessern mit Animation: 1.0 → 1.18 (deutlich sichtbar)
        container.RenderTransformOrigin = new System.Windows.Point(0.0, 0.5); // Links verankert
        container.RenderTransform = new ScaleTransform(1.0, 1.0);
        var grow = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = 1.18,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        ((ScaleTransform)container.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ((ScaleTransform)container.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>Setzt das vergroesserte ListBox-Item auf Normalgroesse zurueck.</summary>

    /// <summary>Delete auf Maske (via Maus-Callback) — weiterleiten an zentrale Methode.</summary>
    private void OnMaskOverlayDeleted(int maskIndex)
    {
        DeleteMaskAtIndex(maskIndex);
    }

    /// <summary>
    /// Verwirft eine Maske (Delete-Taste). Identische Funktion wie Ablehnen in der Befundliste:
    /// Sperrliste, Event entfernen, SAM-Maske entfernen, Negativ-Feedback, ggf. Video weiter.
    /// </summary>
    private void DeleteMaskAtIndex(int maskIndex)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks || maskIndex >= masks.Count) return;

        var quant = masks[maskIndex];
        var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
        var meter = _codingVm?.CurrentMeter ?? 0;

        // Auf Sperrliste setzen → wird nicht mehr erneut eingefuegt
        _rejectedFindings.Add(MakeRejectionKey(vsaCode, meter));

        // Zugehoeriges CodingEvent entfernen (gleicher Pfad wie Ablehnen in Befundliste)
        RemoveMatchingCodingEvent(vsaCode, meter);
        if (_codingVm != null)
        {
            _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        }

        // SAM-Maske visuell entfernen
        Ai.Pipeline.SamMaskRenderer.RemoveMaskGroup(
            CodingOverlayCanvas, $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{maskIndex}");

        // Negativ-Feedback speichern
        Task.Run(() => SaveNegativeFeedbackAsync(quant.Label, vsaCode, meter))
            .SafeFireAndForget("NegativeFeedbackMask");

        _selectedMaskIndex = -1;
        RefreshCodingEventsList();

        // Wenn keine Masken mehr sichtbar → Video weiter
        if (!HasVisibleMasks())
            ResumeAfterPause();
    }

    /// <summary>Entfernt das CodingEvent das zum geloeschten Overlay gehoert.</summary>
    private void RemoveMatchingCodingEvent(string? vsaCode, double meter)
    {
        if (_codingVm == null || string.IsNullOrEmpty(vsaCode)) return;

        // Neueste Events zuerst (rueckwaerts suchen)
        for (int i = _codingVm.Events.Count - 1; i >= 0; i--)
        {
            var ev = _codingVm.Events[i];
            if (CodesMatchForDedup(ev.Entry.Code, vsaCode)
                && Math.Abs((ev.MeterAtCapture) - meter) < 1.0)
            {
                _codingVm.Events.RemoveAt(i);
                _codingSessionService?.ActiveSession?.Events.Remove(ev);
                System.Diagnostics.Debug.WriteLine(
                    $"[Sperrliste] CodingEvent entfernt: {ev.Entry.Code} @ {ev.MeterAtCapture:F1}m");
                break;
            }
        }
    }

    /// <summary>
    /// Akzeptiert eine Maske (O-Taste). Identische Funktion wie Akzeptieren in der Befundliste:
    /// Decision=Accepted, Sperrliste, SAM-Maske entfernen, Positiv-Feedback, ggf. Video weiter.
    /// </summary>
    private void AcceptMaskAtIndex(int maskIndex)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks || maskIndex >= masks.Count) return;

        var quant = masks[maskIndex];
        var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
        var meter = _codingVm?.CurrentMeter ?? 0;

        // Zugehoeriges CodingEvent finden und ueber ViewModel akzeptieren (gleicher Pfad wie Liste)
        if (_codingVm != null && !string.IsNullOrEmpty(vsaCode))
        {
            var matchingEvent = _codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, vsaCode)
                && Math.Abs(e.MeterAtCapture - meter) < 1.0);
            if (matchingEvent != null)
            {
                _codingVm.SelectedDefect = matchingEvent;
                _codingVm.AcceptDefectCommand.Execute(null);
            }
        }

        // Auf Sperrliste setzen → wird bei naechster Analyse nicht erneut erkannt
        _rejectedFindings.Add(MakeRejectionKey(vsaCode, meter));

        // SAM-Maske visuell entfernen
        Ai.Pipeline.SamMaskRenderer.RemoveMaskGroup(
            CodingOverlayCanvas, $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{maskIndex}");

        // Positiv-Feedback speichern
        Task.Run(() => SavePositiveFeedbackAsync(quant.Label, vsaCode, meter))
            .SafeFireAndForget("PositiveFeedbackMask");

        _selectedMaskIndex = -1;
        RefreshCodingEventsList();

        // Wenn keine Masken mehr sichtbar → Video weiter
        if (!HasVisibleMasks())
            ResumeAfterPause();
    }

    // Phase 6.1.F Sub-K: SAM-Mask-Helper + IsInsideDetectionZone + IsKunststoffRohr + HasNearbyStructuralDamage + ResumeAfterPause + GatherImportContext + IsAlreadyCovered + IsSamePosition + CodesMatchForDedup nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.B: Feedback-Loop (Cluster B4) nach PlayerWindow.Feedback.cs migriert.
    // Enthaelt: _feedbackHttpClient, _positiveFeedbackLock, _negativeFeedbackLock,
    // CreateFeedbackService, ResolveFeedbackCode, BuildFeedbackMappedEntry,
    // IngestFeedbackAsync, SavePositiveFeedbackAsync, SaveNegativeFeedbackAsync.

    /// <summary>
    /// Erstellt CodingEvents aus Multi-Model Befunden (DINO-Detections + SAM-Quantifizierung).
    /// </summary>
    /// <summary>
    /// Multi-Model Findings als CodingEvents — nutzt denselben Resolver-
    /// und Label-Pfad wie der Qwen/Enhanced-Pfad (ResolveFindingCodeForCoding, LookupVsaLabel).
    /// </summary>


    /// <summary>
    /// Filtert KI-Findings: VSA-Code-Validierung, BCD/BCE-Ausschluss, Deduplizierung.
    /// Die gefilterte Liste wird fuer UI, Overlays und Event-Erstellung verwendet.
    /// Deduplizierung: code + BBox-Mittelpunkt (verschiedene Positionen = verschiedene Befunde).
    /// </summary>
    // Phase 6.1.F Sub-I: FilterValidFindings + ResolveFindingCodeForCoding + RefineGenericCodeFromImport + TryResolveImportFallbackCode + AddAiFindingsAsEvents nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>
    /// Erlaubte Code-Familien fuer Import-Fallback.
    /// Umfasst Bestandsaufnahme (BC), Strukturschaeden (BA) und Betriebliche Stoerungen (BB).
    /// </summary>
    // Phase 6.1.A: IsAllowedImportFallbackCode nach PlayerWindow.Helpers.cs migriert.


    private void CodingPauseMode_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingPauseMode.IsChecked == true)
        {
            // Pausenmodus aktivieren — setzt auch Auto-Analyse an falls nicht schon aktiv
            if (BtnCodingLiveAi.IsChecked != true)
            {
                BtnCodingLiveAi.IsChecked = true;
                CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
            }
            BtnCodingPauseMode.Background = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
            SetCodingAiState("KI-Analyse mit Pause aktiv", Color.FromRgb(0x38, 0xBD, 0xF8),
                "Video pausiert bei jedem Befund — Delete = loeschen, Leertaste = weiter");
        }
        else
        {
            BtnCodingPauseMode.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            SetCodingAiState("Pausenmodus deaktiviert", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }

    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingLiveAi.IsChecked == true)
        {
            // 8s Intervall: Qwen braucht ~3s Inferenz + 1s Capture + Puffer
            _codingLiveAiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _codingLiveAiTimer.Tick += CodingLiveAiTimer_Tick;
            _codingLiveAiTimer.Start();

            // Gruen blinken wenn aktiv
            _codingLiveAiBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _codingLiveAiBlinkTimer.Tick += (_, _) =>
            {
                _codingLiveAiBlinkState = !_codingLiveAiBlinkState;
                BtnCodingLiveAi.Background = new SolidColorBrush(
                    _codingLiveAiBlinkState
                        ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
                        : Color.FromRgb(0x16, 0x65, 0x34));  // Dunkelgruen
            };
            _codingLiveAiBlinkTimer.Start();
            BtnCodingLiveAi.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {CompactModelName(_codingAiModelName)}");
        }
        else
        {
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;

            // Blinken stoppen, Standardfarbe zuruecksetzen
            _codingLiveAiBlinkTimer?.Stop();
            _codingLiveAiBlinkTimer = null;
            BtnCodingLiveAi.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

            SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }

    private async void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
    {
        // 2026-04-26: Window-Lifecycle-Guard. async-void darf bei IsClosed
        // nicht weiterlaufen — sonst greift RunCodingAnalysisAsync auf
        // disposed _player/_codingVisionClient zu (App-Crash).
        if (_isWindowClosed) return;
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            // Mindestens ein Analyse-Service muss verfuegbar sein
            if (_codingEnhancedVision == null && _codingLiveDetection == null) return;
            if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput) return;

            // Nur analysieren wenn Video tatsaechlich laeuft
            if (_player == null || !_player.IsPlaying) return;

            await RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {ex.Message}");
        }
    }

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














