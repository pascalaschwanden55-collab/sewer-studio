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
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Shared;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

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
    private const float MinRate = 0.25f;
    private const float MaxRate = 8.0f;

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
        time = default;
        try
        {
            var ms = Math.Max(0, _player.Time);
            time = TimeSpan.FromMilliseconds(ms);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySeekToInternal(TimeSpan time)
    {
        try
        {
            EnsurePlaying();
            var ms = (long)Math.Max(0, time.TotalMilliseconds);
            if (_player.Length > 0 && ms > _player.Length)
                ms = _player.Length;
            _player.Time = ms;
            UpdateUi();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static LibVLC CreateLibVlc(PlayerWindowOptions options)
    {
        var args = new List<string>();

        if (!string.Equals(options.VideoOutput, "any", StringComparison.OrdinalIgnoreCase))
            args.Add($"--vout={options.VideoOutput}");

        args.Add(options.EnableHardwareDecoding ? "--avcodec-hw=dxva2" : "--avcodec-hw=none");
        args.Add($"--avcodec-threads={options.CodecThreads}");
        args.Add($"--file-caching={options.FileCachingMs}");
        args.Add($"--network-caching={options.NetworkCachingMs}");

        if (options.DropLateFrames)
            args.Add("--drop-late-frames");
        if (options.SkipFrames)
            args.Add("--skip-frames");

        args.Add("--clock-jitter=0");
        args.Add("--clock-synchro=0");

        // Snapshot-Pfad nicht als OSD anzeigen (stoert bei KI-Frame-Captures)
        args.Add("--no-snapshot-preview");

        try
        {
            return new LibVLC(args.ToArray());
        }
        catch
        {
            return new LibVLC();
        }
    }

    // Audit R-C3 2026-04-25: Doppel-ESC innerhalb 1.5s als Notbremse fuer
    // den Codier-Modus, falls Overlay-Canvas in unerwarteten Zustand klemmt.
    private DateTime _lastEscapePress = DateTime.MinValue;

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // ESC im Trainings-Modus = Notausstieg. Ohne diese Zeile fing das Popup-
        // Fadenkreuz alle Mausklicks ab und der Toggle-Button war nicht mehr
        // erreichbar (UI-Trap).
        if (e.Key == Key.Escape && _isTrainingMode)
        {
            ExitTrainingMode();
            if (TrainingModeButton != null)
                TrainingModeButton.IsChecked = false;
            e.Handled = true;
            return;
        }

        // Doppel-ESC innerhalb 1.5s: harter Codier-Modus-Notausstieg
        // (R-C3). Tritt zur Wirkung wenn der normale CancelDraw nicht mehr
        // klappt weil Overlay-Service korrupt ist.
        if (e.Key == Key.Escape && _isCodingMode)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEscapePress).TotalSeconds < 1.5)
            {
                try
                {
                    ExitCodingMode();
                    System.Diagnostics.Debug.WriteLine("[PlayerWindow] Doppel-ESC: CodingMode hart beendet.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayerWindow] Doppel-ESC ExitCodingMode: {ex.Message}");
                }
                _lastEscapePress = DateTime.MinValue;
                e.Handled = true;
                return;
            }
            _lastEscapePress = now;
            // weiterreichen an normalen Cancel-Pfad unten
        }

        if (e.Key == Key.Escape && _codingOverlayService != null)
        {
            _codingOverlayService.CancelDraw();
            _codingSchemaManager.Cancel();
            if (CodingOverlayCanvas.IsMouseCaptured)
                CodingOverlayCanvas.ReleaseMouseCapture();
            if (_codingVm != null)
            {
                _codingVm.CurrentOverlay = null;
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }
            if (CodingOverlayPopup.IsOpen)
                RedrawCodingCanvas(includeManualOverlay: false);
            e.Handled = true;
            return;
        }

        // ── Pausenmodus-Tasten: Delete/O nur wenn pausiert + Masken sichtbar ──
        if (BtnCodingPauseMode?.IsChecked == true && _player is { IsPlaying: false } && HasVisibleMasks())
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                // Delete: selektierte Maske verwerfen, oder erste sichtbare
                var idx = _selectedMaskIndex >= 0 ? _selectedMaskIndex : FindFirstVisibleMaskIndex();
                if (idx >= 0) DeleteMaskAtIndex(idx);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                // O: selektierte Maske akzeptieren, oder erste sichtbare
                var idx = _selectedMaskIndex >= 0 ? _selectedMaskIndex : FindFirstVisibleMaskIndex();
                if (idx >= 0) AcceptMaskAtIndex(idx);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                // Tab: zur naechsten Maske wechseln
                var current = _selectedMaskIndex >= 0 ? _selectedMaskIndex : -1;
                var next = FindNextVisibleMaskIndex(current);
                if (next >= 0) OnMaskOverlayClicked(next);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Space)
        {
            TogglePlayPause();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S)
        {
            _player.Stop();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.P)
        {
            _player.SetPause(true);
            _codingAnalysisCts?.Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            EnsurePlaying();
            _player.SetPause(false);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Add || e.Key == Key.OemPlus)
        {
            ChangeSpeed(+0.25f);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
        {
            ChangeSpeed(-0.25f);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right)
        {
            JumpSeconds(5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            JumpSeconds(-5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D)
        {
            if (_isCodingMode)
            {
                BtnCodingLiveAi.IsChecked = !(BtnCodingLiveAi.IsChecked == true);
                CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
            }
            else
            {
                LiveDetectionButton.IsChecked = !(LiveDetectionButton.IsChecked == true);
                LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs());
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M)
        {
            // Toggle: Wenn Mark-Tool aktiv → deaktivieren, sonst Popup oeffnen
            if (_markToolType != OverlayToolType.None)
                DeactivateMarkTool();
            else
                MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
            e.Handled = true;
        }
    }

    private void TogglePlayPause()
    {
        EnsurePlaying();
        var willPause = _player.IsPlaying;
        _player.SetPause(willPause);

        // Laufende KI-Analyse abbrechen wenn pausiert wird
        if (willPause)
            _codingAnalysisCts?.Cancel();
    }

    // Phase 6.1.A: FormatMs nach PlayerWindow.Helpers.cs migriert.
    // Phase 6.1.D Sub-B/C/D: EnsurePlaying, ChangeSpeed, JumpSeconds, Play(string),
    //   OnClosing, Cleanup, UpdateUi nach PlayerWindow.VideoPlayback.cs migriert.

    // Phase 6.1.D Sub-A: Play/Pause/Stop/Speed-Click + Slider-Seek + Rate-Label
    // (12 Methoden) nach PlayerWindow.VideoPlayback.cs migriert.

    // Ã¢"â‚¬Ã¢"â‚¬ Damage marker overlay Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    private void BuildDamageMarkers()
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        DamageMarkerCanvas.Children.Clear();
        _damageMarkers.Clear();

        var accentBrush = (System.Windows.Media.Brush)FindResource("NeonCyanBrush");
        var accentColor = (System.Windows.Media.Color)FindResource("ColorNeonCyan");

        foreach (var info in _damageOverlay.Markers)
        {
            if (info.MeterStart < 0 || info.MeterStart > _damageOverlay.PipeLengthMeters)
                continue;

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
                CreateRangeMarker(info, accentBrush, accentColor);
            else
                CreatePointMarker(info, accentBrush, accentColor);
        }

        RepositionDamageMarkers();
    }

    private void CreatePointMarker(DamageMarkerInfo info, System.Windows.Media.Brush accentBrush, System.Windows.Media.Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var tick = new Rectangle
        {
            Width = 2,
            Height = 14,
            Fill = accentBrush,
            Opacity = 0.85,
            IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = accentColor, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5 }
        };
        Canvas.SetTop(tick, -5);
        container.Children.Add(tick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        container.ToolTip = $"{info.Code} @ {info.MeterStart:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        DamageMarkerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, tick, label));
    }

    private void CreateRangeMarker(DamageMarkerInfo info, System.Windows.Media.Brush accentBrush, System.Windows.Media.Color accentColor)
    {
        var container = new Canvas { Cursor = Cursors.Hand };

        var bar = new Rectangle
        {
            Height = 5,
            Fill = accentBrush,
            Opacity = 0.35,
            RadiusX = 2,
            RadiusY = 2,
            IsHitTestVisible = false
        };
        Canvas.SetTop(bar, -2);
        container.Children.Add(bar);

        var startTick = new Rectangle
        {
            Width = 1.5,
            Height = 10,
            Fill = accentBrush,
            Opacity = 0.7,
            IsHitTestVisible = false
        };
        Canvas.SetTop(startTick, -4);
        container.Children.Add(startTick);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(info.Code) ? "?" : info.Code.Trim(),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            Foreground = accentBrush,
            IsHitTestVisible = false
        };
        Canvas.SetTop(label, -19);
        container.Children.Add(label);

        var endM = Math.Min(info.MeterEnd ?? info.MeterStart, _damageOverlay!.PipeLengthMeters);
        container.ToolTip = $"{info.Code} Strecke {info.MeterStart:0.0}m - {endM:0.0}m"
            + (string.IsNullOrWhiteSpace(info.Description) ? "" : $"\n{info.Description}");

        container.MouseLeftButtonDown += (_, _) => SeekToMeter(info.MeterStart);

        DamageMarkerCanvas.Children.Add(container);
        _damageMarkers.Add((info, container, bar, label));
    }

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

    private void RepositionDamageMarkers()
    {
        if (_damageOverlay is null || _damageMarkers.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        var pipeLength = _damageOverlay.PipeLengthMeters;

        foreach (var (info, container, tickOrRange, label) in _damageMarkers)
        {
            var ratio = Math.Clamp(info.MeterStart / pipeLength, 0.0, 1.0);
            var x = offsetX + ratio * trackWidth;

            if (info.IsStreckenschaden && info.MeterEnd.HasValue && info.MeterEnd.Value > info.MeterStart)
            {
                Canvas.SetLeft(container, x);
                var endRatio = Math.Clamp(Math.Min(info.MeterEnd.Value, pipeLength) / pipeLength, 0.0, 1.0);
                var endX = offsetX + endRatio * trackWidth;
                var barWidth = Math.Max(endX - x, 3);
                ((Rectangle)tickOrRange).Width = barWidth;

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, (barWidth - labelWidth) / 2);
            }
            else
            {
                Canvas.SetLeft(container, x - 1);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelWidth = label.DesiredSize.Width;
                Canvas.SetLeft(label, -(labelWidth / 2) + 1);
            }
        }
    }

    private void SeekToMeter(double meter)
    {
        if (_damageOverlay is null || _damageOverlay.PipeLengthMeters <= 0)
            return;

        EnsurePlaying();
        // Pause so the jumped-to frame is clearly visible
        _player.SetPause(true);

        var ratio = Math.Clamp(meter / _damageOverlay.PipeLengthMeters, 0.0, 1.0);
        PositionSlider.Value = ratio * PositionSlider.Maximum;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(ratio * length);
        else
            _player.Position = (float)ratio;

        UpdateUi();
    }

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
            cfg = AiRuntimeConfig.Load();
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

    private void AddHeatmapSegment(QuickScanSegment segment, double videoDurationSec)
    {
        if (videoDurationSec <= 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        double segWidth = (5.0 / videoDurationSec) * trackWidth;
        if (segWidth < 2) segWidth = 2;

        double ratio = Math.Clamp(segment.TimestampSeconds / videoDurationSec, 0.0, 1.0);
        double x = offsetX + ratio * trackWidth;

        var rect = new Rectangle
        {
            Width = segWidth,
            Height = 6,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(SeverityToColor(segment.Severity, segment.HasDamage)),
            Cursor = Cursors.Hand,
            Opacity = segment.HasDamage ? 0.85 : 0.4
        };

        var tip = segment.HasDamage
            ? $"Befund: {segment.Label ?? "?"} (Schwere {segment.Severity})"
              + (segment.Clock != null ? $"\nUhr: {segment.Clock}" : "")
              + $"\n@ {segment.TimestampSeconds:0.0}s"
            : $"Kein Befund @ {segment.TimestampSeconds:0.0}s";
        rect.ToolTip = tip;

        var timestampSec = segment.TimestampSeconds;
        rect.MouseLeftButtonDown += (_, _) =>
        {
            EnsurePlaying();
            _player.SetPause(true);
            var length = _player.Length;
            if (length > 0)
            {
                var targetMs = (long)(timestampSec * 1000);
                if (targetMs > length) targetMs = length;
                _player.Time = targetMs;
            }
            UpdateUi();
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, 0);

        HeatmapCanvas.Children.Add(rect);
        _heatmapRects.Add((segment, rect));
    }

    private void RepositionHeatmap()
    {
        if (_heatmapRects.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        // Infer video duration from the last segment timestamp + step
        double videoDuration = 0;
        foreach (var (seg, _) in _heatmapRects)
        {
            if (seg.TimestampSeconds + 5.0 > videoDuration)
                videoDuration = seg.TimestampSeconds + 5.0;
        }
        if (videoDuration <= 0)
            return;

        foreach (var (seg, rect) in _heatmapRects)
        {
            double ratio = Math.Clamp(seg.TimestampSeconds / videoDuration, 0.0, 1.0);
            double x = offsetX + ratio * trackWidth;
            double w = (5.0 / videoDuration) * trackWidth;
            if (w < 2) w = 2;

            Canvas.SetLeft(rect, x);
            rect.Width = w;
        }
    }

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

    // ── Markieren Popup-Menü ──────────────────────────────────────────────

    private OverlayToolType _markToolType = OverlayToolType.None;

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        if (_isCodingMode)
            ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
        else
            MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
    {
        ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
    }

    private void MarkTool_Punkt_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Point, "Punkt");

    private void MarkTool_Ellipse_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Ellipse, "Ellipse");

    private void MarkTool_Freihand_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Freehand, "Freihand");

    private void MarkTool_Rechteck_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Rechteck");

    private void ActivateMarkTool(OverlayToolType tool, string label)
    {
        MarkToolPopup.IsOpen = false;
        CodingMarkToolPopup.IsOpen = false;
        ToolsDropdownPopup.IsOpen = false;
        _markToolType = tool;
        TxtMarkToolName.Text = label;
        TxtActiveToolLabel.Text = label;
        _player.SetPause(true);
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (tool == OverlayToolType.Point)
        {
            // Bestehende Punkt-Logik: DetectionCanvas aktivieren
            _isManualMarkMode = true;
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            DetectionOverlayGrid.IsHitTestVisible = true;
            DetectionCanvas.IsHitTestVisible = true;
            DetectionCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            // Zeichen-Tools: CodingOverlayPopup aktivieren
            _isManualMarkMode = false;
            EnsureMarkOverlayReady();
            _codingOverlayService!.ActiveTool = tool;

            // Offene Zeichnung verwerfen
            if (_codingVm != null)
                _codingVm.CurrentOverlay = null;

            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = Cursors.Cross;
        }
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new Ai.OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= new Ai.CodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(_codingSessionService, _codingOverlayService);
        }
    }

    private void DeactivateMarkTool()
    {
        _markToolType = OverlayToolType.None;
        _isManualMarkMode = false;
        TxtMarkToolName.Text = "Markieren";

        DetectionCanvas.Cursor = Cursors.Arrow;
        DetectionCanvas.IsHitTestVisible = false;
        if (!_isDetecting)
        {
            DetectionOverlayGrid.IsHitTestVisible = false;
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }

        if (!_isCodingMode)
        {
            _codingSchemaManager.Cancel();
            _codingOverlayService?.CancelDraw();
            if (_codingOverlayService != null)
                _codingOverlayService.ActiveTool = OverlayToolType.None;
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.IsHitTestVisible = false;
        }
    }

    /// <summary>
    /// Nach abgeschlossener Markierung (Ellipse/Freihand/Rechteck): Code-Katalog oeffnen + Training speichern.
    /// </summary>
    private async void HandleMarkDrawingComplete()
    {
        // Window-Lifecycle-Guard fuer async-void-Methode
        if (_isWindowClosed) return;
        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null) return;

            var timestampSec = _player.Time / 1000.0;

            // Uhrzeiger-Position aus Overlay-Zentrum berechnen
            string? clockPos = null;
            double avgX = 0.5, avgY = 0.5;
            if (overlay.Points.Count > 0)
            {
                avgX = overlay.Points.Average(p => p.X);
                avgY = overlay.Points.Average(p => p.Y);
                var cx = 0.5; var cy = 0.5; // Rohrmitte (normalisiert)
                var dx = avgX - cx;
                var dy = avgY - cy;
                var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var clockAngle = (angleDeg + 90 + 360) % 360;
                var hour = (int)Math.Round(clockAngle / 30.0) % 12;
                if (hour == 0) hour = 12;
                clockPos = hour.ToString();
            }

            // SAM-Segmentierung an der markierten Stelle anzeigen
            await ShowSamPreviewAtMarkAsync(overlay, avgX, avgY);

            // Der VSA-Picker ist modal und liegt ueber dem CodingOverlayPopup.
            // Deshalb kurz warten, damit die erfolgreich gerenderte SAM-Maske sichtbar ist,
            // bevor der Picker automatisch aufgeht.
            if (!_isWindowClosed && overlay.ToolType == OverlayToolType.Rectangle && CodingOverlayPopup.IsOpen)
                await Task.Delay(TimeSpan.FromMilliseconds(1200));

            // Training speichern: Frame + YOLO-Export + TeacherAnnotation + CodingEvent
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos);

            // Overlay entfernen und Canvas neu zeichnen.
            // WICHTIG: preserveSamMasks=true — sonst werden die gerade von
            // ShowSamPreviewAtMarkAsync gerenderten Masken durch
            // ClearTransientCodingCanvas sofort wieder geloescht.
            if (_codingVm != null) _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false, preserveSamMasks: true);

            if (saved)
            {
                // Erfolgreich gespeichert → Tool deaktivieren
                DeactivateMarkTool();
            }
            else
            {
                // Abgebrochen → Tool bleibt aktiv, naechste Markierung kann sofort gezeichnet werden
                if (_codingOverlayService != null)
                    _codingOverlayService.ActiveTool = _markToolType;
                CodingOverlayCanvas.Cursor = Cursors.Cross;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    /// <summary>
    /// Zeigt eine SAM-Segmentierung als Vorschau an der markierten Stelle.
    /// Der User sieht sofort die Konturen des Objekts das die KI dort erkennt.
    /// </summary>
    private async Task ShowSamPreviewAtMarkAsync(OverlayGeometry overlay, double normX, double normY)
    {
        if (_isWindowClosed) return;
        if (_codingVisionClient == null)
        {
            System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: _codingVisionClient ist null (Sidecar nicht initialisiert)");
            SetCodingAiState("SAM: Sidecar-Client nicht initialisiert", Color.FromRgb(0xEF, 0x44, 0x44));
            return;
        }

        try
        {
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

            if (overlay.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: BBox-Punkte fehlen");
                SetCodingAiState("SAM: BBox-Punkte fehlen", Color.FromRgb(0xEF, 0x44, 0x44));
                return;
            }

            // Snapshot fuer SAM
            var pngBytes = await CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: Snapshot leer/null");
                SetCodingAiState("SAM: Frame-Capture leer (Video pausiert?)", Color.FromRgb(0xEF, 0x44, 0x44));
                return;
            }

            var b64 = Convert.ToBase64String(pngBytes);

            // Bild-Aufloesung dynamisch aus dem Snapshot lesen (vorher hartkodiert
            // 640x480 -> falsche Pixel-Koordinaten bei 1920x1080-Frames -> SAM
            // bekam BBox an falscher Stelle und lieferte 0 Masken).
            int imgW = 1920, imgH = 1080; // Sicherer Default fuer typische Inspektionsvideos
            try
            {
                using var ms = new System.IO.MemoryStream(pngBytes);
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    ms,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    imgW = decoder.Frames[0].PixelWidth;
                    imgH = decoder.Frames[0].PixelHeight;
                }
            }
            catch (Exception decEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] Bild-Decode fehlgeschlagen, nutze Default {imgW}x{imgH}: {decEx.Message}");
            }

            // BBox-Berechnung analog zum Trainingsmodus (PlayerWindow.TrainingMode.cs:289-294):
            // overlay.Points sind normiert zum CodingOverlayCanvas. Wenn das Canvas-
            // Aspect-Ratio nicht zum Frame-Aspect passt (Letterbox-Bars wegen
            // Window-Resizing), wuerde die direkte Normalisierung overlay.Points * imgW
            // die BBox verschieben. Stattdessen ueber Canvas-Pixel + sx/sy-Skalierung,
            // das macht der Trainingsmodus auch und es funktioniert dort zuverlaessig.
            var (cw, ch) = GetCodingOverlayRenderSize();
            double sx = imgW / cw;
            double sy = imgH / ch;

            double minNormX = overlay.Points.Min(p => p.X);
            double minNormY = overlay.Points.Min(p => p.Y);
            double maxNormX = overlay.Points.Max(p => p.X);
            double maxNormY = overlay.Points.Max(p => p.Y);

            // Normiert -> Canvas-Pixel -> Image-Pixel
            double minX = (minNormX * cw) * sx;
            double minY = (minNormY * ch) * sy;
            double maxX = (maxNormX * cw) * sx;
            double maxY = (maxNormY * ch) * sy;

            // Sanity-Check: BBox muss > 0 Pixel haben
            if ((maxX - minX) < 4 || (maxY - minY) < 4)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] BBox zu klein: {maxX - minX:F0}x{maxY - minY:F0} px");
                SetCodingAiState($"SAM: BBox zu klein ({maxX - minX:F0}x{maxY - minY:F0} px)", Color.FromRgb(0xF5, 0x9E, 0x0B));
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SAM] Anfrage: img={imgW}x{imgH}, canvas={cw:F0}x{ch:F0}, " +
                $"bbox=({minX:F0},{minY:F0})-({maxX:F0},{maxY:F0}) " +
                $"({maxX-minX:F0}x{maxY-minY:F0}px), b64={b64.Length} bytes");

            // Nur BBox als Prompt — kein Punkt-Prompt, damit SAM innerhalb der Box bleibt
            var boxes = new[] { new Ai.Pipeline.SamBoundingBox(minX, minY, maxX, maxY, "mark", 1.0) };

            int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
            var samReq = new Ai.Pipeline.SamRequest(b64, boxes, PipeDiameterMm: dn);

            Ai.Pipeline.SamResponse? samResp;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                SetCodingAiState("SAM: laeuft...", Color.FromRgb(0xF5, 0x9E, 0x0B), pulse: true);
                samResp = await _codingVisionClient.SegmentSamAsync(samReq);
            }
            catch (Exception apiEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] API-Fehler: {apiEx.Message}");
                SetCodingAiState($"SAM-Fehler: {TrimStatus(apiEx.Message)}", Color.FromRgb(0xEF, 0x44, 0x44));
                return;
            }
            finally
            {
                sw.Stop();
            }

            if (samResp == null)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Antwort null (Sidecar nicht erreichbar oder 401/500)");
                SetCodingAiState("SAM-Fehler: Sidecar antwortet nicht", Color.FromRgb(0xEF, 0x44, 0x44));
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SAM] Antwort: {samResp.Masks.Count} Masken, img={samResp.ImageWidth}x{samResp.ImageHeight}, t={samResp.InferenceTimeMs}ms");

            if (samResp.Masks.Count == 0)
            {
                SetCodingAiState("SAM: Keine Maske gefunden (leeres Ergebnis vom Sidecar)", Color.FromRgb(0xF5, 0x9E, 0x0B));
                return;
            }

            // Alte Masken entfernen, SAM-Vorschau rendern (Cyan = manuell markiert)
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            var (renderW, renderH) = GetCodingOverlayRenderSize();
            RenderSamPromptBox(minNormX, minNormY, maxNormX, maxNormY, renderW, renderH);

            // Quantifizierung fuer Label-Anzeige
            var quantified = new List<Ai.Pipeline.MaskQuantificationService.QuantifiedMask>();
            var cal = _codingOverlayService?.Calibration;
            foreach (var mask in samResp.Masks)
            {
                var q = cal != null
                    ? Ai.Pipeline.MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, dn, cal)
                    : Ai.Pipeline.MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, dn);
                quantified.Add(q);
            }

            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                samResp,
                quantified,
                renderW,
                renderH);

            var visibleSamElements = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Count(e => (e.Tag as string)?.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag, StringComparison.Ordinal) == true);

            SetCodingAiState($"SAM-Maske: {samResp.Masks.Count} Region(en) in {sw.ElapsedMilliseconds} ms",
                Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Overlay-Elemente: {visibleSamElements}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SAM] Unerwarteter Fehler: {ex}");
            SetCodingAiState($"SAM-Fehler: {TrimStatus(ex.Message)}", Color.FromRgb(0xEF, 0x44, 0x44));
        }
    }

    private (double Width, double Height) GetCodingOverlayRenderSize()
    {
        UpdateCodingOverlayViewport();

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (double.IsNaN(w) || w <= 1) w = CodingOverlayCanvas.Width;
        if (double.IsNaN(h) || h <= 1) h = CodingOverlayCanvas.Height;
        if (double.IsNaN(w) || w <= 1) w = VideoView.ActualWidth;
        if (double.IsNaN(h) || h <= 1) h = VideoView.ActualHeight;

        return (Math.Max(1, w), Math.Max(1, h));
    }

    private void RenderSamPromptBox(double minNormX, double minNormY, double maxNormX, double maxNormY, double canvasW, double canvasH)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(1, (maxNormX - minNormX) * canvasW),
            Height = Math.Max(1, (maxNormY - minNormY) * canvasH),
            Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 4 },
            Fill = new SolidColorBrush(Color.FromArgb(20, 0x38, 0xBD, 0xF8)),
            IsHitTestVisible = false,
            Tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_prompt"
        };

        Canvas.SetLeft(rect, minNormX * canvasW);
        Canvas.SetTop(rect, minNormY * canvasH);
        CodingOverlayCanvas.Children.Add(rect);
    }

    // Phase 6.1.A: TrimStatus nach PlayerWindow.Helpers.cs migriert.

    /// <summary>
    /// Speichert eine Markierung als Teacher-Annotation (YOLO-Export + TeacherAnnotationStore).
    /// Vereinfachte Version von CodingModeWindow.BtnSaveAsTraining_Click.
    /// </summary>
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition)
    {
        try
        {
            // 1. VSA-Code waehlen — VsaCodeExplorer oeffnet sich sofort
            // Meter automatisch aus OSD oder Videoposition berechnen
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = new ProtocolEntry();
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(entry, autoMeter, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };
            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
                return false;

            var selectedEntry = explorer.SelectedEntry;

            // 2. Frame-Capture
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes == null) return false;

            // 3. BoundingBox aus Overlay-Punkten
            var bbox = Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y)).ToList());

            // Mindestgroesse pruefen (1% des Frames)
            if (bbox.Width < 0.01 || bbox.Height < 0.01) return false;

            // 4. YOLO-Export
            int classId = Ai.Teacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            // Frame in Temp speichern
            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_mark_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Temp aufräumen
            try { System.IO.File.Delete(tempFrame); } catch { }

            // 5. TeacherAnnotation erstellen + persistieren
            var captureMeter = 0.0;
            if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedMeter))
                captureMeter = parsedMeter;

            var annotation = new Ai.Teacher.TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = captureMeter,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                ToolType = overlay.ToolType,
                Points = new List<Domain.Models.NormalizedPoint>(
                    overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y))),
                BoundingBox = bbox,
                ClockPosition = clockPosition != null && double.TryParse(clockPosition, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = overlay.Q2Mm,
                HeightMm = overlay.Q1Mm
            };

            await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Markierung AUCH als CodingEvent in die KI-Befunde-Liste eintragen
            if (_codingSessionService != null && _codingVm != null)
            {
                var codingEntry = new ProtocolEntry
                {
                    Source = ProtocolEntrySource.Manual,
                    Code = selectedEntry.Code,
                    Beschreibung = selectedEntry.Beschreibung,
                    MeterStart = selectedEntry.MeterStart ?? captureMeter,
                    MeterEnd = selectedEntry.MeterEnd,
                    Zeit = selectedEntry.Zeit ?? TimeSpan.FromSeconds(timestampSec),
                    IsStreckenschaden = selectedEntry.IsStreckenschaden,
                    CodeMeta = selectedEntry.CodeMeta
                };
                if (exportResult.FullFramePath != null)
                    codingEntry.FotoPaths.Add(exportResult.FullFramePath);

                _codingSessionService.AddEvent(codingEntry, overlay);
                RefreshCodingEventsList();
            }

            // Dezente Statusmeldung im OSD-Badge (kein MessageBox-Popup)
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ {selectedEntry.Code} gespeichert";

            // Badge nach 3 Sekunden zuruecksetzen
            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                if (_codingLastOsdMeter.HasValue)
                    TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
                else
                    OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
            return true;
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"\u2717 Fehler: {ex.Message}";
            return false;
        }
    }

    // ── LiveDetection Bestaetigungs-Logik ──────────────────────────
    private void ShowDetectionConfirmation(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0) return;

        // Video pausieren und zur Fundstelle springen
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        // Zur Fundstelle springen (Timestamp aus dem analysierten Frame)
        if (_detectionPendingTimestampSec.HasValue && _player != null)
        {
            long targetMs = (long)(_detectionPendingTimestampSec.Value * 1000);
            _player.Time = targetMs;
        }

        // Zusammenfassung der Befunde
        var primary = findings[0];
        var severityText = primary.Severity switch
        {
            5 => "S5 kritisch",
            4 => "S4 schwer",
            3 => "S3 mittel",
            2 => "S2 leicht",
            _ => $"S{primary.Severity}"
        };

        TxtDetectionFinding.Text = findings.Count == 1
            ? $"KI-Erkennung: {primary.Label} ({severityText})"
            : $"KI-Erkennung: {findings.Count} Befunde — {primary.Label} ({severityText})";

        var details = new System.Text.StringBuilder();
        foreach (var f in findings)
        {
            if (details.Length > 0) details.Append("  |  ");
            details.Append($"{f.PositionClock ?? "?"} Uhr · {f.Label}");
            if (f.ExtentPercent.HasValue) details.Append($" · {f.ExtentPercent}%");
        }
        TxtDetectionDetail.Text = details.ToString();

        DetectionConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ResumeDetection()
    {
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;

        // Video automatisch weiterlaufen lassen nach Entscheidung
        if (_player != null && !_player.IsPlaying)
            _player.Play();
    }

    private async void DetectionAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var timestampSec = _detectionPendingTimestampSec ?? (_player.Time / 1000.0);
            var exportService = new Ai.Teacher.TrainingAnnotationExportService();

            foreach (var finding in _detectionPendingFindings)
            {
                var code = finding.VsaCodeHint ?? finding.Label;
                int classId = Ai.Teacher.VsaYoloClassMap.GetClassId(code);
                var annotationId = Guid.NewGuid().ToString("N")[..12];
                var baseName = $"det_{annotationId}";

                // Bounding-Box aus Uhrposition ableiten (Ring-Sektor → normalisierte Koordinaten)
                var bbox = BBoxFromClockPosition(finding);

                // Frame temp speichern
                var tempFrame = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
                await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

                var exportResult = await exportService.ExportAsync(tempFrame, bbox, code, classId, baseName);
                try { System.IO.File.Delete(tempFrame); } catch { }

                // TeacherAnnotation erstellen
                var annotation = new Ai.Teacher.TeacherAnnotation
                {
                    AnnotationId = annotationId,
                    VsaCode = code,
                    Beschreibung = finding.Label,
                    MeterPosition = 0,
                    VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                    ToolType = OverlayToolType.None,
                    Points = new List<Domain.Models.NormalizedPoint>(),
                    BoundingBox = bbox,
                    ClockPosition = double.TryParse(finding.PositionClock, out var cp) ? cp : null,
                    FullFramePath = exportResult.FullFramePath,
                    CroppedRegionPath = exportResult.CroppedRegionPath,
                    YoloAnnotationPath = exportResult.YoloAnnotationPath,
                    WidthMm = finding.WidthMm,
                    HeightMm = finding.HeightMm
                };
                await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);
            }

            // Dezente Bestaetigung im OSD-Badge
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ {_detectionPendingFindings.Count} Befund(e) gespeichert";

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                if (_codingLastOsdMeter.HasValue)
                    TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
                else
                    OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✗ Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private async void DetectionCorrect_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _player.Time / 1000.0;

            // VsaCodeExplorer oeffnen fuer Korrektur — Meter aus OSD/Video
            var autoMeter2 = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = new ProtocolEntry();
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(entry, autoMeter2, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };

            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
            {
                ResumeDetection();
                return;
            }

            var selectedEntry = explorer.SelectedEntry;

            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var primary = _detectionPendingFindings[0];
            var timestampSecForFrame = _detectionPendingTimestampSec ?? timestampSec;
            var bbox = BBoxFromClockPosition(primary);

            int classId = Ai.Teacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"det_corr_{annotationId}";

            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);
            try { System.IO.File.Delete(tempFrame); } catch { }

            var annotation = new Ai.Teacher.TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = 0,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSecForFrame),
                ToolType = OverlayToolType.None,
                Points = new List<Domain.Models.NormalizedPoint>(),
                BoundingBox = bbox,
                ClockPosition = double.TryParse(primary.PositionClock, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = primary.WidthMm,
                HeightMm = primary.HeightMm
            };
            await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ Training: {selectedEntry.Code} (korrigiert)";

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                if (_codingLastOsdMeter.HasValue)
                    TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
                else
                    OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✗ Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private void DetectionSkip_Click(object sender, RoutedEventArgs e)
    {
        ResumeDetection();
    }

    /// <summary>
    /// Erzeugt eine grobe BoundingBox aus Uhrposition + Ausdehnung eines LiveFrameFinding.
    /// Mapping: Uhrposition → Kreissektor → normalisierte Box im Bild.
    /// </summary>
    private static Application.Ai.NormalizedBoundingBox BBoxFromClockPosition(LiveFrameFinding finding)
    {
        // Uhrzeiger → Winkel (12 Uhr = oben = -90°, dann im Uhrzeigersinn)
        double clockHour = 6; // Default: unten
        if (double.TryParse(finding.PositionClock, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            clockHour = parsed;

        double angleDeg = (clockHour / 12.0) * 360.0 - 90.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        // Extent-basierte Groesse (% Umfang → Box-Groesse)
        double extent = (finding.ExtentPercent ?? 15) / 100.0;
        double boxSize = Math.Clamp(extent * 0.6, 0.08, 0.40);

        // Zentrum auf ~35% Radius vom Bildmittelpunkt
        double cx = 0.5 + 0.35 * Math.Cos(angleRad);
        double cy = 0.5 + 0.35 * Math.Sin(angleRad);

        return new Application.Ai.NormalizedBoundingBox
        {
            XCenter = Math.Clamp(cx, 0, 1),
            YCenter = Math.Clamp(cy, 0, 1),
            Width = Math.Clamp(boxSize, 0.08, 0.40),
            Height = Math.Clamp(boxSize, 0.08, 0.40)
        };
    }

    private void DetectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker nutzt CodingOverlayCanvas (nicht DetectionCanvas)

        if (!_isManualMarkMode)
            return;

        var clickPoint = e.GetPosition(DetectionCanvas);
        var canvasSize = new Size(DetectionCanvas.ActualWidth, DetectionCanvas.ActualHeight);

        if (canvasSize.Width < 60 || canvasSize.Height < 60)
            return;

        // Pause video
        _player.SetPause(true);

        var clockPosition = ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private static string ClickToClockPosition(Point click, Size canvasSize)
    {
        var cx = canvasSize.Width / 2.0;
        var cy = canvasSize.Height / 2.0;
        var dx = click.X - cx;
        var dy = click.Y - cy;

        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var clockAngle = (angleDeg + 90 + 360) % 360;
        var hour = (int)Math.Round(clockAngle / 30.0) % 12;
        if (hour == 0) hour = 12;

        return hour.ToString();
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        _player.SetPause(true);
        OpenCodeCatalogForMark(
            finding.PositionClock,
            timestampSec,
            finding.VsaCodeHint);
    }

    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        // Resolve ServiceProvider: prefer injected, fallback to App.Services
        var sp = _serviceProvider ?? (App.Services as ServiceProvider);

        if (sp?.CodeCatalog is null)
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
    private Ai.Pipeline.SingleFrameMultiModelService? _codingMultiModel;
    private Ai.Pipeline.VisionPipelineClient? _codingVisionClient;

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

    private void EnterCodingMode()
    {
        if (_isCodingMode || _haltungRecord == null) return;
        _isCodingMode = true;
        ResetFrameReadiness();

        // Video pausieren
        _player.SetPause(true);

        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
        }

        LiveDetectionButton.Visibility = Visibility.Collapsed;
        LiveDetectionStatusText.Visibility = Visibility.Collapsed;

        // Session-Services erstellen
        _codingSessionService = new CodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(_codingSessionService, _codingOverlayService);
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;

        // DN laden
        int nominalDn = 0;
        if (_haltungRecord.Fields.TryGetValue("DN_mm", out var dnStr)
            && int.TryParse(dnStr, out var dn) && dn > 0)
        {
            nominalDn = dn;
            _codingOverlayService.SetCalibration(new PipeCalibration { NominalDiameterMm = dn });
        }

        TxtCodingCalibDn.Text = nominalDn > 0 ? $"DN: {nominalDn} mm" : "DN: unbekannt";
        TxtCodingCalibStatus.Text = _codingOverlayService.IsCalibrated
            ? "Kalibriert" : "Nicht kalibriert";

        // Fallback: Haltungslaenge pruefen, ggf. manuell abfragen
        EnsureHaltungslaenge(_haltungRecord);

        // Session starten
        try
        {
            _codingVm.StartSessionCommand.Execute(_haltungRecord);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Warning);
            ExitCodingMode();
            return;
        }

        // Pruefen ob Session tatsaechlich gestartet wurde
        // (StartSessionCommand faengt Fehler intern ab, z.B. fehlende Haltungslaenge)
        if (_codingSessionService.ActiveSession == null)
        {
            ExitCodingMode();
            return;
        }

        // Session pausieren (Video steht still, Schritt-Navigation)
        _codingSessionService.PauseSession();

        TxtCodingRange.Text = $"/ {_codingVm.EndMeter:F2}m";
        TxtCodingMeter.Text = "0.00m";

        // ALLE bestehenden Beobachtungen in Import-Referenz verschieben.
        // KI-Befunde-Liste startet LEER — KI erkennt frisch, User korrigiert.
        _codingImportEvents.Clear();
        var allExisting = _codingVm.Events.OrderBy(e => e.MeterAtCapture).ToList();
        _codingVm.Events.Clear();
        foreach (var ev in allExisting)
            _codingImportEvents.Add(ev);
        LstImportEvents.ItemsSource = _codingImportEvents;
        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();

        // WICHTIG: Auch session.Events leeren, damit CompleteSession() nur neue
        // KI-Events enthaelt (Import-Events sind in _codingImportEvents gesichert).
        // Sonst: Duplikate im Protokoll (Import + neue KI-Events).
        _codingSessionService.ActiveSession?.Events.Clear();

        // KI-Events-Liste binden (startet leer)
        LstCodingEvents.ItemsSource = _codingVm.Events;
        RunCodingDefectCount.Text = "0";

        // UI einblenden
        CodingOverlayPopup.IsOpen = true;
        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(700);
        CodingToolbar.Visibility = Visibility.Visible;

        // PipeGraphTimeline einrichten und einblenden
        PipeTimeline.TotalLength = _codingVm.EndMeter;
        PipeTimeline.MeterAccessor = obj => obj is CodingEvent ce ? ce.MeterAtCapture : 0;
        PipeTimeline.CodeAccessor = obj => obj is CodingEvent ce ? ce.Entry.Code : "?";
        PipeTimeline.ConfidenceAccessor = obj => obj is CodingEvent ce && ce.AiContext != null
            ? ce.AiContext.Confidence : -1;
        PipeTimeline.IsRejectedAccessor = obj => obj is CodingEvent ce
            && CodingSessionViewModel.GetDefectStatus(ce) == DefectStatus.Rejected;
        PipeTimeline.Markers = _codingVm.Events;
        PipeTimeline.NavigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (_codingVm.IsRunning || _codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });
        CodingTimelinePanel.Visibility = Visibility.Visible;

        // KI initialisieren + OSD-Timer starten
        InitCodingAi();
        StartCodingOsdTimer();

        // OSD-Badge sofort sichtbar
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";

        // Bestehende Protokoll-Eintraege direkt in Import-Referenz laden
        // (NICHT in KI-Befunde — die startet leer)
        LoadExistingProtocolEventsAsImport();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Import-Referenz-Liste.
    /// KI-Befunde-Liste bleibt leer (KI erkennt frisch).
    /// </summary>
    private void LoadExistingProtocolEventsAsImport()
    {
        if (_haltungRecord?.Protocol?.Current?.Entries == null) return;

        var entries = _haltungRecord.Protocol.Current.Entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        foreach (var entry in entries)
        {
            // Duplikat-Check (CodingSessionService hat evtl. schon geladen)
            if (_codingImportEvents.Any(ev => ev.Entry.EntryId == entry.EntryId))
                continue;

            _codingImportEvents.Add(new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            });
        }

        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();
    }

    private void ExitCodingMode()
    {
        if (!_isCodingMode) return;
        _isCodingMode = false;

        // User-Klage 2026-04-25: "Wenn ich nach dem Codieren das Fenster schliesse,
        // schliesst es das ganze Programm." Ursache: Dieser 90-Zeilen-Cleanup ohne
        // globalen Schutz. CloseOpenStreckenschaeden zeigt einen Dialog, der bei
        // Window-Close-Race werfen kann. EnsureRohrendeExists schreibt in
        // Datenstrukturen die schon disposed sein koennen. Jede dieser Exceptions
        // eskaliert ueber DispatcherUnhandledException und kann die App killen.
        // Fix: jeder Block einzeln in try/catch via Safe()-Helper.
        void Safe(string step, Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExitCodingMode] {step}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Beim Verlassen: IMMER offene Streckenschaeden schliessen
        // (egal ob Rohrende, Abbruch oder einfacher Exit)
        bool exitAborted = false;
        Safe("Streckenschaeden+Endcode", () =>
        {
            if (_codingVm != null && _codingVm.Events.Count > 0)
            {
                var endMeter = _codingLastOsdMeter ?? _codingVm.EndMeter;
                if (!CloseOpenStreckenschaeden(endMeter))
                {
                    // User hat "Abbrechen" geklickt → Exit abbrechen, weiter codieren
                    _isCodingMode = true;
                    exitAborted = true;
                    return;
                }

                // Ende-Code nur einfuegen wenn weder BCE (Rohrende) noch BDC (Abbruch) vorhanden
                bool hasEndCode = _codingVm.Events.Any(e =>
                    string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Entry.Code, "BDC", StringComparison.OrdinalIgnoreCase));
                if (!hasEndCode)
                {
                    var endTime = TimeSpan.FromMilliseconds(_player?.Length ?? 0);
                    EnsureRohrendeExists(_codingVm.EndMeter, endTime);
                }
            }
        });
        if (exitAborted) return;

        Safe("Timer-Stop", () =>
        {
            StopCodingOsdTimer();
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;
            StopCodingAiPulse();
        });

        Safe("AnalysisCts", () =>
        {
            _codingAnalysisCts?.Cancel();
            _codingAnalysisCts?.Dispose();
            _codingAnalysisCts = null;
        });

        Safe("ImportEvents-Clear", () =>
        {
            _codingImportEvents.Clear();
            LstImportEvents.ItemsSource = null;
        });

        Safe("ConfirmationPanels-Hide", () =>
        {
            CodingConfirmationPanel.Visibility = Visibility.Collapsed;
            DetectionConfirmationPanel.Visibility = Visibility.Collapsed;
            _codingPendingConfirmEvent = null;
            _codingPendingGateResult = null;
            _detectionPendingFindings = null;
            _detectionPendingFrameBytes = null;
            _detectionPendingTimestampSec = null;
            DetectionCanvas.Children.Clear();
            if (!_isDetecting)
                DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        });

        Safe("OverlayCanvas-Cleanup", () =>
        {
            if (CodingOverlayCanvas.IsMouseCaptured)
                CodingOverlayCanvas.ReleaseMouseCapture();
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.Children.Clear();
            CodingOverlayCanvas.IsHitTestVisible = false;
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
        });

        Safe("UI-Hide", () =>
        {
            CodingSidePanel.Visibility = Visibility.Collapsed;
            CodingSidePanelColumn.Width = new GridLength(0);
            CodingToolbar.Visibility = Visibility.Collapsed;
            CodingTimelinePanel.Visibility = Visibility.Collapsed;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
            CodingCalibrationHint.Visibility = Visibility.Collapsed;
            CodingMeasurementPanel.Visibility = Visibility.Collapsed;
            OsdMeterBadge.Visibility = Visibility.Collapsed;
            LiveDetectionButton.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Visibility = _isDetecting ? Visibility.Visible : Visibility.Collapsed;
        });

        Safe("Tool-State-Reset", () =>
        {
            _activeCodingToolName = null;
            TxtActiveToolLabel.Text = "";
            BtnCodingLiveAi.IsChecked = false;
            TxtCodingAiStage.Text = string.Empty;
            _codingSchemaManager.Cancel();
            _codingSchemaType = null;
        });

        Safe("VM-Unsubscribe+Null", () =>
        {
            if (_codingVm != null)
                _codingVm.PropertyChanged -= CodingVm_PropertyChanged;
            _codingVm = null;
            _codingSessionService = null;
            _codingOverlayService = null;
            _codingIsCalibrating = false;
            _codingCalibStart = null;
            ResetFrameReadiness(); // setzt auch _codingLastOsdMeter = null
            _codingOverlaySuspendDepth = 0;
            _codingOverlayWasOpenBeforeSuspend = false;
        });
    }

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
            var sample = Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);
            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(new List<Ai.Training.TrainingSample> { sample })
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
            var samples = new System.Collections.Generic.List<Ai.Training.TrainingSample>();
            foreach (var ev in _codingVm.Events)
            {
                var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
                var sample = Ai.Training.CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);

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
                Ai.Training.TrainingSamplesStore.MergeAndSaveAsync(samples)
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

    private void CodingToolBend_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.PipeDirection, SchemaType.PipeDirection);

    private void CodingToolLevel_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.Level, SchemaType.FillLevel, LevelMode.Water);

    private void CodingToolIntrusion_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.Level, SchemaType.Intrusion, LevelMode.Obstacle);

    private string? _activeCodingToolName;

    private void SetCodingTool(
        object activeBtn,
        OverlayToolType tool,
        SchemaType? schemaType = null,
        LevelMode? levelMode = null)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = false;
        _codingCalibStart = null;

        // Popup schliessen
        ToolsDropdownPopup.IsOpen = false;

        // Toggle: gleiches Tool nochmal → deaktivieren
        string btnName = (activeBtn as FrameworkElement)?.Name ?? "";
        bool activate = !string.Equals(_activeCodingToolName, btnName);
        _activeCodingToolName = activate ? btnName : null;

        if (activate && levelMode.HasValue)
            _codingOverlayService.ActiveLevelMode = levelMode.Value;

        _codingOverlayService.ActiveTool = activate ? tool : OverlayToolType.None;
        _codingSchemaType = activate ? schemaType : null;
        _codingSchemaManager.Cancel();

        // Aktives Tool-Label anzeigen
        string label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();
        TxtActiveToolLabel.Text = activate ? label : "";

        // Offene Zeichnung verwerfen, damit das naechste Tool sauber startet.
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        // Overlay-Canvas oeffnen/schliessen je nach Aktivierung
        if (activate && !CodingOverlayPopup.IsOpen)
        {
            _player.SetPause(true);
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
        }
        else if (!activate && CodingOverlayPopup.IsOpen)
        {
            CodingOverlayPopup.IsOpen = false;
        }

        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void SuspendCodingOverlayInput()
    {
        _codingOverlaySuspendDepth++;
        if (_codingOverlaySuspendDepth > 1)
            return;

        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        _codingSchemaManager.EndDrag();
        _codingOverlayService?.CancelDraw();
        _codingOverlayWasOpenBeforeSuspend = CodingOverlayPopup.IsOpen;
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
        if (_codingOverlayWasOpenBeforeSuspend)
            CodingOverlayPopup.IsOpen = false;
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        if (_codingOverlayWasOpenBeforeSuspend)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayCursor();
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void UpdateCodingOverlayCursor()
    {
        if (!CodingOverlayPopup.IsOpen)
        {
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
            return;
        }

        var activeTool = _codingOverlayService?.ActiveTool ?? OverlayToolType.None;
        var isInteractive = _codingIsCalibrating || activeTool != OverlayToolType.None;
        CodingOverlayCanvas.Cursor = isInteractive ? Cursors.Cross : Cursors.Arrow;
    }

    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        ToolsDropdownPopup.IsOpen = false;
        _codingIsCalibrating = !_codingIsCalibrating;
        _codingCalibStart = null;
        _codingOverlayService.ActiveTool = OverlayToolType.None;
        _activeCodingToolName = _codingIsCalibrating ? "BtnCodingCalibrate" : null;
        TxtActiveToolLabel.Text = _codingIsCalibrating ? "Kalibrieren" : "";

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationHint.Visibility = _codingIsCalibrating ? Visibility.Visible : Visibility.Collapsed;
        TxtCodingCalibHint.Text = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private bool IsCodingSchemaToolSelected()
        => _codingSchemaType.HasValue
           && _codingOverlayService?.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level or OverlayToolType.PipeDirection;

    private SchemaOverlayBase? CreateCodingSchemaOverlay()
    {
        if (_codingOverlayService == null || _codingSchemaType == null)
            return null;

        return _codingSchemaType.Value switch
        {
            SchemaType.PipeBend => new PipeBendSchema
            {
                SnapEnabled = _codingOverlayService.PipeBendSnapEnabled
            },
            SchemaType.FillLevel => new FillLevelSchema
            {
                Mode = _codingOverlayService.ActiveLevelMode
            },
            SchemaType.Intrusion => new IntrusionSchema(),
            SchemaType.PipeDirection => new PipeDirectionSchema(),
            _ => null
        };
    }

    private string GetDefaultCodingSchemaHandleId()
        => _codingSchemaType switch
        {
            SchemaType.PipeBend => "vertex",
            SchemaType.FillLevel => "level",
            SchemaType.Intrusion => "depth",
            SchemaType.PipeDirection => "center1",
            _ => "vertex"
        };

    private OverlayGeometry? BuildCodingSchemaGeometry()
    {
        if (_codingSchemaManager.Active is PipeBendSchema bend)
        {
            var (arm1, arm2) = bend.GetArmEndpoints();
            var angle = bend.SnapEnabled
                ? new[] { 15d, 30d, 45d, 90d }
                    .OrderBy(candidate => Math.Abs(candidate - bend.AngleDeg))
                    .First()
                : Math.Round(bend.AngleDeg, 1);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points = new List<NormalizedPoint> { arm1, bend.Center, arm2 },
                ArcDegrees = Math.Round(angle, 1)
            };
        }

        if (_codingSchemaManager.Active is FillLevelSchema fill)
        {
            double levelY = fill.GetLevelLineY();
            double dy = levelY - fill.PipeCenter.Y;
            double halfChord = Math.Sqrt(Math.Max(0, fill.PipeRadius * fill.PipeRadius - dy * dy));
            double pct = OverlayToolService.CircleSegmentPercent(fill.FillRatio);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint>
                {
                    new(fill.PipeCenter.X - halfChord, levelY),
                    new(fill.PipeCenter.X + halfChord, levelY)
                },
                FillPercent = Math.Round(pct, 1),
                LevelSubMode = fill.Mode
            };
        }

        if (_codingSchemaManager.Active is IntrusionSchema intrusion)
        {
            var edge = intrusion.GetEdgePoint();
            var tip = intrusion.GetIntrusionTip();
            var (left, right) = intrusion.GetSpreadEdges();
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint> { edge, tip, intrusion.PipeCenter, left, right },
                FillPercent = Math.Round(intrusion.DepthRatio * 100.0, 1),
                LevelSubMode = LevelMode.Obstacle,
                ClockFrom = Math.Round(intrusion.ClockHour, 1)
            };
        }

        if (_codingSchemaManager.Active is PipeDirectionSchema pipeDir)
        {
            return pipeDir.BuildGeometry();
        }

        return null;
    }

    private void UpdateCodingSchemaOverlay(bool enableCreateEvent)
    {
        if (_codingVm == null) return;

        _codingVm.CurrentOverlay = BuildCodingSchemaGeometry();
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
        BtnCodingCreateEvent.IsEnabled = enableCreateEvent && _codingVm.CurrentOverlay != null;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderActiveCodingSchema();
    }

    private void ClearCodingSchemaOverlay(bool redraw)
    {
        _codingSchemaManager.Cancel();
        if (_codingVm != null)
            _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        if (redraw)
            RedrawCodingCanvas(includeManualOverlay: false);
    }

    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker hat Vorrang: Rechteck ziehen
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseDown(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }
        // Input-Phase: Canvas-Klicks ignorieren (ComboBox ist aktiv)
        if (_eingabemarkerPhase == EingabemarkerPhase.Input ||
            _eingabemarkerPhase == EingabemarkerPhase.Analyzing)
        {
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating)
        {
            _codingCalibStart = norm;
            CodingOverlayCanvas.CaptureMouse();
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            return;
        }

        if (_codingOverlayService.ActiveTool == OverlayToolType.None) return;

        if (IsCodingSchemaToolSelected())
        {
            if (!_codingSchemaManager.IsActive)
            {
                // Schema noch nicht platziert oder wartet auf zweiten Klick (PipeDirection)
                if (_codingSchemaManager.Active is PipeDirectionSchema pd && pd.IsWaitingForSecondClick)
                {
                    // Zweiter Klick: Platziert die zweite Ellipse → Adjusting
                    _codingSchemaManager.Place(norm);
                    UpdateCodingSchemaOverlay(enableCreateEvent: true);
                    return;
                }

                var schema = CreateCodingSchemaOverlay();
                if (schema == null) return;
                _codingSchemaManager.Activate(schema, _codingOverlayService.Calibration);
                _codingSchemaManager.Place(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
                return;
            }

            var handleId = _codingSchemaManager.HitTest(norm, 0.035) ?? GetDefaultCodingSchemaHandleId();
            _codingSchemaManager.BeginDrag(handleId);
            _codingSchemaManager.UpdateDrag(norm);
            CodingOverlayCanvas.CaptureMouse();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        // Multi-Punkt-Werkzeug (Winkelmesser: 3 Klicks)
        if (_codingOverlayService.IsMultiPointTool)
        {
            // Beim ersten Klick Reset
            if (_codingOverlayService.DrawPointCount == 0)
            {
                _codingVm.CurrentOverlay = null;
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }

            bool complete = _codingVm.OnCanvasMultiPointClick(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();

            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: !complete);

            if (complete)
            {
                UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
                BtnCodingCreateEvent.IsEnabled = true;
                if (BtnCodingLiveAi.IsChecked == true && _codingVm.CurrentOverlay != null)
                    AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHint");
            }
            return; // Kein CaptureMouse bei Multi-Punkt
        }

        // Standard 2-Punkt-Werkzeug (Klick+Drag)
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        _codingVm.OnCanvasMouseDown(norm);
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Eingabemarker Rechteck-Drag
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing && _eingabemarkerPreviewRect != null)
        {
            EingabemarkerCanvas_MouseMove(e.GetPosition(CodingOverlayCanvas));
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();

            var p1 = CodingNormToPixel(_codingCalibStart);
            var p2 = CodingNormToPixel(norm);
            _codingPreviewLine = new System.Windows.Shapes.Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Magenta,
                StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Tag = "overlay_preview"
            };
            CodingOverlayCanvas.Children.Add(_codingPreviewLine);
            double pxLen = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            TxtCodingCalibHint.Text = $"Referenzlinie: {pxLen:F0} px";
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsActive)
        {
            if (_codingSchemaManager.IsDragging)
            {
                _codingSchemaManager.UpdateDrag(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
            }
            return;
        }

        // Multi-Punkt-Vorschau (Winkelmesser: Mausbewegung zwischen Klicks)
        if (_codingOverlayService.IsMultiPointTool && _codingOverlayService.DrawPointCount > 0)
        {
            _codingVm.OnCanvasMultiPointMove(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();
            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseMove(norm);
        if (_codingVm.CurrentOverlay == null) return;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker Rechteck fertig
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseUp(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            CodingOverlayCanvas.ReleaseMouseCapture();
            ApplyCodingCalibration(_codingCalibStart, norm);
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsDragging)
        {
            _codingSchemaManager.UpdateDrag(norm);
            _codingSchemaManager.EndDrag();
            CodingOverlayCanvas.ReleaseMouseCapture();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseUp(norm);
        CodingOverlayCanvas.ReleaseMouseCapture();

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        if (_codingVm.CurrentOverlay != null)
        {
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

            // Mark-Modus ODER Rectangle-Overlay: direkt VsaCodeExplorer oeffnen
            // + SAM + Training speichern via HandleMarkDrawingComplete.
            //
            // User-Wunsch 2026-04-26: "Im Codiermodus, waere es gut wenn ich
            // markiere BBox das Fenster zum Codieren jedesmal aufgeht."
            // Vorher: Rectangle-Overlay rief nur SAM auf, User musste manuell
            // auf "Befund erstellen"-Button klicken UND vorher Code waehlen.
            // Jetzt: Rectangle-Overlay = sofort VsaCodeExplorer wie im Mark-Modus.
            //
            // Andere Geometrien (Linie/Bogen/Stretch/Punkt/Level/PipeBend) bleiben
            // manuell — fuer Streckenschaeden/Bogenwinkel etc. ist der zweistufige
            // Workflow korrekt (zuerst messen, dann Code).
            if (_markToolType != OverlayToolType.None
                || _codingVm.CurrentOverlay.ToolType == OverlayToolType.Rectangle)
            {
                HandleMarkDrawingComplete();
                return;
            }

            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHintAutoAi");
        }
        else
        {
            UpdateCodingOverlayInfo(null);
            BtnCodingCreateEvent.IsEnabled = false;
        }
    }

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mausrad: Winkel der PipeBend-Schablone aendern (5° pro Schritt)
        if (_codingSchemaManager.Active is PipeBendSchema bend && _codingSchemaManager.IsActive)
        {
            double delta = e.Delta > 0 ? 5 : -5;
            bend.AdjustAngle(delta);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            e.Handled = true;
        }
    }

    private void ApplyCodingCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        if (_codingOverlayService == null) return;
        var p1 = CodingNormToPixel(start);
        var p2 = CodingNormToPixel(end);
        double pixelDiameter = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

        if (pixelDiameter < 10)
        {
            TxtCodingCalibHint.Text = "Linie zu kurz - bitte nochmal";
            _codingCalibStart = null;
            return;
        }

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double normDiameter = Math.Sqrt(dx * dx + dy * dy);
        int dn = _codingOverlayService.Calibration?.NominalDiameterMm ?? 300;

        var cal = new PipeCalibration
        {
            NominalDiameterMm = dn,
            PipePixelDiameter = pixelDiameter,
            NormalizedDiameter = normDiameter,
            PipeCenter = center,
            WasManuallyCalibrated = true
        };
        _codingOverlayService.SetCalibration(cal);
        _codingSchemaManager.Active?.ApplyCalibration(cal);

        TxtCodingCalibStatus.Text = $"Kalibriert: {cal.MmPerNormUnit:F1} mm/norm";
        TxtCodingCalibHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        if (string.Equals(_activeCodingToolName, "BtnCodingCalibrate"))
            _activeCodingToolName = null;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        UpdateCodingOverlayCursor();
        if (_codingSchemaManager.IsActive)
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
    }

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

    private void RenderOverlayGeometry(OverlayGeometry overlay, bool isPreview, NormalizedPoint? labelAnchor = null)
    {
        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        string tag = isPreview ? "overlay_preview" : "overlay_manual";
        var stroke = isPreview
            ? Brushes.Lime
            : new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
        var fill = isPreview
            ? new SolidColorBrush(Color.FromArgb(50, 0x00, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(35, 0x00, 0xE5, 0xFF));
        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        switch (overlay.ToolType)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
                if (overlay.Points.Count >= 2)
                {
                    var p1 = CodingNormToPixel(overlay.Points[0]);
                    var p2 = CodingNormToPixel(overlay.Points[1]);
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = stroke,
                        StrokeThickness = 3,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        line.StrokeDashArray = new DoubleCollection { 4, 2 };
                    CodingOverlayCanvas.Children.Add(line);
                }
                break;

            case OverlayToolType.Rectangle:
                if (overlay.Points.Count >= 4)
                {
                    var xs = overlay.Points.Select(p => p.X * w).ToList();
                    var ys = overlay.Points.Select(p => p.Y * h).ToList();
                    double minX = xs.Min();
                    double maxX = xs.Max();
                    double minY = ys.Min();
                    double maxY = ys.Max();

                    var rect = new Rectangle
                    {
                        Width = Math.Max(1, maxX - minX),
                        Height = Math.Max(1, maxY - minY),
                        Stroke = stroke,
                        StrokeThickness = 3,
                        Fill = fill,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        rect.StrokeDashArray = new DoubleCollection { 4, 2 };

                    Canvas.SetLeft(rect, minX);
                    Canvas.SetTop(rect, minY);
                    CodingOverlayCanvas.Children.Add(rect);
                }
                break;

            case OverlayToolType.Point:
                if (overlay.Points.Count >= 1)
                {
                    var p = CodingNormToPixel(overlay.Points[0]);
                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = stroke,
                        Stroke = Brushes.White,
                        StrokeThickness = 2,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    Canvas.SetLeft(dot, p.X - 8);
                    Canvas.SetTop(dot, p.Y - 8);
                    CodingOverlayCanvas.Children.Add(dot);
                }
                break;

            case OverlayToolType.Arc:
                if (overlay.Points.Count >= 2)
                {
                    var arc = CreateArcPath(overlay.Points[0], overlay.Points[1], stroke, glowEffect, tag, isPreview);
                    if (arc != null)
                        CodingOverlayCanvas.Children.Add(arc);
                }
                break;

            case OverlayToolType.PipeBend:
                RenderPipeBendOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.PipeDirection:
                RenderPipeDirectionOverlay(overlay, isPreview, glowEffect, tag);
                return;

            case OverlayToolType.LateralCircle:
                RenderLateralCircleOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ruler:
                RenderRulerOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Level:
                RenderLevelOverlay(overlay, isPreview, glowEffect, tag);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ellipse:
                if (overlay.Points.Count >= 2)
                {
                    var ep1 = CodingNormToPixel(overlay.Points[0]);
                    var ep2 = CodingNormToPixel(overlay.Points[1]);
                    var elli = new System.Windows.Shapes.Ellipse
                    {
                        Width = Math.Max(1, Math.Abs(ep2.X - ep1.X)),
                        Height = Math.Max(1, Math.Abs(ep2.Y - ep1.Y)),
                        Stroke = isPreview ? Brushes.MediumPurple : new SolidColorBrush(Color.FromRgb(147, 112, 219)),
                        StrokeThickness = isPreview ? 2 : 2.5,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        elli.StrokeDashArray = new DoubleCollection { 4, 2 };
                    Canvas.SetLeft(elli, Math.Min(ep1.X, ep2.X));
                    Canvas.SetTop(elli, Math.Min(ep1.Y, ep2.Y));
                    CodingOverlayCanvas.Children.Add(elli);
                }
                break;

            case OverlayToolType.Freehand:
                if (overlay.Points.Count >= 3)
                {
                    // Geschlossenes Polygon (nicht offene Polyline) — umschliesst den Schadensbereich
                    var poly = new System.Windows.Shapes.Polygon
                    {
                        Stroke = isPreview ? Brushes.HotPink : new SolidColorBrush(Color.FromRgb(255, 105, 180)),
                        StrokeThickness = isPreview ? 2 : 2.5,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = new SolidColorBrush(Color.FromArgb(25, 255, 105, 180)), // Leicht gefuellt
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        poly.StrokeDashArray = new DoubleCollection { 3, 2 };
                    foreach (var pt in overlay.Points)
                    {
                        var px = CodingNormToPixel(pt);
                        poly.Points.Add(new Point(px.X, px.Y));
                    }
                    CodingOverlayCanvas.Children.Add(poly);
                }
                break;
        }

        var text = BuildOverlayMeasurementText(overlay);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var anchorNorm = labelAnchor ?? overlay.Points.LastOrDefault() ?? new NormalizedPoint(0.5, 0.5);
            var anchor = CodingNormToPixel(anchorNorm);

            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(5, 2, 5, 2),
                Effect = glowEffect,
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(label, anchor.X + 12);
            Canvas.SetTop(label, anchor.Y - 20);
            CodingOverlayCanvas.Children.Add(label);
        }
    }

    private void RenderActiveCodingSchema()
    {
        if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)
            return;

        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.95
        };

        switch (_codingSchemaManager.Active)
        {
            case PipeBendSchema bend:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay != null)
                    RenderPipeBendOverlay(overlay, true, Brushes.Gold, glowEffect, "overlay_preview", bend.Center);

                var center = CodingNormToPixel(bend.Center);
                var radiusHandle = CodingNormToPixel(bend.GetRadiusHandle());

                var guide = new System.Windows.Shapes.Line
                {
                    X1 = center.X,
                    Y1 = center.Y,
                    X2 = radiusHandle.X,
                    Y2 = radiusHandle.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 184, 0)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(guide);

                AddDotMarker(radiusHandle, 5, Brushes.White, "overlay_preview", glowEffect);
                break;
            }

            case FillLevelSchema fill:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null || overlay.Points.Count < 2)
                    return;

                var strokeColor = fill.Mode switch
                {
                    LevelMode.Water => Color.FromRgb(65, 105, 225),
                    LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
                    _ => Color.FromRgb(210, 105, 30)
                };
                var stroke = new SolidColorBrush(strokeColor);
                var fillBrush = new SolidColorBrush(Color.FromArgb(68, strokeColor.R, strokeColor.G, strokeColor.B));

                RenderSchemaPipeReference(fill.PipeCenter, fill.PipeRadius, stroke, glowEffect, "overlay_preview");

                var center = CodingNormToPixel(fill.PipeCenter);
                double rPx = fill.PipeRadius * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);
                double rx = rPx;
                double ry = rPx;
                double top = center.Y - rPx;
                double bottom = center.Y + rPx;
                var lineP1 = CodingNormToPixel(overlay.Points[0]);
                var lineP2 = CodingNormToPixel(overlay.Points[1]);
                double levelY = lineP1.Y;

                var segment = new Rectangle
                {
                    Width = Math.Max(1, rx * 2),
                    Height = Math.Max(1, fill.Mode == LevelMode.Obstacle ? levelY - top : bottom - levelY),
                    Fill = fillBrush,
                    Tag = "overlay_preview",
                    Clip = new EllipseGeometry(center, rx, ry)
                };
                Canvas.SetLeft(segment, center.X - rx);
                Canvas.SetTop(segment, fill.Mode == LevelMode.Obstacle ? top : levelY);
                CodingOverlayCanvas.Children.Add(segment);

                var levelLine = new System.Windows.Shapes.Line
                {
                    X1 = lineP1.X,
                    Y1 = levelY,
                    X2 = lineP2.X,
                    Y2 = levelY,
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(levelLine);

                AddDotMarker(new Point(center.X, levelY), 6, stroke, "overlay_preview", glowEffect);
                AddSchemaLabel(new Point(center.X, levelY), $"{overlay.FillPercent:F1}%", stroke, glowEffect);
                break;
            }

            case IntrusionSchema intrusion:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null)
                    return;

                var stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                var fillBrush = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));

                RenderSchemaPipeReference(intrusion.PipeCenter, intrusion.PipeRadius, stroke, glowEffect, "overlay_preview");

                var tip = CodingNormToPixel(intrusion.GetIntrusionTip());
                var edge = CodingNormToPixel(intrusion.GetEdgePoint());
                var (leftNorm, rightNorm) = intrusion.GetSpreadEdges();
                var left = CodingNormToPixel(leftNorm);
                var right = CodingNormToPixel(rightNorm);

                var tongue = new System.Windows.Shapes.Polygon
                {
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    Fill = fillBrush,
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                tongue.Points.Add(left);
                tongue.Points.Add(tip);
                tongue.Points.Add(right);
                CodingOverlayCanvas.Children.Add(tongue);

                var spine = new System.Windows.Shapes.Line
                {
                    X1 = edge.X,
                    Y1 = edge.Y,
                    X2 = tip.X,
                    Y2 = tip.Y,
                    Stroke = stroke,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(spine);

                AddDotMarker(tip, 7, stroke, "overlay_preview", glowEffect);
                AddDotMarker(edge, 5, Brushes.White, "overlay_preview", glowEffect);
                AddSchemaLabel(tip, $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h", stroke, glowEffect);
                break;
            }

            case PipeDirectionSchema pipeDir:
            {
                var pipeDirectionOverlay = BuildCodingSchemaGeometry();
                if (pipeDirectionOverlay != null)
                    RenderPipeDirectionOverlay(pipeDirectionOverlay, true, glowEffect, "overlay_preview");

                // Zwei Ellipsen + Verbindungslinie + Winkel-Label
                var stroke1 = new SolidColorBrush(Color.FromRgb(0, 200, 255));   // Cyan
                var stroke2 = new SolidColorBrush(Color.FromRgb(255, 165, 0));   // Orange
                var fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 200, 255));

                var c1 = CodingNormToPixel(pipeDir.Center1);
                var c2 = CodingNormToPixel(pipeDir.Center2);

                double canvasW = CodingOverlayCanvas.ActualWidth;
                double canvasH = CodingOverlayCanvas.ActualHeight;

                // Ellipse 1 (Rohrverbindung — Cyan)
                double rx1Px = pipeDir.RadiusX1 * canvasW;
                double ry1Px = pipeDir.RadiusY1 * canvasH;
                var ellipse1 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx1Px * 2, Height = ry1Px * 2,
                    Stroke = stroke1, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = fillBrush,
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse1, c1.X - rx1Px);
                Canvas.SetTop(ellipse1, c1.Y - ry1Px);
                CodingOverlayCanvas.Children.Add(ellipse1);

                // Ellipse 2 (weiter im Rohr — Orange)
                double rx2Px = pipeDir.RadiusX2 * canvasW;
                double ry2Px = pipeDir.RadiusY2 * canvasH;
                var ellipse2 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx2Px * 2, Height = ry2Px * 2,
                    Stroke = stroke2, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse2, c2.X - rx2Px);
                Canvas.SetTop(ellipse2, c2.Y - ry2Px);
                CodingOverlayCanvas.Children.Add(ellipse2);

                // Verbindungslinie (Richtungswechsel)
                var connector = new System.Windows.Shapes.Line
                {
                    X1 = c1.X, Y1 = c1.Y, X2 = c2.X, Y2 = c2.Y,
                    Stroke = Brushes.White, StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(connector);

                // Handles
                AddDotMarker(c1, 7, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(c2, 7, stroke2, "overlay_preview", glowEffect);

                // Groessen-Handles (kleine Punkte an den Ellipsenraendern)
                AddDotMarker(new Point(c1.X + rx1Px, c1.Y), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c1.X, c1.Y + ry1Px), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X + rx2Px, c2.Y), 4, stroke2, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X, c2.Y + ry2Px), 4, stroke2, "overlay_preview", glowEffect);

                // Winkel-Label
                var midPoint = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
                AddSchemaLabel(midPoint, $"{pipeDir.AngleDeg:F0}°", Brushes.White, glowEffect);
                break;
            }
        }
    }

    private void RenderSchemaPipeReference(
        NormalizedPoint centerNorm,
        double radiusNorm,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        var center = CodingNormToPixel(centerNorm);
        // Kreisprofil: Radius in Pixel basierend auf Canvas-Hoehe
        // (Hoehe ist die kuerzere Dimension, damit der Kreis immer rund bleibt)
        double rPx = radiusNorm * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);

        var pipe = new System.Windows.Shapes.Ellipse
        {
            Width = rPx * 2,
            Height = rPx * 2,
            Stroke = stroke,
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        Canvas.SetLeft(pipe, center.X - rPx);
        Canvas.SetTop(pipe, center.Y - rPx);
        CodingOverlayCanvas.Children.Add(pipe);
    }

    private void AddSchemaLabel(
        Point anchor,
        string text,
        Brush foreground,
        System.Windows.Media.Effects.DropShadowEffect glowEffect)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Background = new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = glowEffect,
            Tag = "overlay_measure"
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        CodingOverlayCanvas.Children.Add(label);
    }

    private void RenderLevelOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        if (overlay.Points.Count >= 5)
        {
            var intrusionStroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            var edge = CodingNormToPixel(overlay.Points[0]);
            var tip = CodingNormToPixel(overlay.Points[1]);
            var pipeCenter = overlay.Points[2];
            var left = CodingNormToPixel(overlay.Points[3]);
            var right = CodingNormToPixel(overlay.Points[4]);
            var pipeRadius = _codingOverlayService?.Calibration?.NormalizedDiameter / 2.0 ?? 0.35;

            RenderSchemaPipeReference(pipeCenter, pipeRadius, intrusionStroke, glowEffect, tag);

            var tongue = new System.Windows.Shapes.Polygon
            {
                Stroke = intrusionStroke,
                StrokeThickness = 2.5,
                Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)72 : (byte)95, 239, 68, 68)),
                Effect = glowEffect,
                Tag = tag
            };
            tongue.Points.Add(left);
            tongue.Points.Add(tip);
            tongue.Points.Add(right);
            CodingOverlayCanvas.Children.Add(tongue);

            var spine = new System.Windows.Shapes.Line
            {
                X1 = edge.X,
                Y1 = edge.Y,
                X2 = tip.X,
                Y2 = tip.Y,
                Stroke = intrusionStroke,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Effect = glowEffect,
                Tag = tag
            };
            CodingOverlayCanvas.Children.Add(spine);

            AddDotMarker(tip, 6, intrusionStroke, tag, glowEffect);
            if (overlay.FillPercent.HasValue)
                AddSchemaLabel(tip, $"Einragung {overlay.FillPercent:F1}%", intrusionStroke, glowEffect);
            return;
        }

        if (overlay.Points.Count < 2)
            return;

        var p1 = CodingNormToPixel(overlay.Points[0]);
        var p2 = CodingNormToPixel(overlay.Points[1]);
        double y = p1.Y;
        var strokeColor = overlay.LevelSubMode switch
        {
            LevelMode.Water => Color.FromRgb(65, 105, 225),
            LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
            _ => Color.FromRgb(210, 105, 30)
        };
        var stroke = new SolidColorBrush(strokeColor);

        var line = new System.Windows.Shapes.Line
        {
            X1 = p1.X,
            Y1 = y,
            X2 = p2.X,
            Y2 = y,
            Stroke = stroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        CodingOverlayCanvas.Children.Add(line);

        if (_codingOverlayService?.Calibration is { IsCalibrated: true } cal)
        {
            RenderSchemaPipeReference(cal.PipeCenter, cal.NormalizedDiameter / 2.0, stroke, glowEffect, tag);

            var center = CodingNormToPixel(cal.PipeCenter);
            double rPxCal = (cal.NormalizedDiameter / 2.0) * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);
            double rx = rPxCal;
            double ry = rPxCal;
            double top = center.Y - rPxCal;
            double bottom = center.Y + rPxCal;

            var segment = new Rectangle
            {
                Width = Math.Max(1, rx * 2),
                Height = Math.Max(1, overlay.LevelSubMode == LevelMode.Obstacle ? y - top : bottom - y),
                Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)68 : (byte)88, strokeColor.R, strokeColor.G, strokeColor.B)),
                Tag = tag,
                Clip = new EllipseGeometry(center, rx, ry)
            };
            Canvas.SetLeft(segment, center.X - rx);
            Canvas.SetTop(segment, overlay.LevelSubMode == LevelMode.Obstacle ? top : y);
            CodingOverlayCanvas.Children.Add(segment);
        }

        if (overlay.FillPercent.HasValue)
            AddSchemaLabel(new Point((p1.X + p2.X) / 2, y), $"{overlay.FillPercent:F1}%", stroke, glowEffect);
    }

    private System.Windows.Shapes.Path? CreateArcPath(
        NormalizedPoint start,
        NormalizedPoint end,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect effect,
        string tag,
        bool dashed)
    {
        var centerNorm = _codingOverlayService?.Calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
        var center = CodingNormToPixel(centerNorm);
        var sp = CodingNormToPixel(start);
        var ep = CodingNormToPixel(end);

        double radius = Math.Sqrt(Math.Pow(sp.X - center.X, 2) + Math.Pow(sp.Y - center.Y, 2));
        if (radius < 3)
            return null;

        double startAngle = Math.Atan2(sp.X - center.X, -(sp.Y - center.Y));
        double endAngle = Math.Atan2(ep.X - center.X, -(ep.Y - center.Y));
        double angleDiff = endAngle - startAngle;
        if (angleDiff < 0) angleDiff += 2 * Math.PI;

        var arcEnd = new Point(
            center.X + radius * Math.Sin(endAngle),
            center.Y - radius * Math.Cos(endAngle));

        var figure = new System.Windows.Media.PathFigure { StartPoint = sp, IsClosed = false };
        figure.Segments.Add(new System.Windows.Media.ArcSegment(
            arcEnd,
            new Size(radius, radius),
            0,
            angleDiff > Math.PI,
            System.Windows.Media.SweepDirection.Clockwise,
            true));

        var geometry = new System.Windows.Media.PathGeometry();
        geometry.Figures.Add(figure);

        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 3,
            Effect = effect,
            Tag = tag
        };
        if (dashed)
            path.StrokeDashArray = new DoubleCollection { 4, 2 };

        return path;
    }

    // --- Winkelmesser (Protractor): 2 Linien + Winkelbogen + Grad-Label ---

    /// <summary>
    /// Zeichnet ein gespeichertes PipeDirection-Overlay (2 Ellipsen + Winkel).
    /// Points: [Center1, Corner1(cx+rx, cy+ry), Center2, Corner2(cx+rx, cy+ry)]
    /// </summary>
    private void RenderPipeDirectionOverlay(
        OverlayGeometry overlay, bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag)
    {
        if (overlay.Points.Count < 4) return;

        var c1 = CodingNormToPixel(overlay.Points[0]);
        var corner1 = CodingNormToPixel(overlay.Points[1]);
        var c2 = CodingNormToPixel(overlay.Points[2]);
        var corner2 = CodingNormToPixel(overlay.Points[3]);

        double rx1 = Math.Abs(corner1.X - c1.X);
        double ry1 = Math.Abs(corner1.Y - c1.Y);
        double rx2 = Math.Abs(corner2.X - c2.X);
        double ry2 = Math.Abs(corner2.Y - c2.Y);

        var colorStart = Color.FromRgb(0, 200, 255);
        var colorEnd = Color.FromRgb(255, 165, 0);

        static Point LerpPoint(Point a, Point b, double t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        static double Lerp(double a, double b, double t)
            => a + (b - a) * t;
        static Color LerpColor(Color a, Color b, double t)
            => Color.FromRgb(
                (byte)Math.Clamp((int)Math.Round(a.R + (b.R - a.R) * t), 0, 255),
                (byte)Math.Clamp((int)Math.Round(a.G + (b.G - a.G) * t), 0, 255),
                (byte)Math.Clamp((int)Math.Round(a.B + (b.B - a.B) * t), 0, 255));

        double axisDx = c2.X - c1.X;
        double axisDy = c2.Y - c1.Y;
        double axisLen = Math.Sqrt(axisDx * axisDx + axisDy * axisDy);
        int ringCount = Math.Clamp((int)Math.Round(axisLen / 30.0), 4, 12);

        var spine = new System.Windows.Shapes.Line
        {
            X1 = c1.X,
            Y1 = c1.Y,
            X2 = c2.X,
            Y2 = c2.Y,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        CodingOverlayCanvas.Children.Add(spine);

        if (axisLen > 0.5)
        {
            double nx = -axisDy / axisLen;
            double ny = axisDx / axisLen;
            double off1 = Math.Max(2.0, Math.Min(rx1, ry1) * 0.55);
            double off2 = Math.Max(2.0, Math.Min(rx2, ry2) * 0.55);

            var leftRail = new System.Windows.Shapes.Line
            {
                X1 = c1.X + nx * off1,
                Y1 = c1.Y + ny * off1,
                X2 = c2.X + nx * off2,
                Y2 = c2.Y + ny * off2,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Effect = glowEffect,
                Tag = tag
            };
            var rightRail = new System.Windows.Shapes.Line
            {
                X1 = c1.X - nx * off1,
                Y1 = c1.Y - ny * off1,
                X2 = c2.X - nx * off2,
                Y2 = c2.Y - ny * off2,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Effect = glowEffect,
                Tag = tag
            };
            CodingOverlayCanvas.Children.Add(leftRail);
            CodingOverlayCanvas.Children.Add(rightRail);
        }

        for (int i = 0; i <= ringCount; i++)
        {
            double t = ringCount == 0 ? 0 : i / (double)ringCount;
            var center = LerpPoint(c1, c2, t);
            double ringRx = Math.Max(2.0, Lerp(rx1, rx2, t));
            double ringRy = Math.Max(2.0, Lerp(ry1, ry2, t));
            var ringColor = LerpColor(colorStart, colorEnd, t);

            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = ringRx * 2,
                Height = ringRy * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(240, ringColor.R, ringColor.G, ringColor.B)),
                StrokeThickness = i == 0 || i == ringCount ? 2.6 : 1.8,
                Fill = new SolidColorBrush(Color.FromArgb(24, ringColor.R, ringColor.G, ringColor.B)),
                Effect = glowEffect,
                Tag = tag
            };
            if (isPreview && i % 2 == 1)
                ring.StrokeDashArray = new DoubleCollection { 4, 2 };

            Canvas.SetLeft(ring, center.X - ringRx);
            Canvas.SetTop(ring, center.Y - ringRy);
            CodingOverlayCanvas.Children.Add(ring);
        }

        if (overlay.ArcDegrees.HasValue)
        {
            var mid = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
            AddSchemaLabel(mid, $"{overlay.ArcDegrees:F0}°", Brushes.White, glowEffect);
        }
    }

    private void RenderPipeBendOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        // Farbe: Gold fuer Vorschau, Orange-Gold finalisiert
        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (overlay.Points.Count == 2)
        {
            // Teilvorschau: nur Linie P1 → P2
            var a = CodingNormToPixel(overlay.Points[0]);
            var b = CodingNormToPixel(overlay.Points[1]);
            var line = new System.Windows.Shapes.Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = stroke, StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(line);

            // Punkt-Markierungen
            AddDotMarker(a, 6, stroke, tag, glowEffect);
            AddDotMarker(b, 6, stroke, tag, glowEffect);
            return;
        }

        if (overlay.Points.Count < 3) return;

        var p1 = CodingNormToPixel(overlay.Points[0]);
        var vertex = CodingNormToPixel(overlay.Points[1]);
        var p3 = CodingNormToPixel(overlay.Points[2]);

        // Linie 1: P1 → Vertex
        var line1 = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = vertex.X, Y2 = vertex.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line1.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line1);

        // Linie 2: Vertex → P3
        var line2 = new System.Windows.Shapes.Line
        {
            X1 = vertex.X, Y1 = vertex.Y, X2 = p3.X, Y2 = p3.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line2.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line2);

        // Punkt-Markierungen an allen 3 Punkten
        AddDotMarker(p1, 6, stroke, tag, glowEffect);
        AddDotMarker(vertex, 8, stroke, tag, glowEffect);
        AddDotMarker(p3, 6, stroke, tag, glowEffect);

        // Winkelbogen am Vertex (kleiner Bogen, Radius ~30px)
        double arcRadius = 30;
        double angle1 = Math.Atan2(p1.Y - vertex.Y, p1.X - vertex.X);
        double angle2 = Math.Atan2(p3.Y - vertex.Y, p3.X - vertex.X);

        // Bogen von angle1 nach angle2 (kuerzerer Weg)
        double angleDiff = angle2 - angle1;
        if (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
        if (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;

        var arcStart = new Point(
            vertex.X + arcRadius * Math.Cos(angle1),
            vertex.Y + arcRadius * Math.Sin(angle1));
        var arcEnd = new Point(
            vertex.X + arcRadius * Math.Cos(angle2),
            vertex.Y + arcRadius * Math.Sin(angle2));

        var arcFigure = new System.Windows.Media.PathFigure { StartPoint = arcStart, IsClosed = false };
        arcFigure.Segments.Add(new System.Windows.Media.ArcSegment(
            arcEnd,
            new Size(arcRadius, arcRadius),
            0,
            Math.Abs(angleDiff) > Math.PI,
            angleDiff > 0 ? System.Windows.Media.SweepDirection.Clockwise : System.Windows.Media.SweepDirection.Counterclockwise,
            true));

        var arcGeo = new System.Windows.Media.PathGeometry();
        arcGeo.Figures.Add(arcFigure);
        var arcPath = new System.Windows.Shapes.Path
        {
            Data = arcGeo, Stroke = stroke, StrokeThickness = 2,
            Effect = glowEffect, Tag = tag
        };
        CodingOverlayCanvas.Children.Add(arcPath);

        // Grad-Label am Vertex
        string angleText = overlay.ArcDegrees.HasValue
            ? $"{overlay.ArcDegrees.Value:F1}\u00B0"
            : "";
        if (!string.IsNullOrEmpty(angleText))
        {
            var lbl = new TextBlock
            {
                Text = angleText,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = stroke,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(6, 3, 6, 3),
                Effect = glowEffect,
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(lbl, vertex.X + 14);
            Canvas.SetTop(lbl, vertex.Y - 24);
            CodingOverlayCanvas.Children.Add(lbl);
        }
    }

    // --- DN-Kreis: Kreis + DN-Label ---

    private void RenderLateralCircleOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

        // Farbe: Hot Pink Vorschau, Magenta finalisiert
        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0xB4))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF));
        var fill = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0x00, 0xFF));

        var center = CodingNormToPixel(overlay.Points[0]);
        var edge = CodingNormToPixel(overlay.Points[1]);
        double radius = Math.Sqrt(Math.Pow(edge.X - center.X, 2) + Math.Pow(edge.Y - center.Y, 2));

        if (radius < 3) return;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Stroke = stroke, StrokeThickness = 2.5,
            Fill = fill, Effect = glowEffect, Tag = tag
        };
        if (isPreview) circle.StrokeDashArray = new DoubleCollection { 4, 2 };
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
        CodingOverlayCanvas.Children.Add(circle);

        // Mittelpunkt-Markierung
        AddDotMarker(center, 5, stroke, tag, glowEffect);

        // Radius-Linie
        var radLine = new System.Windows.Shapes.Line
        {
            X1 = center.X, Y1 = center.Y, X2 = edge.X, Y2 = edge.Y,
            Stroke = stroke, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Effect = glowEffect, Tag = tag
        };
        CodingOverlayCanvas.Children.Add(radLine);

        // DN-Label
        var parts = new List<string>();
        if (overlay.Q1Mm.HasValue)
            parts.Add($"DN {overlay.Q1Mm.Value:F0}");
        if (overlay.DnRatioPercent.HasValue)
            parts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");

        if (parts.Count > 0)
        {
            var lbl = new TextBlock
            {
                Text = string.Join(" ", parts),
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = stroke,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(6, 3, 6, 3),
                Effect = glowEffect,
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(lbl, center.X + radius + 8);
            Canvas.SetTop(lbl, center.Y - 12);
            CodingOverlayCanvas.Children.Add(lbl);
        }
    }

    // --- Lineal: Linie + senkrechte Tick-Marks + mm-Werte ---

    private void RenderRulerOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

        var stroke = Brushes.White;
        var p1 = CodingNormToPixel(overlay.Points[0]);
        var p2 = CodingNormToPixel(overlay.Points[1]);

        // Hauptlinie
        var mainLine = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
            Stroke = stroke, StrokeThickness = 2.5,
            Effect = glowEffect, Tag = tag
        };
        if (isPreview) mainLine.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(mainLine);

        // Tick-Marks berechnen
        double totalMm = overlay.Q1Mm ?? 0;
        if (totalMm <= 0) return;

        double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
        double lineLen = Math.Sqrt(dx * dx + dy * dy);
        if (lineLen < 10) return;

        // Senkrechte Richtung
        double normX = -dy / lineLen, normY = dx / lineLen;

        // Adaptive Tick-Teilung
        double tickInterval;
        if (totalMm > 500) tickInterval = 100;
        else if (totalMm > 200) tickInterval = 50;
        else if (totalMm > 50) tickInterval = 10;
        else tickInterval = 5;

        int tickCount = (int)(totalMm / tickInterval);
        for (int i = 0; i <= tickCount; i++)
        {
            double t = (i * tickInterval) / totalMm;
            if (t > 1.0) break;
            double tx = p1.X + dx * t;
            double ty = p1.Y + dy * t;

            // Grosse Ticks alle 5 Intervalle, sonst kleine
            bool isMajor = (i % 5 == 0);
            double tickLen = isMajor ? 10 : 5;

            var tick = new System.Windows.Shapes.Line
            {
                X1 = tx - normX * tickLen,
                Y1 = ty - normY * tickLen,
                X2 = tx + normX * tickLen,
                Y2 = ty + normY * tickLen,
                Stroke = stroke, StrokeThickness = isMajor ? 1.5 : 1,
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(tick);

            // Beschriftung bei grossen Ticks
            if (isMajor && i > 0)
            {
                var tickLbl = new TextBlock
                {
                    Text = $"{(int)(i * tickInterval)}",
                    FontSize = 9, Foreground = stroke,
                    Tag = tag
                };
                Canvas.SetLeft(tickLbl, tx + normX * 14 - 8);
                Canvas.SetTop(tickLbl, ty + normY * 14 - 6);
                CodingOverlayCanvas.Children.Add(tickLbl);
            }
        }

        // End-Ticks an Start und Ende
        foreach (var pt in new[] { p1, p2 })
        {
            var endTick = new System.Windows.Shapes.Line
            {
                X1 = pt.X - normX * 12, Y1 = pt.Y - normY * 12,
                X2 = pt.X + normX * 12, Y2 = pt.Y + normY * 12,
                Stroke = stroke, StrokeThickness = 2,
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(endTick);
        }

        // Gesamtlaenge-Label
        var anchorPt = labelAnchor != null ? CodingNormToPixel(labelAnchor) : new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        var totalLbl = new TextBlock
        {
            Text = $"{totalMm:F1} mm",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = stroke,
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = glowEffect,
            Tag = isPreview ? "overlay_measure" : "overlay_manual"
        };
        Canvas.SetLeft(totalLbl, anchorPt.X + 12);
        Canvas.SetTop(totalLbl, anchorPt.Y - 20);
        CodingOverlayCanvas.Children.Add(totalLbl);
    }

    // --- Referenz-DN: Gestrichelter Kreis am kalibrierten Rohrdurchmesser ---

    private void RenderReferenceDn()
    {
        // Bestehende Referenz-DN-Elemente entfernen
        var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == "ref_dn")
            .ToList();
        foreach (var el in old) CodingOverlayCanvas.Children.Remove(el);

        if (!_showReferenceDn || _codingOverlayService?.Calibration == null) return;
        var cal = _codingOverlayService.Calibration;
        if (!cal.IsCalibrated || cal.NormalizedDiameter <= 0) return;

        double w = CodingOverlayCanvas.ActualWidth, h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var center = CodingNormToPixel(cal.PipeCenter);
        // Radius: halber normierter Durchmesser, skaliert auf Canvas-Breite
        double radiusPxX = (cal.NormalizedDiameter / 2.0) * w;
        double radiusPxY = (cal.NormalizedDiameter / 2.0) * h;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = radiusPxX * 2, Height = radiusPxY * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = "ref_dn"
        };
        Canvas.SetLeft(circle, center.X - radiusPxX);
        Canvas.SetTop(circle, center.Y - radiusPxY);
        CodingOverlayCanvas.Children.Add(circle);

        // Label
        var lbl = new TextBlock
        {
            Text = $"Ref: DN {cal.NominalDiameterMm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            Tag = "ref_dn"
        };
        Canvas.SetLeft(lbl, center.X + radiusPxX + 4);
        Canvas.SetTop(lbl, center.Y - 8);
        CodingOverlayCanvas.Children.Add(lbl);
    }

    // --- Hilfsmethode: Punkt-Markierung ---

    private void AddDotMarker(Point pos, double radius, Brush fill, string tag,
        System.Windows.Media.Effects.DropShadowEffect effect)
    {
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Fill = fill, Stroke = Brushes.White, StrokeThickness = 1.5,
            Effect = effect, Tag = tag
        };
        Canvas.SetLeft(dot, pos.X - radius);
        Canvas.SetTop(dot, pos.Y - radius);
        CodingOverlayCanvas.Children.Add(dot);
    }

    private static string BuildOverlayMeasurementText(OverlayGeometry overlay)
    {
        // Werkzeug-spezifische Texte
        if (overlay.ToolType is OverlayToolType.PipeBend or OverlayToolType.PipeDirection && overlay.ArcDegrees.HasValue)
            return $"Winkel: {overlay.ArcDegrees.Value:F1}\u00B0";

        if (overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue)
        {
            var mode = overlay.Points.Count >= 3
                ? "Einragung"
                : overlay.LevelSubMode switch
                {
                    LevelMode.Water => "Wasser",
                    LevelMode.Obstacle => "Hindernis",
                    _ => "Sediment"
                };
            return $"{mode}: {overlay.FillPercent.Value:F1}%";
        }

        if (overlay.ToolType == OverlayToolType.LateralCircle)
        {
            var dnParts = new List<string>();
            if (overlay.Q1Mm.HasValue) dnParts.Add($"DN {overlay.Q1Mm.Value:F0}");
            if (overlay.DnRatioPercent.HasValue) dnParts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");
            return string.Join(" ", dnParts);
        }

        if (overlay.ToolType == OverlayToolType.Ruler && overlay.Q1Mm.HasValue)
            return $"Laenge: {overlay.Q1Mm.Value:F1} mm";

        if (overlay.ToolType is OverlayToolType.Ellipse or OverlayToolType.Freehand or OverlayToolType.CrossSection && overlay.FillPercent.HasValue)
            return $"Querschnitt: {overlay.FillPercent.Value:F1}%";

        // Standard-Text fuer bestehende Werkzeuge
        var parts = new List<string>();

        if (overlay.Q1Mm.HasValue)
            parts.Add($"Q1:{overlay.Q1Mm.Value:F0}mm");
        if (overlay.Q2Mm.HasValue)
            parts.Add($"Q2:{overlay.Q2Mm.Value:F0}mm");
        if (overlay.ClockFrom.HasValue)
        {
            parts.Add(overlay.ClockTo.HasValue
                ? $"Uhr:{overlay.ClockFrom.Value:F1}->{overlay.ClockTo.Value:F1}"
                : $"Uhr:{overlay.ClockFrom.Value:F1}");
        }
        if (overlay.ArcDegrees.HasValue)
            parts.Add($"Bogen:{overlay.ArcDegrees.Value:F0}deg");

        return string.Join("  ", parts);
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
        if (_serviceProvider == null || _haltungRecord == null) return;

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
            if (!string.IsNullOrWhiteSpace(_serviceProvider.Settings.LastProjectPath))
                projectRoot = Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath) ?? "";

            // Logo suchen
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                IncludePhotos = true,
                IncludeHaltungsgrafik = true,
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
            var pdf = _serviceProvider.ProtocolPdfExporter.BuildHaltungsprotokollPdf(
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
        var projectFolder = !string.IsNullOrEmpty(_serviceProvider?.Settings.LastProjectPath)
            ? Path.GetDirectoryName(_serviceProvider!.Settings.LastProjectPath) ?? ""
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

            var maskCode = Ai.VsaCodeResolver.InferCodeFromLabel(masks[i].Label)?.ToUpperInvariant() ?? "";
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
        var imagesDir = Ai.Teacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        // 4. Lehrer-Annotation erstellen
        var annotation = new Ai.Teacher.TeacherAnnotation
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = Domain.Models.OverlayToolType.None,
            FullFramePath = destFrame,
        };

        await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

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
        if (_haltungRecord == null || _serviceProvider == null) return;

        var result = MessageBox.Show(
            $"{doc.Current.Entries.Count} Beobachtungen protokolliert.\n\n" +
            "Protokoll jetzt anzeigen und bearbeiten?\n" +
            "(Aenderungen werden in Primaere Schaeden uebernommen)",
            "Codier-Session abgeschlossen",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
        if (project == null) return;

        var projectFolder = !string.IsNullOrWhiteSpace(_serviceProvider.Settings.LastProjectPath)
            ? Path.GetDirectoryName(_serviceProvider.Settings.LastProjectPath)
            : null;

        var dlg = new Views.ProtocolObservationsWindow(
            _haltungRecord, project, _serviceProvider, _videoPath, projectFolder,
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

    // --- Coding KI-Analyse ---

    private async void InitCodingAi()
    {
        try
        {
            var config = AiRuntimeConfig.Load();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Kuenstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = config.CreateOllamaClient();
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel, config.ReferenceVisionModel);
            _codingQualityGate = new QualityGateService();

            // Multi-Model Pipeline (YOLO → DINO → SAM) initialisieren
            try
            {
                var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://localhost:8100";
                _codingVisionClient = new Ai.Pipeline.VisionPipelineClient(new Uri(sidecarUrl));
                var health = await _codingVisionClient.HealthCheckAsync();
                if (health != null)
                {
                    _codingMultiModel = new Ai.Pipeline.SingleFrameMultiModelService(_codingVisionClient);
                    // Codier-Modus: Direkt Qwen, Sidecar nur fuer SAM-Nachsegmentierung
                    SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{CompactModelName(_codingAiModelName)} + SAM-Segmentierung");
                }
                else
                {
                    SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{CompactModelName(_codingAiModelName)} (ohne SAM)");
                }
            }
            catch
            {
                SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_codingAiModelName)} (ohne SAM)");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(_codingAiModelName));

            // Few-Shot-Beispiele laden — ohne diese findet die KI drastisch weniger (Audit-Fix)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_codingEnhancedVision == null) return;
                    var store = new Ai.Training.FewShotExampleStore();
                    await _codingEnhancedVision.EnableFewShotAsync(store);
                    var fsDiag = _codingEnhancedVision.FewShotDiagnostics ?? "keine";
                    Dispatcher.Invoke(() =>
                        SetCodingAiState("Kuenstliche Intelligenz bereit (Few-Shot)", Color.FromRgb(0x22, 0xC5, 0x5E),
                            $"{CompactModelName(_codingAiModelName)} | {fsDiag}"));
                }
                catch (Exception fex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Player] Few-Shot laden fehlgeschlagen: {fex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

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
            _ => Ai.VsaCodeResolver.InferCodeFromLabel(keyword)
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

    private async void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunCodingAnalysisAsync("Aktuellen Frame analysieren...", disableAnalyzeButton: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingAnalyzeFrame_Click error: {ex.Message}");
        }
    }

    private async Task RunCodingAnalysisAsync(string activityText, bool disableAnalyzeButton = false,
        string? keywordHint = null, string? codeHint = null)
    {
        if ((_codingEnhancedVision == null && _codingLiveDetection == null && _codingMultiModel == null)
            || _codingIsAnalyzing) return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = false;

            // Zeitstempel VOR dem Capture festhalten (CaptureSnapshotAsync wartet bis zu 1s)
            var captureTimestampSec = _player.Time / 1000.0;

            // ── YOLO-first Live-Analyse: YOLO26l-seg → SAM → optional Qwen-Eskalation ──
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44));
                    return;
                }

                var b64 = Convert.ToBase64String(pngBytes);
                int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;

                // ── Schritt 1: YOLO26l-seg Erkennung (2ms) + Kandidaten-Tracking ──
                LiveDetection? result = null;
                bool yoloHadFindings = false;

                if (_codingVisionClient != null && _codingMultiModel != null)
                {
                    try
                    {
                        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                            "YOLO Detection...", pulse: true);

                        var mmResult = await _codingMultiModel.AnalyzeFrameAsync(
                            pngBytes, dn, _codingOverlayService?.Calibration,
                            _codingAnalysisCts.Token);

                        if (mmResult.HasDetections && mmResult.SamResponse != null)
                        {
                            int imgW = mmResult.SamResponse.ImageWidth;
                            int imgH = mmResult.SamResponse.ImageHeight;

                            // Jede Detektion klassifizieren: Nahbereich oder Tiefe?
                            var nearIndices = new HashSet<int>();
                            var depthLabels = new List<string>();

                            for (int i = 0; i < mmResult.DinoDetections.Count; i++)
                            {
                                var d = mmResult.DinoDetections[i];
                                double nArea = ((d.X2 - d.X1) * (d.Y2 - d.Y1)) / (imgW * (double)imgH);
                                double ncx = ((d.X1 + d.X2) / 2.0) / imgW;
                                double ncy = ((d.Y1 + d.Y2) / 2.0) / imgH;

                                // Tiefe-Erkennung: Box-Mittelpunkt nahe Bildmitte = Fluchtpunkt = weit weg
                                // Unabhaengig von Box-Groesse (YOLO liefert grosse Boxen wegen Training)
                                double distFromCenter = Math.Sqrt(Math.Pow(ncx - 0.5, 2) + Math.Pow(ncy - 0.5, 2));
                                // Box beruehrt den Bildrand? → definitiv nah
                                double margin = 0.05;
                                bool touchesEdge = d.X1 / imgW < margin || d.Y1 / imgH < margin
                                               || d.X2 / imgW > (1 - margin) || d.Y2 / imgH > (1 - margin);
                                // Nah = Box beruehrt Rand ODER Mittelpunkt weit von Bildmitte
                                bool isNear = touchesEdge || distFromCenter > 0.25;

                                if (!isNear)
                                {
                                    // In der Tiefe → Kandidat merken (noch nicht protokollieren)
                                    _codingDepthCandidates[d.Label] = (captureTimestampSec, nArea, d.Confidence, d.Label);
                                    depthLabels.Add(d.Label);
                                }
                                else
                                {
                                    // Nahbereich → als Befund akzeptieren
                                    nearIndices.Add(i);

                                    // War das ein vorheriger Kandidat der jetzt nah ist? → bestaetigt!
                                    if (_codingDepthCandidates.Remove(d.Label))
                                        System.Diagnostics.Debug.WriteLine(
                                            $"[Kandidat→Befund] {d.Label} wurde bestaetigt (von Tiefe zu Nah)");
                                }
                            }

                            if (nearIndices.Count > 0)
                            {
                                yoloHadFindings = true;

                                // Nur Nahbereich-Detektionen als Events + SAM-Masken
                                var acceptedIndices = AddMultiModelFindingsAsEvents(mmResult, captureTimestampSec);
                                // Nur nahe Masken rendern
                                var nearAccepted = acceptedIndices != null
                                    ? new HashSet<int>(acceptedIndices.Where(i => nearIndices.Contains(i)))
                                    : nearIndices;
                                ShowMultiModelResults(mmResult, nearAccepted);

                                int nearCount = _currentMmResult?.QuantifiedMasks.Count ?? 0;
                                var depthInfo = depthLabels.Count > 0
                                    ? $" | Tiefe: {string.Join(", ", depthLabels)}"
                                    : "";
                                SetCodingAiState(
                                    $"{nearCount} Befunde (YOLO){depthInfo}",
                                    Color.FromRgb(0x22, 0xC5, 0x5E),
                                    $"YOLO {mmResult.YoloTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms");

                                if (BtnCodingPauseMode.IsChecked == true && nearCount > 0)
                                {
                                    _player?.SetPause(true);
                                    SetCodingAiState(
                                        $"{nearCount} Befunde — pausiert{depthInfo}",
                                        Color.FromRgb(0x38, 0xBD, 0xF8),
                                        "Delete = loeschen | O = OK | Leertaste = weiter");
                                }
                            }
                            else if (depthLabels.Count > 0)
                            {
                                // Nur Tiefen-Kandidaten → anzeigen als Vorschau
                                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                                SetCodingAiState(
                                    $"Vorschau: {string.Join(", ", depthLabels)} (in Tiefe)",
                                    Color.FromRgb(0x94, 0xA3, 0xB8),
                                    "Kamera muss naeher — wird bestaetigt wenn im Nahbereich");
                            }
                            else
                            {
                                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                            }
                        }
                        else
                        {
                            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                        }
                    }
                    catch (Exception yoloEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO-Live] {yoloEx.Message}");
                    }
                }

                // ── Schritt 2: Qwen-Fallback wenn YOLO nichts findet ODER Sidecar offline ──
                if (!yoloHadFindings && _codingEnhancedVision != null)
                {
                    try
                    {
                        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                            $"Qwen-Fallback: {CompactModelName(_codingAiModelName)}", pulse: true);

                        var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                            b64, _codingAnalysisCts.Token);
                        result = Ai.LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);

                        System.Diagnostics.Debug.WriteLine(
                            _codingEnhancedVision.LastRawOutput ?? "[Qwen] keine Rohdaten");

                        ShowCodingAiResults(result);
                    }
                    catch (Exception qwenEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Qwen-Fallback] {qwenEx.Message}");
                        SetCodingAiState("Analyse fehlgeschlagen",
                            Color.FromRgb(0xEF, 0x44, 0x44), qwenEx.Message);
                    }
                }

                // Wenn weder YOLO noch Qwen etwas gefunden haben
                if (!yoloHadFindings && (result == null || result.Findings.Count == 0))
                {
                    SetCodingAiState("Kein Befund erkannt",
                        Color.FromRgb(0x94, 0xA3, 0xB8), "YOLO + Qwen");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
        finally
        {
            _codingIsAnalyzing = false;
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = true;
        }
    }

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
    private Ai.Pipeline.SingleFrameResult? _currentMmResult;
    /// <summary>Ferne Detektionen (innerhalb Rohrkreis) — grau als Vorschau angezeigt.</summary>
    private Ai.Pipeline.SingleFrameResult? _previewMmResult;

    /// <param name="mmResult">Analyse-Ergebnis (ungefiltert).</param>
    /// <param name="acceptedIndices">Masken-Indices die ein Event bekommen haben (null = VSA-Code-Filter).</param>
    private void ShowMultiModelResults(Ai.Pipeline.SingleFrameResult mmResult, HashSet<int>? acceptedIndices = null)
    {
        // Masken aufteilen: akzeptierte (nah) vs. verworfene (fern/ungueltig)
        var validIndices = new List<int>();
        var rejectedIndices = new List<int>();
        for (int i = 0; i < mmResult.QuantifiedMasks.Count; i++)
        {
            bool accepted = acceptedIndices != null
                ? acceptedIndices.Contains(i)
                : Ai.VsaCodeResolver.InferCodeFromLabel(mmResult.QuantifiedMasks[i].Label) != null;

            if (accepted) validIndices.Add(i);
            else rejectedIndices.Add(i);
        }

        // Akzeptierte Masken als Haupt-Ergebnis
        Ai.Pipeline.SingleFrameResult FilterByIndices(List<int> indices)
        {
            var fq = indices.Select(i => mmResult.QuantifiedMasks[i]).ToList();
            var fd = indices.Where(i => i < mmResult.DinoDetections.Count)
                .Select(i => mmResult.DinoDetections[i]).ToList();
            var fs = mmResult.SamResponse != null
                ? new Ai.Pipeline.SamResponse(
                    indices.Where(i => i < mmResult.SamResponse.Masks.Count)
                        .Select(i => mmResult.SamResponse.Masks[i]).ToList(),
                    mmResult.SamResponse.ImageWidth, mmResult.SamResponse.ImageHeight,
                    mmResult.SamResponse.InferenceTimeMs)
                : mmResult.SamResponse;
            return new Ai.Pipeline.SingleFrameResult(
                mmResult.IsRelevant, fd, fs, fq,
                mmResult.YoloTimeMs, mmResult.DinoTimeMs, mmResult.SamTimeMs, mmResult.Error);
        }

        var nearResult = validIndices.Count < mmResult.QuantifiedMasks.Count
            ? FilterByIndices(validIndices) : mmResult;
        _currentMmResult = nearResult;
        _previewMmResult = rejectedIndices.Count > 0 ? FilterByIndices(rejectedIndices) : null;

        // Alte Masken entfernen
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        // SAM-Masken rendern: nahe Befunde farbig + klickbar
        if (nearResult.SamResponse != null && nearResult.QuantifiedMasks.Count > 0)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                nearResult.SamResponse,
                nearResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                onMaskClicked: OnMaskOverlayClicked,
                onMaskDeleted: OnMaskOverlayDeleted);
        }

        // Ferne Befunde (innerhalb Rohrkreis) grau + gedimmt rendern (Vorschau)
        if (_previewMmResult?.SamResponse != null && _previewMmResult.QuantifiedMasks.Count > 0)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                _previewMmResult.SamResponse,
                _previewMmResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                previewMode: true,
                indexOffset: nearResult.QuantifiedMasks.Count);
        }

        // Kalibrierkreis anzeigen
        _showReferenceDn = true;
        RenderReferenceDn();
    }

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
            var vsaCode = Ai.VsaCodeResolver.InferCodeFromLabel(masks[maskIndex].Label);
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
        var vsaCode = Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
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
        var vsaCode = Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
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

    /// <summary>Entfernt ALLE SAM-Masken die zum gegebenen VSA-Code passen (Befundliste → Canvas-Sync).</summary>
    private void RemoveMatchingSamMask(string? vsaCode, double meter)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks || string.IsNullOrEmpty(vsaCode)) return;

        // Alle Masken entfernen die zum gleichen VSA-Code aufloesen
        // (z.B. "hole", "hole seal" → beide BAC)
        for (int i = 0; i < masks.Count; i++)
        {
            var inferredCode = Ai.VsaCodeResolver.InferCodeFromLabel(masks[i].Label);
            if (!string.Equals(inferredCode, vsaCode, StringComparison.OrdinalIgnoreCase)) continue;

            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{i}";
            if (CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(e => tag.Equals(e.Tag as string)))
            {
                Ai.Pipeline.SamMaskRenderer.RemoveMaskGroup(CodingOverlayCanvas, tag);
            }
        }
    }

    /// <summary>Findet den Index der ersten noch sichtbaren Maske auf dem Canvas.</summary>
    private int FindFirstVisibleMaskIndex()
    {
        for (int i = 0; i < (_currentMmResult?.QuantifiedMasks.Count ?? 0); i++)
        {
            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{i}";
            if (CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(e => tag.Equals(e.Tag as string)))
                return i;
        }
        return -1;
    }

    /// <summary>Prueft ob noch sichtbare SAM-Masken auf dem Canvas sind.</summary>
    private bool HasVisibleMasks()
    {
        return CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Any(e => (e.Tag as string)?.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag) == true);
    }

    /// <summary>
    /// Prueft ob eine DINO-Detektion in der Erkennungszone liegt (nah genug fuer zuverlaessige Segmentierung).
    /// Nur aktiv wenn Kalibrierung vorliegt UND Kamera frontal ins Rohr schaut.
    /// Bei abgeschwenkter Kamera oder ohne Kalibrierung → alles akzeptieren.
    /// </summary>
    private bool IsInsideDetectionZone(Ai.Pipeline.DinoDetectionDto? dino, int imgW, int imgH)
    {
        if (dino == null || imgW <= 0 || imgH <= 0) return true;

        // Ohne Kalibrierung: kein Tiefenfilter — alles akzeptieren
        // Bei abgeschwenkter Kamera wuerde ein statischer Kreis falsche Ergebnisse liefern
        var cal = _codingOverlayService?.Calibration;
        if (cal == null || cal.NormalizedDiameter <= 0) return true;

        double centerX = cal.PipeCenter.X;
        double centerY = cal.PipeCenter.Y;
        double pipeRadius = cal.NormalizedDiameter / 2.0;

        // BBox-Mittelpunkt normiert (0..1)
        double cx = ((dino.X1 + dino.X2) / 2.0) / imgW;
        double cy = ((dino.Y1 + dino.Y2) / 2.0) / imgH;

        // Abstand vom Rohrmittelpunkt
        double dx = cx - centerX;
        double dy = cy - centerY;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // AUSSERHALB des Rohrkreises = nah an der Wand = zuverlaessig erkennbar
        return dist > pipeRadius;
    }

    /// <summary>
    /// Berechnet eine SAM-Box aus der Uhrlage (Clock-Position).
    /// Rohrquerschnitt als Kreis: 12=oben, 3=rechts, 6=unten, 9=links.
    /// Gibt eine Box zurueck die ~30% des Bildes im entsprechenden Quadranten abdeckt.
    /// </summary>
    private static (double x1, double y1, double x2, double y2) ClockPositionToBox(
        string? clockStr, int imgW, int imgH)
    {
        // Fallback: obere Haelfte (wo die meisten Schaeden sind)
        if (string.IsNullOrEmpty(clockStr) || !int.TryParse(clockStr.Split(':')[0], out int hour))
            return (imgW * 0.10, imgH * 0.10, imgW * 0.90, imgH * 0.50);

        hour = ((hour % 12) + 12) % 12; // 0-11

        // Rohr-Zentrum = Bildmitte, Radius = 40% der Bildhoehe
        double cx = imgW * 0.5;
        double cy = imgH * 0.5;
        double r = imgH * 0.35;
        double boxSize = imgH * 0.25; // Box-Groesse

        // Winkel: 12 Uhr = -90°, 3 Uhr = 0°, 6 Uhr = 90°, 9 Uhr = 180°
        double angleDeg = (hour * 30.0) - 90.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        // Mittelpunkt der Box auf dem Rohrrand
        double bx = cx + r * Math.Cos(angleRad);
        double by = cy + r * Math.Sin(angleRad);

        // Box um den Punkt
        double x1 = Math.Max(0, bx - boxSize);
        double y1 = Math.Max(0, by - boxSize);
        double x2 = Math.Min(imgW, bx + boxSize);
        double y2 = Math.Min(imgH, by + boxSize);

        return (x1, y1, x2, y2);
    }

    /// <summary>
    /// Prueft ob die aktuelle Haltung ein Kunststoffrohr hat (PE, PVC, PP, GFK).
    /// Kunststoffrohre sind dicht — Infiltration nur bei Begleitschaden moeglich.
    /// </summary>
    private bool IsKunststoffRohr()
    {
        var material = _haltungRecord?.GetFieldValue("Rohrmaterial") ?? "";
        if (string.IsNullOrWhiteSpace(material)) return false;
        var m = material.ToUpperInvariant();
        return m.Contains("PE") || m.Contains("PVC") || m.Contains("PP")
            || m.Contains("GFK") || m.Contains("KUNSTSTOFF") || m.Contains("PLASTIK")
            || m.Contains("POLYETHYL") || m.Contains("POLYPROP") || m.Contains("POLYVINYL")
            || m.Contains("HDPE") || m.Contains("FASERZ");
    }

    /// <summary>
    /// Prueft ob in der Naehe (±2m) ein Strukturschaden (BA-Code) existiert.
    /// BA = Riss, Bruch, Deformation, Versatz, defekte Verbindung.
    /// Wenn ja, ist Infiltration auch bei Kunststoff plausibel.
    /// </summary>
    private bool HasNearbyStructuralDamage(double meter)
    {
        if (_codingVm == null) return false;
        return _codingVm.Events.Any(e =>
        {
            var evCode = e.Entry.Code;
            if (string.IsNullOrEmpty(evCode) || evCode.Length < 2) return false;
            var prefix = evCode[..2].ToUpperInvariant();
            return prefix == "BA" && Math.Abs(e.MeterAtCapture - meter) < 2.0;
        });
    }

    /// <summary>Setzt Video fort nach Pause (wenn Pausenmodus aktiv).</summary>
    private void ResumeAfterPause()
    {
        if (BtnCodingPauseMode.IsChecked == true && _player is not null)
        {
            // 2s Cooldown: Kamera soll sich erst weiterbewegen bevor naechste Analyse
            _codingIsAnalyzing = true;
            _player.SetPause(false);
            SetCodingAiState("Weiter...", Color.FromRgb(0x22, 0xC5, 0x5E),
                "KI-Analyse mit Pause aktiv");
            Task.Run(async () =>
            {
                await Task.Delay(2000);
                _codingIsAnalyzing = false;
            }).SafeFireAndForget("CodingPauseCooldown");
        }
    }

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
    /// Erstellt Events und gibt die Masken-Indices zurueck die tatsaechlich ein Event bekommen haben.
    /// </summary>
    private HashSet<int> AddMultiModelFindingsAsEvents(
        Ai.Pipeline.SingleFrameResult mmResult, double captureTimestampSec)
    {
        var acceptedIndices = new HashSet<int>();
        if (_codingVm == null || _codingSessionService == null) return acceptedIndices;

        double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;

        // BCD wird NICHT mehr automatisch erzeugt — nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        for (int i = 0; i < mmResult.QuantifiedMasks.Count; i++)
        {
            var quant = mmResult.QuantifiedMasks[i];
            var dino = i < mmResult.DinoDetections.Count ? mmResult.DinoDetections[i] : null;

            // Erkennungszone: nur Detektionen AUSSERHALB des Rohrkreises (nah) auswerten
            // Detektionen in der Tiefe (innerhalb Rohrkreis) → grau anzeigen, kein Event
            int imgW = mmResult.SamResponse?.ImageWidth ?? 1;
            int imgH = mmResult.SamResponse?.ImageHeight ?? 1;
            if (!IsInsideDetectionZone(dino, imgW, imgH))
                continue;

            // Gemeinsamer Resolver: DINO-Label → LiveFrameFinding → ResolveFindingCodeForCoding
            // So laeuft der Multi-Model-Pfad durch exakt denselben Code wie Qwen.
            var pseudoFinding = new LiveFrameFinding(
                Label: quant.Label,
                Severity: EstimateSeverityFromQuantification(quant),
                PositionClock: NormalizeClockPosition(quant.ClockPosition),
                ExtentPercent: quant.ExtentPercent,
                VsaCodeHint: null,  // DINO liefert englische Labels, kein VSA-Code
                HeightMm: quant.HeightMm,
                WidthMm: quant.WidthMm,
                IntrusionPercent: quant.IntrusionPercent,
                CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: dino != null ? dino.X1 / (mmResult.SamResponse?.ImageWidth ?? 1) : null,
                BboxY1: dino != null ? dino.Y1 / (mmResult.SamResponse?.ImageHeight ?? 1) : null,
                BboxX2: dino != null ? dino.X2 / (mmResult.SamResponse?.ImageWidth ?? 1) : null,
                BboxY2: dino != null ? dino.Y2 / (mmResult.SamResponse?.ImageHeight ?? 1) : null);

            // Gemeinsamer Resolver (identisch mit Qwen-Pfad)
            var code = ResolveFindingCodeForCoding(pseudoFinding, meter);
            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Kein VSA-Code fuer Label='{quant.Label}' — uebersprungen");
                continue;
            }

            // Sperrliste: vom Benutzer abgelehnte Befunde nicht erneut einfuegen
            if (_rejectedFindings.Contains(MakeRejectionKey(code, meter)))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Gesperrt: {code} @ {meter:F1}m (vom Benutzer geloescht)");
                continue;
            }

            // Kunststoffrohr-Regel: Infiltration (BBF) und Bodeneindringung (BBD) sind
            // bei intakten Kunststoffrohren physikalisch unmoeglich — das Rohr ist dicht.
            // Nur wenn ein Strukturschaden (BA = Riss/Bruch/Versatz) in der Naehe ist,
            // kann Wasser eindringen. Ohne Begleitschaden → Fehlalarm verwerfen.
            if (code.StartsWith("BBF", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("BBD", StringComparison.OrdinalIgnoreCase))
            {
                if (IsKunststoffRohr() && !HasNearbyStructuralDamage(meter))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Multi-Model] Kunststoff-Filter: {code} @ {meter:F1}m verworfen — kein Begleitschaden");
                    continue;
                }
            }

            var officialLabel = LookupVsaLabel(code);

            // BCD/BCE existieren pro Haltung nur EINMAL — Meterstand-unabhaengige Dedup
            // Primaer gegen session.Events pruefen (wird nie gecleared).
            if ((string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
                && (_codingSessionService?.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || _codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = _codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, code) &&
                IsAlreadyCovered(e, meter, pseudoFinding));
            if (coveringEvent != null) continue;

            // QualityGate mit Multi-Model Evidenz
            double dinoConf = dino?.Confidence ?? quant.Confidence;
            var evidence = new EvidenceVector(
                YoloConf: 0.8,
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(dinoConf, TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Multi-Model")!;

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = officialLabel ?? quant.Label,
                MeterStart = meter,
                Zeit = videoTime
            };

            // Messungen in CodeMeta (gleiche Logik wie Qwen-Pfad)
            ApplyQuantificationToEntry(entry, code, quant);

            var codingEvent = _codingSessionService?.AddEvent(entry);
            if (codingEvent is not null)
                codingEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = code,
                    Confidence = gateResult.CompositeConfidence,
                    Reason = $"{quant.Label} (DINO {dinoConf:P0})",
                    Decision = gateResult.IsGreen
                        ? CodingUserDecision.Accepted
                        : CodingUserDecision.Ignored
                };

            acceptedIndices.Add(i);
            anyAdded = true;
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }

        return acceptedIndices;
    }

    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
    {
        if (_codingImportEvents == null || _codingImportEvents.Count == 0)
            return null;

        var context = new List<(string, string, double)>();
        foreach (var evt in _codingImportEvents)
        {
            var entry = evt.Entry;
            var code = entry?.Code;
            if (string.IsNullOrWhiteSpace(code)) continue;
            context.Add((code, entry?.Beschreibung ?? code, evt.MeterAtCapture));
        }

        return context.Count > 0 ? context : null;
    }

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // ── Zustandsautomat: Einblendung vs. echtes Videobild ──
        // Zuerst State aktualisieren, dann pruefen ob Frame analysiert werden darf.
        // Gating BEVOR irgendetwas ins UI geschrieben wird.
        UpdateFrameReadiness(result);

        if (!IsFrameReady())
        {
            // Ergebnis puffern statt verwerfen (Warmup-Phase)
            if (result.Findings.Count > 0)
                _pendingWarmupResult = result;

            SetCodingAiState("Dateneinblendung erkannt \u2014 \u00fcbersprungen",
                Color.FromRgb(0x94, 0xA3, 0xB8),
                $"Warte auf Videobild... (Bild {_codingOsdSkippedFrames} von 3)");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        // Warmup-Puffer nachtraeglich verarbeiten (erste Ready-Transition)
        if (_pendingWarmupResult != null)
        {
            var buffered = _pendingWarmupResult;
            _pendingWarmupResult = null;
            // Bestes gepuffertes Ergebnis verwenden wenn aktuelles leer ist
            if (result.Findings.Count == 0 && buffered.Findings.Count > 0)
                result = buffered;
        }

        // ── Ab hier: Frame ist bereit fuer Analyse ──

        // OSD-Meterstand uebernehmen (Plausibilitaet: nicht rueckwaerts springen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            var newMeter = result.MeterReading.Value;
            var prevMeter = _codingLastOsdMeter ?? 0;

            // Nur vorwaerts aktualisieren (Kamera faehrt nicht rueckwaerts)
            // Ausnahme: erster Meter-Wert (currentMeter == 0) darf immer gesetzt werden
            if (newMeter >= prevMeter || prevMeter == 0)
            {
                _codingLastOsdMeter = newMeter;
                _codingSessionService?.MoveToMeter(newMeter);
                OsdMeterBadge.Visibility = Visibility.Visible;
                TxtOsdMeter.Text = $"{newMeter:F2}m (OSD)";
            }
            else
            {
                // Qwen hat kleineren Meter gelesen → ignorieren (wahrscheinlich OSD-Fehler)
                System.Diagnostics.Debug.WriteLine(
                    $"[OSD] Meter-Ruecksprung ignoriert: {newMeter:F2}m < {prevMeter:F2}m");
            }
        }

        // ── Findings filtern: VSA-Validierung + Deduplizierung ──
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = result.MeterReading ?? (_codingVm?.CurrentMeter ?? 0);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Befund"
                : "Kein Befund";
            SetCodingAiState(noDamageText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = result.MeterReading.HasValue
            ? $"OSD {result.MeterReading.Value:F2}m \u2013 {validFindings.Count} Befund(e)"
            : $"{validFindings.Count} Befund(e)";
        SetCodingAiState(findingsText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = validFindings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Befunde als visuelle Overlays direkt auf dem Videobild anzeigen
        if (validFindings.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(validFindings, _player.Time / 1000.0);
        }

        // Pausenmodus: Video pausieren wenn Befunde erkannt
        if (BtnCodingPauseMode.IsChecked == true && validFindings.Count > 0)
        {
            _player?.SetPause(true);
            SetCodingAiState(
                $"{validFindings.Count} Befunde — pausiert zum Pruefen",
                Color.FromRgb(0x38, 0xBD, 0xF8),
                "Delete = Befund loeschen | Leertaste = weiter");
        }
    }

    /// <summary>
    /// Prueft ob ein neuer Fund bereits durch ein bestehendes Event abgedeckt ist.
    /// Beruecksichtigt: Streckenschaeden (ganzer Bereich), akzeptierte Events,
    /// und Punktschaeden (±0.3m + Position).
    /// </summary>
    private static bool IsAlreadyCovered(CodingEvent existing, double newMeter, LiveFrameFinding newFinding)
    {
        // Einmal-Codes: BCD (Rohranfang), BCE (Rohrende), BDC (Abbruch) duerfen
        // nur 1× pro Session vorkommen — Meter-Distanz ist irrelevant
        var existBaseCode = existing.Entry.Code?.Length >= 3
            ? existing.Entry.Code[..3].ToUpperInvariant() : "";
        if (existBaseCode is "BCD" or "BCE" or "BDC")
            return true; // IMMER Duplikat, egal bei welchem Meter

        // Streckenschaden: der ganze Bereich MeterStart..MeterEnd ist abgedeckt
        if (existing.Entry.IsStreckenschaden)
        {
            var start = existing.Entry.MeterStart ?? existing.MeterAtCapture;
            var end = existing.Entry.MeterEnd ?? double.MaxValue; // offen = bis Ende
            return newMeter >= (start - 0.1) && newMeter <= (end + 0.1);
        }

        // Bereits akzeptiertes/bearbeitetes Event: gleicher Code innerhalb ±1.0m
        // nicht nochmal melden (User hat den Schaden schon gesehen und bestaetigt)
        if (existing.AiContext?.Decision is CodingUserDecision.Accepted
            or CodingUserDecision.AcceptedWithEdit)
        {
            return Math.Abs(existing.MeterAtCapture - newMeter) < 1.0;
        }

        // Punktschaden: gleicher Code innerhalb ±1.0m
        if (Math.Abs(existing.MeterAtCapture - newMeter) >= 1.0)
            return false;

        // BCA (Anschluss) kann mehrfach am gleichen Meter vorkommen (z.B. 3h und 9h)
        // → Position-Check noetig um verschiedene Anschluesse zu unterscheiden
        var baseCode = newFinding.VsaCodeHint?.Length >= 3
            ? newFinding.VsaCodeHint[..3].ToUpperInvariant() : "";
        if (baseCode == "BCA")
            return IsSamePosition(existing, newFinding);

        // Alle anderen Codes: gleicher Meter = Duplikat (kein Position-Check noetig)
        return true;
    }

    /// <summary>
    /// Positionsvergleich fuer Duplikat-Erkennung.
    /// Zwei Befunde mit gleichem Code gelten als gleiche Position wenn:
    /// - Beide BBox haben → Mittelpunktabstand kleiner 15% (normalisiert)
    /// - Keiner BBox hat → gleiche Uhrlage
    /// - Gemischt (BBox vs. ohne) → Uhrlage vergleichen als Fallback.
    ///   Verhindert Duplikate wenn Vision die BBox mal liefert, mal nicht.
    /// </summary>
    private static bool IsSamePosition(CodingEvent existing, LiveFrameFinding newFinding)
    {
        bool newHasBbox = newFinding.BboxX1.HasValue && newFinding.BboxY1.HasValue
                       && newFinding.BboxX2.HasValue && newFinding.BboxY2.HasValue;
        bool existHasBbox = existing.Overlay?.Points?.Count >= 4;

        if (newHasBbox && existHasBbox)
        {
            // Mittelpunkt-Vergleich (normalisierte Koordinaten 0..1)
            var ncx = (newFinding.BboxX1!.Value + newFinding.BboxX2!.Value) / 2;
            var ncy = (newFinding.BboxY1!.Value + newFinding.BboxY2!.Value) / 2;
            var pts = existing.Overlay!.Points;
            var ecx = (pts[0].X + pts[2].X) / 2;
            var ecy = (pts[0].Y + pts[2].Y) / 2;
            var dist = Math.Sqrt(Math.Pow(ncx - ecx, 2) + Math.Pow(ncy - ecy, 2));
            return dist < 0.15;
        }

        // Fallback: Uhrlage vergleichen (auch bei gemischtem BBox-Status).
        // Faengt den Fall ab, dass Vision die BBox mal liefert und mal nicht.
        var existClock = existing.Entry.CodeMeta?.Parameters
            ?.GetValueOrDefault("vsa.uhr.von");
        var newClock = newFinding.PositionClock;

        // Beide haben Uhrlage → vergleichen
        if (!string.IsNullOrEmpty(existClock) && !string.IsNullOrEmpty(newClock))
            return string.Equals(existClock, newClock, StringComparison.OrdinalIgnoreCase);

        // Keine Positionsinfo verfuegbar → konservativ: als gleich werten (Duplikat annehmen)
        return true;
    }

    /// <summary>
    /// Prueft ob zwei VSA-Codes fuer Dedup-Zwecke als gleich gelten.
    /// Exakter Match ODER gleicher 3-Zeichen-Hauptcode (z.B. BCAEB vs BCA).
    /// </summary>
    private static bool CodesMatchForDedup(string? existingCode, string newCode)
    {
        if (string.IsNullOrWhiteSpace(existingCode) || string.IsNullOrWhiteSpace(newCode))
            return false;

        // Exakter Match
        if (string.Equals(existingCode, newCode, StringComparison.OrdinalIgnoreCase))
            return true;

        // Hauptcode-Match: gleicher 3-Zeichen-Prefix = gleiche Schadensgruppe
        if (existingCode.Length >= 3 && newCode.Length >= 3)
            return string.Equals(
                existingCode[..3], newCode[..3], StringComparison.OrdinalIgnoreCase);

        return false;
    }

    /// <summary>
    /// Filtert KI-Findings: VSA-Code-Validierung, BCD/BCE-Ausschluss, Deduplizierung.
    /// Die gefilterte Liste wird fuer UI, Overlays und Event-Erstellung verwendet.
    /// Deduplizierung: code + BBox-Mittelpunkt (verschiedene Positionen = verschiedene Befunde).
    /// </summary>
    /// <summary>
    /// Filtert und normalisiert KI-Findings.
    /// Nach diesem Schritt gilt fuer jedes Finding:
    ///   - VsaCodeHint ist ein gueltiger VSA-Code (validiert) oder das Finding wurde verworfen
    ///   - Keine "???"-Codes, keine ungeprueften Hint-Werte
    /// </summary>
    private IReadOnlyList<LiveFrameFinding> FilterValidFindings(IReadOnlyList<LiveFrameFinding> raw, double currentMeter)
    {
        var filtered = new List<LiveFrameFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in raw)
        {
            // Einzige Code-Aufloesung — ResolveFindingCodeForCoding gibt validen Code oder null
            var code = ResolveFindingCodeForCoding(f, currentMeter);

            // BCD/BCE: Live-Check bei JEDEM Finding (nicht gecacht!).
            // Wichtig weil zwischen Analyse-Start und diesem Punkt der Eingabemarker
            // bereits ein BCD erzeugt haben kann (async Timing).
            if (code != null
                && (string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase)))
            {
                bool alreadyExists =
                    _codingSessionService?.ActiveSession?.Events.Any(e =>
                        string.Equals(e.Entry.Code, code, StringComparison.OrdinalIgnoreCase)) == true
                    || _codingVm?.Events.Any(e =>
                        string.Equals(e.Entry.Code, code, StringComparison.OrdinalIgnoreCase)) == true;
                if (alreadyExists)
                {
                    System.Diagnostics.Debug.WriteLine($"[KI-Filter] {code} uebersprungen (bereits vorhanden, live-check)");
                    continue;
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[KI-Filter] Label='{f.Label}' VsaCodeHint='{f.VsaCodeHint}' → Code='{code ?? "(null)"}'");

            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[KI-Filter] Verworfen: Label='{f.Label}' (kein VSA-Code ableitbar)");
                continue;
            }

            // Sperrliste: vom Benutzer abgelehnte Befunde nicht erneut einfuegen
            if (_rejectedFindings.Contains(MakeRejectionKey(code, currentMeter)))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[KI-Filter] Gesperrt: {code} @ {currentMeter:F1}m (vom Benutzer geloescht)");
                continue;
            }

            // VsaCodeHint konsequent auf den validierten Code setzen.
            // Alte ungueltige Werte werden NICHT beibehalten.
            var normalizedFinding = string.Equals(code, f.VsaCodeHint, StringComparison.OrdinalIgnoreCase)
                ? f
                : f with { VsaCodeHint = code };

            // Deduplizierung: code + raeumliche Position
            string dedupeKey;
            if (normalizedFinding.BboxX1.HasValue && normalizedFinding.BboxY1.HasValue
                && normalizedFinding.BboxX2.HasValue && normalizedFinding.BboxY2.HasValue)
            {
                var cx = Math.Round((normalizedFinding.BboxX1.Value + normalizedFinding.BboxX2.Value) / 2, 1);
                var cy = Math.Round((normalizedFinding.BboxY1.Value + normalizedFinding.BboxY2.Value) / 2, 1);
                dedupeKey = $"{code}@{cx:F1},{cy:F1}";
            }
            else
            {
                dedupeKey = $"{code}@{NormalizeClockPosition(normalizedFinding.PositionClock) ?? "?"}";
            }

            if (!seen.Add(dedupeKey)) continue;

            filtered.Add(normalizedFinding);
        }

        return filtered;
    }

    /// <summary>
    /// Klartext-Lookup fuer einen VSA-Code mit Fallback-Kette:
    /// Voller Code → 3-Zeichen-Hauptcode → 2-Zeichen-Gruppe → null.
    /// </summary>
    // Phase 6.1.C: LookupVsaLabel + ApplyQuantificationToEntry +
    // EstimateSeverityFromQuantification + NormalizeClockPosition
    // nach PlayerWindow.Helpers.cs migriert.

    /// <summary>
    /// Einzige Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Delegiert an VsaCodeResolver (zentrale Utility) + Import-Verfeinerung.
    /// Gibt validen VSA-Code oder null zurueck — nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        // 1. VsaCodeHint normalisieren
        var hinted = Ai.VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint);
        if (hinted != null)
            return RefineGenericCodeFromImport(hinted, currentMeter) ?? hinted;

        // 2. Label-Heuristik
        var coarse = Ai.VsaCodeResolver.InferCodeFromLabel(finding.Label);
        if (coarse != null)
            return RefineGenericCodeFromImport(coarse, currentMeter) ?? coarse;

        // 3. Konservativer Import-Fallback fuer Grundgeruest-Codes am aktuellen Meter
        var importFallback = TryResolveImportFallbackCode(currentMeter);
        if (importFallback != null)
            return importFallback;

        // 4. Kein Code ableitbar
        return null;
    }

    /// <summary>
    private string? RefineGenericCodeFromImport(string genericCode, double currentMeter)
    {
        if (_codingImportEvents.Count == 0 || string.IsNullOrWhiteSpace(genericCode))
            return null;

        var family = genericCode.Trim().ToUpperInvariant();
        var candidate = _codingImportEvents
            .Where(ev =>
                !string.IsNullOrWhiteSpace(ev.Entry?.Code) &&
                ev.Entry.Code.StartsWith(family, StringComparison.OrdinalIgnoreCase))
            .Select(ev => new
            {
                Code = ev.Entry.Code!.Trim().ToUpperInvariant(),
                Distance = Math.Abs(ev.MeterAtCapture - currentMeter)
            })
            .Where(x => x.Distance <= 2.0)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Code.Length)
            .FirstOrDefault();

        return candidate?.Code;
    }

    private string? TryResolveImportFallbackCode(double currentMeter)
    {
        if (_codingImportEvents.Count == 0)
            return null;

        var candidate = _codingImportEvents
            .Where(ev => !string.IsNullOrWhiteSpace(ev.Entry?.Code))
            .Select(ev => new
            {
                Code = ev.Entry!.Code.Trim().ToUpperInvariant(),
                Distance = Math.Abs(ev.MeterAtCapture - currentMeter)
            })
            .Where(x => x.Distance <= 2.0 && IsAllowedImportFallbackCode(x.Code))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Code.Length)
            .FirstOrDefault();

        return candidate?.Code;
    }

    /// <summary>
    /// Erlaubte Code-Familien fuer Import-Fallback.
    /// Umfasst Bestandsaufnahme (BC), Strukturschaeden (BA) und Betriebliche Stoerungen (BB).
    /// </summary>
    // Phase 6.1.A: IsAllowedImportFallbackCode nach PlayerWindow.Helpers.cs migriert.

    /// <summary>
    /// KI-Befunde als CodingEvents eintragen — mit QualityGate-Ampelsystem.
    /// Erwartet bereits gefilterte Findings (aus FilterValidFindings).
    /// </summary>
    private void AddAiFindingsAsEvents(LiveDetection result, IReadOnlyList<LiveFrameFinding> validFindings)
    {
        if (_codingVm == null || _codingSessionService == null) return;

        double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;
        CodingEvent? firstUnsure = null;
        QualityGateResult? firstUnsureGate = null;

        // BCD wird NICHT mehr automatisch erzeugt — nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        if (validFindings.Count == 0)
        {
            if (anyAdded) RefreshCodingEventsList();
            return;
        }

        foreach (var finding in validFindings)
        {
            // FilterValidFindings garantiert: VsaCodeHint ist ein gueltiger VSA-Code.
            // Kein zweiter Inferenzpfad hier — nur uebernehmen.
            string code = finding.VsaCodeHint!;

            // BCD/BCE existieren pro Haltung nur EINMAL — Meterstand-unabhaengige Dedup.
            // Primaer gegen session.Events pruefen (wird nie gecleared, im Gegensatz zu _codingVm.Events).
            if ((string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
                && (_codingSessionService?.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || _codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
            {
                System.Diagnostics.Debug.WriteLine($"[BCD-Dedup] AddFindings: {code} uebersprungen (bereits vorhanden)");
                continue;
            }

            // Klartext aufloesen (voller Code → Hauptcode → Gruppe)
            var officialLabel = LookupVsaLabel(code);

            // Duplikat-Check: gleicher Code (oder gleicher Hauptcode) bereits vorhanden?
            // Hauptcode-Match: BCAEB vs BCA = gleiche Schadensgruppe → Duplikat.
            // 1. Punktschaden: code + meter ±0.3m + gleiche Position
            // 2. Streckenschaden: code faellt in den MeterStart..MeterEnd Bereich
            // 3. Bereits akzeptierter/bearbeiteter Code: nicht nochmal melden
            var coveringEvent = _codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, code) &&
                IsAlreadyCovered(e, meter, finding));
            if (coveringEvent != null)
            {
                // Offener Streckenschaden: letzte Sichtung merken (fuer automatisches Schliessen)
                // MeterEnd bleibt null (= offen) — wird beim Exit via CloseOpenStreckenschaeden gesetzt
                if (coveringEvent.Entry.IsStreckenschaden)
                    coveringEvent.MeterAtCapture = Math.Max(coveringEvent.MeterAtCapture, meter);
                continue;
            }

            // QualityGate: Severity -> EvidenceVector -> Ampel
            var evidence = new EvidenceVector(
                QwenVisionConf: finding.Severity / 5.0,
                PlausibilityScore: 0.6
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(
                    finding.Severity / 5.0,
                    finding.Severity >= 4 ? TrafficLight.Green : TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Fallback")!;

            // officialLabel wurde oben bereits per LookupLabel geholt und validiert

            // Streckenschaden-Erkennung: Codes die typischerweise ueber eine Strecke auftreten
            // (z.B. Wasserrueckstau, Wurzeleinwuchs, Ablagerung, Korrosion)
            bool isStrecke = Services.CodeCatalog.VsaCodeTree.IsStreckenschadenCode(code);

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = officialLabel ?? finding.Label,
                MeterStart = meter,
                IsStreckenschaden = isStrecke,
                // MeterEnd bleibt null (offen) — wird beim naechsten Tick
                // oder beim Exit automatisch geschlossen
                Zeit = videoTime
            };

            if (!string.IsNullOrWhiteSpace(finding.PositionClock))
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.uhr.von"] = finding.PositionClock!;
            }
            if (finding.CrossSectionReductionPercent is > 0)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] = finding.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);
            }
            else if (finding.IntrusionPercent is > 0)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] = finding.IntrusionPercent.Value.ToString(CultureInfo.InvariantCulture);
            }

            // Foto 1: Automatischer Snapshot vom Erkennungsframe
            var fotoPath = CodingCaptureSnapshot(entry);
            if (fotoPath != null)
                entry.FotoPaths.Add(fotoPath);

            var codingEvent = _codingSessionService?.AddEvent(entry);
            if (codingEvent is not null)
                codingEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = code,
                    Confidence = gateResult.CompositeConfidence,
                    Reason = finding.Label,
                    Decision = gateResult.IsGreen
                        ? CodingUserDecision.Accepted
                        : CodingUserDecision.Ignored
                };

            // Bbox → OverlayGeometry (Rectangle) fuer Kontur-Rendering auf CodingOverlayCanvas
            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var x1 = finding.BboxX1.Value;
                var y1 = finding.BboxY1.Value;
                var x2 = finding.BboxX2.Value;
                var y2 = finding.BboxY2.Value;
                if (codingEvent is not null) codingEvent.Overlay = new OverlayGeometry
                {
                    ToolType = OverlayToolType.Rectangle,
                    Points = new List<NormalizedPoint>
                    {
                        new(Math.Min(x1, x2), Math.Min(y1, y2)),
                        new(Math.Max(x1, x2), Math.Min(y1, y2)),
                        new(Math.Max(x1, x2), Math.Max(y1, y2)),
                        new(Math.Min(x1, x2), Math.Max(y1, y2))
                    }
                };
            }

            anyAdded = true;

            if (!gateResult.IsGreen && firstUnsure == null)
            {
                firstUnsure = codingEvent;
                firstUnsureGate = gateResult;
            }
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            RenderAiOverlays();
            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);
            UpdateToolBadge();
        }

        if (firstUnsure != null && firstUnsureGate != null)
            PauseAndAskConfirmation(firstUnsure, firstUnsureGate);
    }

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

    // --- Ampel: Pause + Bestaetigungs-Panel ---

    private void PauseAndAskConfirmation(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        // Video pausieren
        _player.SetPause(true);
        _codingSessionService?.SetWaitingForInput();

        _codingPendingConfirmEvent = codingEvent;
        _codingPendingGateResult = gateResult;

        // Ampel-Farbe setzen
        var ampelColor = gateResult.IsYellow
            ? Color.FromRgb(0xF5, 0x9E, 0x0B)   // Gelb
            : Color.FromRgb(0xEF, 0x44, 0x44);   // Rot
        ConfirmAmpel.Fill = new SolidColorBrush(ampelColor);

        // Globale Ampel aktualisieren
        SetCodingAiState(TxtCodingAiStatus.Text, ampelColor,
            gateResult.IsYellow ? "QualityGate: Gelb" : "QualityGate: Rot");

        // Panel befuellen
        TxtConfirmCode.Text = codingEvent.Entry.Code ?? "???";
        TxtConfirmConfidence.Text = $"({gateResult.CompositeConfidence:P0})";
        TxtConfirmDescription.Text = codingEvent.Entry.Beschreibung ?? codingEvent.AiContext?.Reason ?? "";
        TxtConfirmDetail.Text = gateResult.IsYellow
            ? "KI ist unsicher \u2014 bitte pruefen."
            : "KI hat geringe Sicherheit \u2014 bitte Code korrigieren oder verwerfen.";

        CodingConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent?.AiContext != null)
        {
            _codingPendingConfirmEvent.AiContext.Decision = CodingUserDecision.Accepted;
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent);
        }

        CloseConfirmationAndResume();
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        // VSA-Code-Explorer oeffnen \u2192 User waehlt korrekten Code
        CloseConfirmationPanel();

        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.AcceptedWithEdit;
            // Defect-Detail-Panel oeffnen fuer manuelle Bearbeitung
            LstCodingEvents.SelectedItem = _codingPendingConfirmEvent;
        }

        ResumeAfterConfirmation();
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent != null)
        {
            // Auf Sperrliste → wird bei naechster Analyse nicht erneut erkannt
            _rejectedFindings.Add(MakeRejectionKey(
                _codingPendingConfirmEvent.Entry.Code,
                _codingPendingConfirmEvent.MeterAtCapture));

            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.Rejected;
            // Event entfernen
            _codingSessionService?.RemoveEvent(_codingPendingConfirmEvent.EventId);
            _codingVm?.Events.Remove(_codingPendingConfirmEvent);
            RefreshCodingEventsList();
        }

        CloseConfirmationAndResume();
    }

    private void CloseConfirmationAndResume()
    {
        CloseConfirmationPanel();
        ResumeAfterConfirmation();
    }

    private void CloseConfirmationPanel()
    {
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
    }

    private void ResumeAfterConfirmation()
    {
        // Session wieder auf Running
        if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput)
            _codingSessionService.ResumeSession();

        // Video weiterlaufen lassen (wenn Auto-KI aktiv)
        if (BtnCodingLiveAi.IsChecked == true)
            _player.SetPause(false);

        // Globale Ampel zuruecksetzen
        if (BtnCodingLiveAi.IsChecked == true)
        {
            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {CompactModelName(_codingAiModelName)}");
        }
        else
        {
            SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }
    /// <summary>Werkzeug-Badge oben links auf Canvas anzeigen.</summary>
    private void UpdateToolBadge()
    {
        var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == "tool_badge")
            .ToList();
        foreach (var el in old)
            CodingOverlayCanvas.Children.Remove(el);

        if (_codingOverlayService == null) return;

        string? toolText = _codingOverlayService.ActiveTool switch
        {
            OverlayToolType.Line => "Linie",
            OverlayToolType.Arc => "Bogen",
            OverlayToolType.Rectangle => "Flaeche",
            OverlayToolType.Point => "Punkt",
            OverlayToolType.Stretch => "Strecke",
            OverlayToolType.PipeBend => "Winkel",
            OverlayToolType.PipeDirection => "Bogen-Wurm",
            OverlayToolType.LateralCircle => "Anschluss",
            OverlayToolType.Level => _codingSchemaType switch
            {
                SchemaType.FillLevel when _codingOverlayService.ActiveLevelMode == LevelMode.Water => "Wasser %",
                SchemaType.FillLevel => "Sediment %",
                SchemaType.Intrusion => "Einragung %",
                _ => "Level"
            },
            OverlayToolType.Ruler => "Lineal",
            _ => null
        };

        if (toolText == null) return;

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Tag = "tool_badge",
            Child = new TextBlock
            {
                Text = toolText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF))
            }
        };

        Canvas.SetLeft(badge, 10);
        Canvas.SetTop(badge, 10);
        CodingOverlayCanvas.Children.Add(badge);
    }

    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        // Bestehende KI-Overlays entfernen (Tags beginnen mit "ai_")
        var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s.StartsWith("ai_"))
            .ToList();
        foreach (var el in toRemove)
            CodingOverlayCanvas.Children.Remove(el);

        // User-Wunsch: nach dem Codieren+Speichern eines Befundes sollen nicht
        // alle bisherigen Events erneut auf dem Live-Canvas erscheinen. Frueher
        // hat diese Methode bei jedem RedrawCodingCanvas-Aufruf alle Events
        // aus _codingVm.Events erneut gezeichnet — das war als "Sammeluebersicht"
        // gedacht, fuehrte aber zu staendigem Wieder-Erscheinen alter Befunde.
        // Die codierten Events bleiben in der Befundliste/DataGrid sichtbar.
        // Das aktuell aktive Overlay (CurrentOverlay) wird weiterhin via
        // RedrawCodingCanvas(includeManualOverlay: true) -> RenderOverlayGeometry
        // gerendert und ist davon nicht betroffen.
        return;

#pragma warning disable CS0162 // Sammeluebersicht-Pfad bewusst stillgelegt
        var amber = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        var amberFill = new SolidColorBrush(Color.FromArgb(30, 0xF5, 0x9E, 0x0B));
        var aiGlow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (var ev in _codingVm.Events)
        {
            if (ev.Overlay == null || ev.AiContext == null) continue;
            var geo = ev.Overlay;

            Brush stroke = ev.AiContext.Decision switch
            {
                CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit
                    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                CodingUserDecision.Rejected
                    => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                _ => amber
            };

            switch (geo.ToolType)
            {
                case OverlayToolType.Line:
                case OverlayToolType.Stretch:
                    if (geo.Points.Count >= 2)
                    {
                        var line = new System.Windows.Shapes.Line
                        {
                            X1 = geo.Points[0].X * w,
                            Y1 = geo.Points[0].Y * h,
                            X2 = geo.Points[1].X * w,
                            Y2 = geo.Points[1].Y * h,
                            Stroke = stroke,
                            StrokeThickness = 2.5,
                            StrokeDashArray = new DoubleCollection { 5, 3 },
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        CodingOverlayCanvas.Children.Add(line);
                    }
                    break;

                case OverlayToolType.Rectangle:
                    if (geo.Points.Count >= 4)
                    {
                        double rx = geo.Points[0].X * w;
                        double ry = geo.Points[0].Y * h;
                        double rw = (geo.Points[2].X - geo.Points[0].X) * w;
                        double rh = (geo.Points[2].Y - geo.Points[0].Y) * h;
                        var rectLeft = Math.Min(rx, rx + rw);
                        var rectTop = Math.Min(ry, ry + rh);
                        var rectAbsW = Math.Abs(rw);
                        var rectAbsH = Math.Abs(rh);

                        // Farbige Kontur mit halbtransparenter Fuellung
                        var strokeColor = (stroke as SolidColorBrush)?.Color ?? Color.FromRgb(0xF5, 0x9E, 0x0B);
                        var rect = new Rectangle
                        {
                            Width = rectAbsW,
                            Height = rectAbsH,
                            Stroke = stroke,
                            StrokeThickness = 3,
                            Fill = new SolidColorBrush(Color.FromArgb(30, strokeColor.R, strokeColor.G, strokeColor.B)),
                            RadiusX = 6,
                            RadiusY = 6,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(rect, rectLeft);
                        Canvas.SetTop(rect, rectTop);
                        CodingOverlayCanvas.Children.Add(rect);

                        // Label-Badge: Code [Konfidenz%]
                        var codeStr = string.IsNullOrWhiteSpace(ev.Entry.Code) ? "?" : ev.Entry.Code;
                        var confPct = ev.AiContext != null ? $" [{ev.AiContext.Confidence * 100:F1}%]" : "";
                        var labelBorder = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(210, strokeColor.R, strokeColor.G, strokeColor.B)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Tag = "ai_overlay",
                            Effect = aiGlow,
                            IsHitTestVisible = false,
                            Child = new TextBlock
                            {
                                Text = $"{codeStr}{confPct}",
                                FontSize = 12,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White
                            }
                        };
                        labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var lx = Math.Clamp(rectLeft, 2, w - labelBorder.DesiredSize.Width - 2);
                        var ly = Math.Clamp(rectTop - labelBorder.DesiredSize.Height - 4, 2, h - labelBorder.DesiredSize.Height - 2);
                        Canvas.SetLeft(labelBorder, lx);
                        Canvas.SetTop(labelBorder, ly);
                        CodingOverlayCanvas.Children.Add(labelBorder);
                    }
                    break;

                case OverlayToolType.Point:
                    if (geo.Points.Count >= 1)
                    {
                        double px = geo.Points[0].X * w;
                        double py = geo.Points[0].Y * h;
                        var dot = new System.Windows.Shapes.Ellipse
                        {
                            Width = 14,
                            Height = 14,
                            Fill = stroke,
                            Opacity = 0.8,
                            Stroke = Brushes.White,
                            StrokeThickness = 1.5,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(dot, px - 7);
                        Canvas.SetTop(dot, py - 7);
                        CodingOverlayCanvas.Children.Add(dot);
                    }
                    break;

                case OverlayToolType.Arc:
                    if (geo.Points.Count >= 2)
                    {
                        var arc = CreateArcPath(geo.Points[0], geo.Points[1], stroke, aiGlow, "ai_overlay", dashed: true);
                        if (arc != null)
                            CodingOverlayCanvas.Children.Add(arc);
                    }
                    break;

                case OverlayToolType.PipeBend:
                    RenderPipeBendOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.PipeDirection:
                    RenderPipeDirectionOverlay(geo, true, aiGlow, "ai_overlay");
                    break;

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Level:
                case OverlayToolType.CrossSection:
                    RenderLevelOverlay(geo, true, aiGlow, "ai_overlay");
                    break;

                case OverlayToolType.Ellipse:
                    if (geo.Points.Count >= 2)
                    {
                        // Punkte: Zentrum + Radius-Punkt
                        double ecx = geo.Points[0].X * w;
                        double ecy = geo.Points[0].Y * h;
                        double erx = Math.Abs(geo.Points[1].X - geo.Points[0].X) * w;
                        double ery = Math.Abs(geo.Points[1].Y - geo.Points[0].Y) * h;
                        if (erx > 0 && ery > 0)
                        {
                            var ellipse = new System.Windows.Shapes.Ellipse
                            {
                                Width = erx * 2,
                                Height = ery * 2,
                                Stroke = stroke,
                                StrokeThickness = 2.5,
                                StrokeDashArray = new DoubleCollection { 5, 3 },
                                Tag = "ai_overlay",
                                Effect = aiGlow
                            };
                            Canvas.SetLeft(ellipse, ecx - erx);
                            Canvas.SetTop(ellipse, ecy - ery);
                            CodingOverlayCanvas.Children.Add(ellipse);
                        }
                    }
                    break;

                case OverlayToolType.Freehand:
                    if (geo.Points.Count >= 2)
                    {
                        var polyline = new System.Windows.Shapes.Polyline
                        {
                            Stroke = stroke,
                            StrokeThickness = 2.5,
                            StrokeDashArray = new DoubleCollection { 5, 3 },
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        foreach (var pt in geo.Points)
                            polyline.Points.Add(new System.Windows.Point(pt.X * w, pt.Y * h));
                        CodingOverlayCanvas.Children.Add(polyline);
                    }
                    break;
            }
        }
#pragma warning restore CS0162
    }

    // --- Dateneinblendung-Erkennung: Zustandsautomat ---
    //
    // WaitingForVideo: Dateneinblendung wird vermutet, Analyse blockiert.
    // Warmup:          Erster Meter gesehen, warte auf Bestaetigung (2. Frame).
    // Ready:           Analyse freigeschaltet, kein weiteres Gating.
    //
    private enum FrameReadiness { WaitingForVideo, Warmup, Ready }
    private FrameReadiness _codingFrameState = FrameReadiness.WaitingForVideo;
    private int _codingOsdSkippedFrames;
    private int _codingMeterConfirmCount;

    // Warmup-Puffer: Ergebnis aus der Warmup-Phase wird zwischengespeichert
    // und nach Transition zu Ready nachtraeglich verarbeitet.
    private LiveDetection? _pendingWarmupResult;

    /// <summary>Setzt den Einblendungs-Zustand zurueck (bei Eintritt/Austritt Codier-Modus).</summary>
    private void ResetFrameReadiness()
    {
        _codingFrameState = FrameReadiness.WaitingForVideo;
        _codingOsdSkippedFrames = 0;
        _codingMeterConfirmCount = 0;
        _codingLastOsdMeter = null; // Stale Meter aus vorheriger Session verhindern
        _pendingWarmupResult = null;
    }

    /// <summary>
    /// Reine Bewertung: Ist der aktuelle Frame bereit fuer die Analyse?
    /// Aendert KEINEN Zustand — dafuer ist UpdateFrameReadiness zustaendig.
    /// </summary>
    private bool IsFrameReady() => _codingFrameState == FrameReadiness.Ready;

    /// <summary>
    /// Aktualisiert den Einblendungs-Zustand anhand des aktuellen Analyse-Ergebnisses.
    /// Muss VOR IsFrameReady aufgerufen werden.
    ///
    /// Uebergaenge:
    ///   WaitingForVideo → Warmup:  erster Frame mit Meterstand (aus aktuellem result)
    ///   WaitingForVideo → Ready:   3 Frames ohne Meter (kein OSD vorhanden)
    ///   Warmup          → Ready:   2. Frame mit Meterstand (Bestaetigung)
    ///   Warmup          → Ready:   2 Frames in Warmup ohne zweiten Meter (Fallback gegen Deadlock)
    /// </summary>
    private void UpdateFrameReadiness(LiveDetection result)
    {
        if (_codingFrameState == FrameReadiness.Ready)
            return;

        // NUR den aktuellen Frame-Meter verwenden, NICHT den gecachten _codingLastOsdMeter.
        // Sonst kann ein stale Wert aus vorheriger Navigation die Sperre umgehen.
        bool hasMeterThisFrame = result.MeterReading.HasValue;

        switch (_codingFrameState)
        {
            case FrameReadiness.WaitingForVideo:
                if (hasMeterThisFrame)
                {
                    // Erster Meter gesehen → Warmup (noch nicht sofort freischalten)
                    _codingFrameState = FrameReadiness.Warmup;
                    _codingMeterConfirmCount = 1;
                    _codingOsdSkippedFrames = 0; // Zaehler fuer Warmup-Fallback neu starten
                }
                else
                {
                    // Kein Meter → zaehlen. Nach 3 Frames: kein OSD vorhanden.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 3)
                        _codingFrameState = FrameReadiness.Ready;
                }
                break;

            case FrameReadiness.Warmup:
                if (hasMeterThisFrame)
                    _codingMeterConfirmCount++;

                // 2 Frames mit Meter → sofort Ready (stabiler Uebergang)
                if (_codingMeterConfirmCount >= 2)
                {
                    _codingMeterConfirmCount = 0;
                    _codingFrameState = FrameReadiness.Ready;
                }
                else
                {
                    // Fallback: nach 2 Frames in Warmup (auch ohne zweiten Meter) → Ready.
                    // Verhindert Deadlock bei OCR-Aussetzern nach erstem Meter.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 2)
                    {
                        _codingMeterConfirmCount = 0;
                        _codingFrameState = FrameReadiness.Ready;
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Stellt sicher, dass BCD (Rohranfang) als erster Eintrag existiert.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// </summary>
    private void EnsureRohranfangExists(double currentMeter, TimeSpan currentVideoTime, ref bool anyAdded)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCD bereits vorhanden? Alle moeglichen Quellen pruefen
        var vmBcd = _codingVm.Events.Count(e => string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        var sessBcd = _codingSessionService.ActiveSession?.Events.Count(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (vmBcd > 0 || sessBcd > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={vmBcd}, Session={sessBcd})");
            return;
        }
        System.Diagnostics.Debug.WriteLine(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {currentMeter:F2}m (VM={vmBcd}, Session={sessBcd})");

        // Rohranfang: OSD-Meter vom Import uebernehmen, sonst 0.00m
        // Videozeit: aus dem Import oder Anfang des Videos
        double rohranfangMeter = 0.0;
        var rohranfangTime = TimeSpan.Zero;

        // Aus Import-Referenz den BCD-Eintrag holen (falls vorhanden)
        var importBcd = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCD", StringComparison.OrdinalIgnoreCase));
        if (importBcd != null)
        {
            rohranfangMeter = importBcd.MeterAtCapture;
            rohranfangTime = importBcd.VideoTimestamp;
        }

        var label = Services.CodeCatalog.VsaCodeTree.LookupLabel("BCD") ?? "Rohranfang";
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = "BCD",
            Beschreibung = label,
            MeterStart = rohranfangMeter,
            Zeit = rohranfangTime
        };
        var ev = _codingSessionService.AddEvent(entry);
        ev.MeterAtCapture = rohranfangMeter;
        ev.VideoTimestamp = rohranfangTime;
        ev.AiContext = new CodingEventAiContext
        {
            SuggestedCode = "BCD",
            Confidence = 1.0,
            Reason = "Rohranfang (automatisch)",
            Decision = CodingUserDecision.Accepted
        };
        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites _codingVm.Events.Add() — sonst doppelt!
        anyAdded = true;

        // Auto-Kalibrierung bei Rohranfang versuchen (wenn noch nicht kalibriert)
        TryAutoCalibrationFromCurrentFrame();
    }

    /// <summary>
    /// Versucht eine Auto-Kalibrierung des Rohrdurchmessers aus dem aktuellen Video-Frame.
    /// Erkennt Rohrinnenwand-Kanten per Helligkeitsgradienten.
    /// </summary>
    private async void TryAutoCalibrationFromCurrentFrame()
    {
        // Nur wenn noch nicht kalibriert
        if (_codingOverlayService?.IsCalibrated == true) return;

        // DN aus Haltungsdaten
        int nominalDn = 300; // Fallback
        if (_haltungRecord?.Fields.TryGetValue("DN_mm", out var dnStr) == true
            && int.TryParse(dnStr, out var dn) && dn > 0)
            nominalDn = dn;

        try
        {
            // Aktuellen Frame capturen (async)
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes == null || frameBytes.Length == 0) return;

            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(frameBytes);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            var autoCalib = Ai.AutoCalibrationService.TryAutoCalibrate(bmp, nominalDn);
            if (autoCalib == null) return;

            _codingOverlayService?.SetCalibration(autoCalib);

            SetCodingAiState(
                $"Auto-Kalibrierung: DN{nominalDn} erkannt ({autoCalib.NormalizedDiameter:P0} der Bildbreite)",
                Color.FromRgb(0x22, 0xC5, 0x5E),
                "Rohrdurchmesser automatisch gemessen");

            System.Diagnostics.Debug.WriteLine(
                $"[AutoCalib] DN{nominalDn}: NormDiam={autoCalib.NormalizedDiameter:F3}, " +
                $"Center=({autoCalib.PipeCenter.X:F3},{autoCalib.PipeCenter.Y:F3}), " +
                $"PixelDiam={autoCalib.PipePixelDiameter:F0}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoCalib] Fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Fuegt BCE (Rohrende) als letzten Eintrag ein.
    /// Meter und Timestamp werden automatisch aus OSD / Video entnommen.
    /// Aufgerufen beim Beenden der Codier-Session oder am Videoende.
    /// </summary>
    private void EnsureRohrendeExists(double meterEnd, TimeSpan videoTime)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        // BCE bereits vorhanden?
        if (_codingVm.Events.Any(e => string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase)))
            return;
        // Streckenschaeden werden bereits in ExitCodingMode geschlossen (vor diesem Aufruf)

        // Rohrende: OSD-Meter bevorzugen, sonst aus Import, sonst EndMeter
        double rohrEndMeter = _codingLastOsdMeter ?? meterEnd;
        var rohrEndTime = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time)
            : videoTime;

        // Aus Import-Referenz den BCE-Eintrag holen (falls vorhanden)
        var importBce = _codingImportEvents.FirstOrDefault(e =>
            string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase));
        if (importBce != null)
        {
            rohrEndMeter = importBce.MeterAtCapture;
            rohrEndTime = importBce.VideoTimestamp;
        }

        var label = Services.CodeCatalog.VsaCodeTree.LookupLabel("BCE") ?? "Rohrende";
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = "BCE",
            Beschreibung = label,
            MeterStart = rohrEndMeter,
            Zeit = rohrEndTime
        };
        var ev = _codingSessionService.AddEvent(entry);
        ev.MeterAtCapture = rohrEndMeter;
        ev.VideoTimestamp = rohrEndTime;
        ev.AiContext = new CodingEventAiContext
        {
            SuggestedCode = "BCE",
            Confidence = 1.0,
            Reason = "Rohrende (automatisch)",
            Decision = CodingUserDecision.Accepted
        };
        RefreshCodingEventsList();
    }

    /// <summary>
    /// Prueft ob offene Streckenschaeden existieren (IsStreckenschaden=true, MeterEnd=null).
    /// Zeigt Dialog mit Liste und bietet an, sie am aktuellen Meter zu schliessen.
    /// Rueckgabe: true = weiter (geschlossen oder ignoriert), false = abgebrochen (User will weiter codieren).
    /// </summary>
    private bool CloseOpenStreckenschaeden(double currentMeter)
    {
        if (_codingVm == null) return true;

        var offene = _codingVm.Events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .ToList();

        if (offene.Count == 0) return true;

        // Hinweis-Dialog mit Liste der offenen Streckenschaeden
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Folgende Streckenschaeden sind noch offen (kein MeterEnde):");
        sb.AppendLine();
        foreach (var ev in offene)
        {
            sb.AppendLine($"  \u2022 {ev.Entry.Code} \u2013 {ev.Entry.Beschreibung}");
            sb.AppendLine($"    Start: {ev.MeterAtCapture:F2}m");
        }
        sb.AppendLine();
        sb.AppendLine($"Sollen alle offenen Streckenschaeden bei {currentMeter:F2}m geschlossen werden?");

        SuspendCodingOverlayInput();
        MessageBoxResult result;
        try
        {
            result = MessageBox.Show(
                sb.ToString(),
                "Offene Streckenschaeden",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (result == MessageBoxResult.Yes)
        {
            // Alle offenen Streckenschaeden schliessen.
            // MeterEnd = letzte Sichtung (MeterAtCapture) oder aktueller Meter
            foreach (var ev in offene)
            {
                var start = ev.Entry.MeterStart ?? 0;
                ev.Entry.MeterEnd = ev.MeterAtCapture > start
                    ? ev.MeterAtCapture
                    : currentMeter;
                _codingSessionService?.UpdateEvent(ev.EventId, ev.Entry, ev.Overlay);
            }
            RefreshCodingEventsList();
            return true;
        }

        if (result == MessageBoxResult.Cancel)
            return false; // User will weiter codieren — Exit abbrechen

        return true; // "Nein" → weiter ohne Schliessen
    }

    /// <summary>
    /// Nach Accept/Reject/Edit: Overlay kurz in Statusfarbe anzeigen, dann ausblenden.
    /// So sieht der User die Bestaetigung, das Bild wird aber danach wieder frei.
    /// </summary>
    private void FadeOutAiOverlayAfterAction()
    {
        // Sofort neu rendern (zeigt gruen/rot je nach Decision)
        RenderAiOverlays();
        // Nach 800ms die KI-Overlays entfernen
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            // Alle ai_overlay-Elemente entfernen
            var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Where(el => el.Tag is string s && s.StartsWith("ai_"))
                .ToList();
            foreach (var el in toRemove)
                CodingOverlayCanvas.Children.Remove(el);
        };
        timer.Start();
    }

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        await RunCodingAnalysisAsync("Analyse: markierte Stelle...");
    }

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

    // --- OSD Meter automatisch lesen beim Navigieren ---

    private double? _codingLastOsdMeter;

    /// <summary>
    /// Liest den OSD-Meterstand vom aktuellen Video-Frame (async, via KI).
    /// Wird bei Codier-Navigation und bei Event-Erstellung aufgerufen.
    /// </summary>
    // OSD-Prompt: NUR Meterstand lesen, keine Analyse (schneller, praeziser)
    private static readonly string OsdMeterPrompt = """
        Kanalinspektion OSD (On-Screen-Display).
        Lies NUR die Meterzahl UNTEN RECHTS im Bild.
        Das ist eine Dezimalzahl wie "0.00", "7.90", "14.98" - die gefahrene Distanz.
        IGNORIERE alle Zahlen im oberen Headertext (Knotennummern wie 74468, 80622 etc.).
        IGNORIERE Datumsangaben und andere Texte.
        Antworte NUR mit der Zahl, z.B.: 7.90
        Falls kein Meterstand lesbar: 0.00
        """;

    private async Task<double?> CodingReadOsdMeterAsync()
    {
        if (_codingLiveDetection == null) return null;

        try
        {
            var tmpDir = Path.GetTempPath();
            var snapFile = Path.Combine(tmpDir, $"sewerstudio_osd_{Guid.NewGuid():N}.png");
            byte[]? pngBytes = null;

            try
            {
                TakeSnapshotSafe(snapFile);
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                        break;
                }
                if (File.Exists(snapFile))
                    pngBytes = await File.ReadAllBytesAsync(snapFile);
            }
            finally
            {
                try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch { }
            }

            if (pngBytes == null || pngBytes.Length == 0) return null;

            // Leichtgewichtiger OSD-Request: nur Meterstand, keine volle Analyse
            var config = AiRuntimeConfig.Load();
            var client = config.CreateOllamaClient();
            var b64 = Convert.ToBase64String(pngBytes);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", OsdMeterPrompt, new[] { b64 })
            };
            var raw = await client.ChatAsync(config.VisionModel, messages, cts.Token);

            // Parse: nur eine Zahl erwartet
            var meterText = raw?.Trim().Replace(",", ".");
            if (!string.IsNullOrWhiteSpace(meterText))
            {
                // Zahl extrahieren (erste Dezimalzahl im Text)
                var match = System.Text.RegularExpressions.Regex.Match(
                    meterText, @"(\d{1,3}(?:\.\d{1,2})?)");
                if (match.Success && double.TryParse(match.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var meter))
                {
                    // Plausibilitaet: 0-500m (Knotennummern sind 5+ stellig)
                    if (meter >= 0 && meter <= 500)
                    {
                        _codingLastOsdMeter = meter;
                        OsdMeterBadge.Visibility = Visibility.Visible;
                        TxtOsdMeter.Text = $"{meter:F2}m (OSD)";
                        return meter;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}














