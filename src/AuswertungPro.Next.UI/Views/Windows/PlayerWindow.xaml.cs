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

    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame als PNG.
    /// Funktioniert mit jeder Aufloesung (auch FullHD 1920x1080).
    /// </summary>
    public static bool TryTakeSnapshot(out string snapshotPath)
    {
        snapshotPath = string.Empty;
        if (_lastOpened?._player is null || !_lastOpened._player.IsPlaying && _lastOpened._player.Time <= 0)
            return false;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SewerStudio_Snapshots");
            Directory.CreateDirectory(tempDir);
            snapshotPath = Path.Combine(tempDir, $"snap_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            // VLC Snapshot: 0 = original Aufloesung (FullHD etc.)
            return _lastOpened.TakeSnapshotSafe(snapshotPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// TakeSnapshot mit kurzem Pause-Trick, um D3D11-Deadlock zu vermeiden.
    /// D3D11 haelt die Video-Surface exklusiv gesperrt; kurzes Pausieren gibt sie frei.
    /// </summary>
    private bool TakeSnapshotSafe(string filePath, uint width = 0, uint height = 0)
    {
        var wasPlaying = _player.IsPlaying;
        if (wasPlaying)
        {
            _player.SetPause(true);
            System.Threading.Thread.Sleep(60);
        }
        // VLC-OSD-Anzeige (Dateipfad) vorher deaktivieren, damit der Pfad
        // nicht als Text auf dem Videobild erscheint
        try { _player.SetMarqueeInt(VideoMarqueeOption.Enable, 0); } catch { }
        var success = _player.TakeSnapshot(0, filePath, width, height);
        if (wasPlaying)
            _player.SetPause(false);
        return success;
    }

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

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
        _player.SetPause(_player.IsPlaying);
    }

    private void EnsurePlaying()
    {
        var state = _player.State;
        if (state == VLCState.Stopped || state == VLCState.Ended)
            Play(_videoPath);
    }

    private void ChangeSpeed(float delta)
    {
        var current = _player.Rate <= 0f ? 1.0f : _player.Rate;
        SetSpeed(current + delta);
    }

    private void JumpSeconds(int seconds)
    {
        if (_player.Length <= 0)
            return;

        long newTime = _player.Time + seconds * 1000L;
        if (newTime < 0)
            newTime = 0;
        if (newTime > _player.Length)
            newTime = _player.Length;
        _player.Time = newTime;
        ClearDetectionOverlays(); // Alte Overlays bei Navigation entfernen
        UpdateUi();
    }

    private void Play(string path)
    {
        using var media = new Media(_libVlc, path, FromType.FromPath);
        _player.Play(media);
        _timer.Start();
        UpdateRateLabel();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] OnClosing error: {ex.Message}");
        }
    }

    private void Cleanup()
    {
        _timer.Stop();
        _scrubTimer.Stop();
        VideoView.MediaPlayer = null;
        _player.Dispose();
        _libVlc.Dispose();
    }

    private void UpdateUi()
    {
        if (_isDragging)
            return;

        var length = _player.Length;
        var time = Math.Max(0, _player.Time);

        if (length > 0)
        {
            var pos = (double)time / length;
            PositionSlider.Value = pos * PositionSlider.Maximum;
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = FormatMs(length);
        }
        else
        {
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = "--:--";
        }

        UpdateRateLabel();

        // Im Codier-Modus: Echtzeit-Code am Zeitstempel aktualisieren
        if (_isCodingMode)
            UpdateCodingCurrentCode();
    }

    private static string FormatMs(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        EnsurePlaying();
        _player.SetPause(false);
        UpdateRateLabel();
        // Overlays aufraumen — beim Abspielen sind alte Markierungen irrelevant
        ClearDetectionOverlays();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _player.SetPause(true);
        UpdateRateLabel();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        UpdateRateLabel();
    }

    private void Speed05_Click(object sender, RoutedEventArgs e) => SetSpeed(0.5f);

    private void Speed1_Click(object sender, RoutedEventArgs e) => SetSpeed(1.0f);

    private void Speed15_Click(object sender, RoutedEventArgs e) => SetSpeed(1.5f);

    private void Speed2_Click(object sender, RoutedEventArgs e) => SetSpeed(2.0f);

    private void Speed4_Click(object sender, RoutedEventArgs e) => SetSpeed(4.0f);

    private void Speed8_Click(object sender, RoutedEventArgs e) => SetSpeed(8.0f);

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDragging)
            UpdateSeekPreview();
    }

    private void SeekToSlider()
    {
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = PositionSlider.Value / max;
        if (targetPos < 0)
            targetPos = 0;
        if (targetPos > 1)
            targetPos = 1;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        UpdateUi();
    }

    private void UpdateSeekPreview()
    {
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = PositionSlider.Value / max;
        if (targetPos < 0)
            targetPos = 0;
        if (targetPos > 1)
            targetPos = 1;

        var length = _player.Length;
        if (length > 0)
        {
            var targetMs = (long)(targetPos * length);
            CurrentTimeText.Text = FormatMs(targetMs);
            DurationText.Text = FormatMs(length);
        }
        else
        {
            CurrentTimeText.Text = $"{targetPos:P0}";
            DurationText.Text = "--:--";
        }

        // Throttled live seek: schedule scrub if not already pending
        if (_isDragging && !_scrubTimer.IsEnabled)
            _scrubTimer.Start();
    }

    private void ScrubSeekToSlider()
    {
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = Math.Clamp(PositionSlider.Value / max, 0.0, 1.0);
        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        CurrentTimeText.Text = length > 0 ? FormatMs((long)(targetPos * length)) : $"{targetPos:P0}";
    }

    private void SetSpeed(float rate)
    {
        var clamped = Math.Clamp(rate, MinRate, MaxRate);
        var result = _player.SetRate(clamped);
        if (result != 0)
        {
            MessageBox.Show($"SetRate({clamped:0.##}) nicht unterstuetzt fuer dieses Video.",
                "Video", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        UpdateRateLabel();
    }

    private void UpdateRateLabel()
    {
        var rate = _player.Rate <= 0f ? 1.0f : _player.Rate;
        RateText.Text = $"{rate:0.##}x";
        UpdateSpeedButtons(rate);
    }

    private void UpdateSpeedButtons(float rate)
    {
        SetSpeedButtonState(Speed05Button, rate, 0.5f);
        SetSpeedButtonState(Speed1Button, rate, 1.0f);
        SetSpeedButtonState(Speed15Button, rate, 1.5f);
        SetSpeedButtonState(Speed2Button, rate, 2.0f);
        SetSpeedButtonState(Speed4Button, rate, 4.0f);
        SetSpeedButtonState(Speed8Button, rate, 8.0f);
    }

    private static void SetSpeedButtonState(ToggleButton button, float currentRate, float targetRate)
    {
        button.IsChecked = Math.Abs(currentRate - targetRate) < 0.01f;
    }

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

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
    }

    // Ã¢"â‚¬Ã¢"â‚¬ Quick-Scan Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        if (_isQuickScanning)
        {
            _quickScanCts?.Cancel();
            QuickScanButton.IsChecked = false;
            return;
        }

        AiRuntimeSettings cfg;
        try
        {
            cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
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
            ? $"Schaden: {segment.Label ?? "?"} (Schwere {segment.Severity})"
              + (segment.Clock != null ? $"\nUhr: {segment.Clock}" : "")
              + $"\n@ {segment.TimestampSeconds:0.0}s"
            : $"Kein Schaden @ {segment.TimestampSeconds:0.0}s";
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

    private static Color SeverityToColor(int severity, bool hasDamage)
    {
        if (!hasDamage)
            return Color.FromArgb(100, 0x94, 0xA3, 0xB8); // grey with alpha

        return severity switch
        {
            >= 4 => (Color)ColorConverter.ConvertFromString("#EF4444"), // red
            3    => (Color)ColorConverter.ConvertFromString("#F59E0B"), // orange
            2    => (Color)ColorConverter.ConvertFromString("#FACC15"), // yellow
            _    => (Color)ColorConverter.ConvertFromString("#22C55E"), // green
        };
    }

    // Ã¢"â‚¬Ã¢"â‚¬ Live Detection Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬Ã¢"â‚¬

    private static string CompactModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "?";

        var trimmed = model.Trim();
        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
            trimmed = trimmed[(slashIndex + 1)..];
        return trimmed;
    }

    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveDetectionBadge(status, dotColor, stage));
            return;
        }

        var stageSuffix = string.IsNullOrWhiteSpace(stage) ? string.Empty : $" | {stage}";
        AiStatusBadge.Visibility = Visibility.Visible;
        AiStatusText.Text = $"{status}{stageSuffix}";
        AiStatusDot.Fill = new SolidColorBrush(dotColor);
    }

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetYoloStatus(text, dotColor, model));
            return;
        }

        YoloStatusBar.Visibility = Visibility.Visible;
        TxtYoloStatus.Text = $"YOLO: {text}";
        YoloDot.Fill = new SolidColorBrush(dotColor);
        TxtYoloModel.Text = model ?? string.Empty;
    }

    private void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetCodingAiState(status, dotColor, stage, pulse));
            return;
        }

        TxtCodingAiStatus.Text = status;
        TxtCodingAiStage.Text = stage ?? string.Empty;
        CodingAiDot.Fill = new SolidColorBrush(dotColor);
        if (pulse)
            StartCodingAiPulse();
        else
            StopCodingAiPulse();
    }

    private void StartCodingAiPulse()
    {
        if (_codingAiPulseRunning)
            return;

        _codingAiPulseRunning = true;
        CodingAiPulseRing.Opacity = 1.0;
        if (CodingAiPulseRing.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            CodingAiPulseRing.RenderTransform = scale;
        }

        var scaleAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.2,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };
        var opacityAnim = new DoubleAnimation
        {
            From = 0.75,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void StopCodingAiPulse()
    {
        _codingAiPulseRunning = false;

        if (CodingAiPulseRing.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, null);
        CodingAiPulseRing.Opacity = 0;
    }

    private async void LiveDetection_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
            return;
        }

        await StartLiveDetectionAsync();
    }

    private async Task StartLiveDetectionAsync()
    {
        AiRuntimeSettings cfg;
        try
        {
            cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
        }
        catch
        {
            MessageBox.Show("KI-Konfiguration konnte nicht geladen werden.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            LiveDetectionButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LiveDetectionButton.IsChecked = false;
            return;
        }

        try
        {
            var client = new OllamaClient(cfg.OllamaBaseUri,
                ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
                keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);

            // Auto-detect vision model: check if configured model exists, fallback to first *vl* model
            var visionModel = cfg.VisionModel;
            try
            {
                var models = await client.ListModelNamesAsync(CancellationToken.None);
                bool configuredExists = false;
                string? fallbackVision = null;
                foreach (var m in models)
                {
                    if (m.StartsWith(visionModel, StringComparison.OrdinalIgnoreCase) ||
                        m.Equals(visionModel, StringComparison.OrdinalIgnoreCase))
                        configuredExists = true;
                    if (fallbackVision == null && m.Contains("vl", StringComparison.OrdinalIgnoreCase))
                        fallbackVision = m;
                }
                if (!configuredExists && fallbackVision != null)
                    visionModel = fallbackVision;
            }
            catch { /* use configured model */ }

            _liveDetectionClient = client;
            _liveDetectionService = new LiveDetectionService(client, visionModel);
            _liveDetectionModelName = visionModel;
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            // Show overlay layer
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(visionModel));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _detectionTimer.Tick += DetectionTimer_Tick;
            _detectionTimer.Start();

            // Run first detection immediately
            RunDetectionAsync().SafeFireAndForget("LiveDetection");
        }
        catch (Exception ex)
        {
            LiveDetectionButton.IsChecked = false;
            MessageBox.Show($"Live-KI konnte nicht gestartet werden: {ex.Message}", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopLiveDetection()
    {
        _detectionTimer?.Stop();
        _detectionTimer = null;
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
        _isDetecting = false;
        _isDetectionInFlight = false;
        _liveDetectionService = null;
        _liveDetectionClient?.Dispose();
        _liveDetectionClient = null;
        _liveDetectionModelName = string.Empty;

        // Hide overlay layer (unless manual mark mode is still active)
        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", Color.FromRgb(0x94, 0xA3, 0xB8));
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;
        _currentFindings.Clear();

        // Fertig-Meldung mit Zusammenfassung
        int totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet — {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        // Video pausieren damit der User die Meldung sieht
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            if (!_isDetecting)
                LiveDetectionStatusText.Visibility = Visibility.Collapsed;
        };
        hideTimer.Start();
    }

    private async void DetectionTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await RunDetectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] DetectionTimer_Tick Fehler: {ex.Message}");
        }
    }

    private async Task RunDetectionAsync()
    {
        if (_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null)
            return;
        if (!_player.IsPlaying)
            return;
        // Keine neue Analyse waehrend User-Bestaetigung
        if (_detectionPendingFindings != null)
            return;

        _isDetectionInFlight = true;
        SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
            $"{CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Bereit");
                return;
            }

            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{CompactModelName(_liveDetectionModelName)} | Inferenz");
            var timestampSec = _player.Time / 1000.0;
            var result = await _liveDetectionService.AnalyzeFrameAsync(
                snapshot, timestampSec, _detectionCts.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (!_isDetecting) return;

                _lastDetectionTimestamp = result.TimestampSeconds;
                _currentFindings.Clear();
                _currentFindings.AddRange(result.Findings);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Overlay");

                // Auto-Pause bei relevanten Befunden (Severity >= 2)
                var significantFindings = result.Findings
                    .Where(f => f.Severity >= 2).ToList();
                if (significantFindings.Count > 0)
                {
                    _detectionPendingFindings = significantFindings;
                    _detectionPendingFrameBytes = snapshot;
                    _detectionPendingTimestampSec = result.TimestampSeconds;
                    ShowDetectionConfirmation(significantFindings);
                    SetLiveDetectionBadge("Befund erkannt", Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{CompactModelName(_liveDetectionModelName)} | Warte auf Bestaetigung");
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Length > 200) msg = msg[..200] + "...";
            Dispatcher.Invoke(() =>
            {
                LiveDetectionStatusText.Text = $"Fehler: {msg}";
                SetLiveDetectionBadge("KI Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                    CompactModelName(_liveDetectionModelName));
            });
        }
        finally
        {
            _isDetectionInFlight = false;
        }
    }

    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sewer_live_{Guid.NewGuid():N}.png");
        try
        {
            var success = TakeSnapshotSafe(tempPath, 640);
            if (!success)
                return null;

            // Wait briefly for file write
            await Task.Delay(80);

            if (!File.Exists(tempPath))
                return null;

            return await File.ReadAllBytesAsync(tempPath,
                _detectionCts?.Token ?? CancellationToken.None);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private void UpdateDetectionStatus(LiveDetection result)
    {
        if (result.Error is not null)
        {
            LiveDetectionStatusText.Text = $"Fehler: {result.Error}";
            return;
        }

        var count = result.Findings.Count;
        LiveDetectionStatusText.Text = count > 0
            ? $"{count} Schaden erkannt @ {result.TimestampSeconds:0.0}s"
            : $"Kein Schaden @ {result.TimestampSeconds:0.0}s";

        if (count > 0)
        {
            var summary = string.Join(" | ",
                result.Findings.Take(3).Select(f =>
                    $"{f.VsaCodeHint ?? f.Label} (S{f.Severity})"));
            FindingSummaryPanel.Visibility = Visibility.Visible;
            FindingSummaryText.Text = summary;
        }
        else
        {
            FindingSummaryPanel.Visibility = Visibility.Collapsed;
        }
    }

    // Ã¢"â‚¬Ã¢"â‚¬ Detection Overlay Rendering (ring-sector pattern from LiveFrameWindow) Ã¢"â‚¬Ã¢"â‚¬

    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
    {
        DetectionCanvas.Children.Clear();

        var width = DetectionCanvas.ActualWidth;
        var height = DetectionCanvas.ActualHeight;
        if (width < 60 || height < 60)
            return;

        if (findings.Count == 0)
            return;

        // Pruefen ob mindestens ein Finding Bbox hat
        bool hasBbox = findings.Any(f => f.BboxX1.HasValue && f.BboxY1.HasValue
                                       && f.BboxX2.HasValue && f.BboxY2.HasValue);

        // Wenn keine Bboxes: Fallback auf Ring-Sektor-Darstellung
        if (!hasBbox)
        {
            RenderRingSectorOverlay(findings, timestampSec, width, height);
            return;
        }

        // ── Bbox-basiertes Rendering: Rechtecke + Labels direkt auf dem Bild ──
        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var finding = findings[i];
            var color = MapDetectionSeverityColor(finding.Severity);

            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var px1 = finding.BboxX1.Value * width;
                var py1 = finding.BboxY1.Value * height;
                var px2 = finding.BboxX2.Value * width;
                var py2 = finding.BboxY2.Value * height;

                var rectLeft = Math.Min(px1, px2);
                var rectTop = Math.Min(py1, py2);
                var rectW = Math.Max(1, Math.Abs(px2 - px1));
                var rectH = Math.Max(1, Math.Abs(py2 - py1));

                // Farbiges Rechteck (halbtransparent gefuellt, farbiger Rand)
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = rectW,
                    Height = rectH,
                    Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)),
                    RadiusX = 4,
                    RadiusY = 4,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, rectLeft);
                Canvas.SetTop(rect, rectTop);
                DetectionCanvas.Children.Add(rect);

                // Label-Badge oben am Rechteck
                var labelText = $"{finding.VsaCodeHint ?? finding.Label} [S{finding.Severity}]";
                if (finding.ExtentPercent is > 0)
                    labelText += $" {finding.ExtentPercent}%";

                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    IsHitTestVisible = true,
                    Child = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                };

                var capturedFinding = finding;
                var capturedTimestamp = timestampSec;
                label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
                label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
                    + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
                    + $"\nSchwere: {finding.Severity}/5";

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = label.DesiredSize;
                var lx = Math.Clamp(rectLeft, 2, width - desired.Width - 2);
                var ly = Math.Clamp(rectTop - desired.Height - 4, 2, height - desired.Height - 2);
                Canvas.SetLeft(label, lx);
                Canvas.SetTop(label, ly);
                DetectionCanvas.Children.Add(label);
            }
            else
            {
                // Einzelnes Finding ohne Bbox → Ring-Sektor-Fallback
                RenderRingSectorFinding(finding, i, findings.Count, width, height, timestampSec);
            }
        }
    }

    /// <summary>
    /// Fallback: Ring-Sektor-Darstellung wenn keine Bounding Boxes verfuegbar.
    /// </summary>
    private void RenderRingSectorOverlay(IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec, double width, double height)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        // Aeusserer Fuehrungsring
        var guide = new System.Windows.Shapes.Ellipse
        {
            Width = ringOuter * 2, Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        DetectionCanvas.Children.Add(guide);

        // Innerer Fuehrungsring
        var guideInner = new System.Windows.Shapes.Ellipse
        {
            Width = ringInner * 2, Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        DetectionCanvas.Children.Add(guideInner);

        // Uhr-Teilstriche
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            DetectionCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = cx + Math.Cos(rad) * (ringInner - 4),
                Y1 = cy + Math.Sin(rad) * (ringInner - 4),
                X2 = cx + Math.Cos(rad) * (ringOuter + 4),
                Y2 = cy + Math.Sin(rad) * (ringOuter + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8, IsHitTestVisible = false
            });
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
            RenderRingSectorFinding(findings[i], i, findings.Count, width, height, timestampSec);
    }

    /// <summary>
    /// Rendert ein einzelnes Finding als Ring-Sektor (Fallback ohne Bbox).
    /// </summary>
    private void RenderRingSectorFinding(LiveFrameFinding finding, int index, int total,
        double width, double height, double timestampSec)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var parsedClock = ParseClockHour(finding.PositionClock);
        var centerDeg = parsedClock.HasValue
            ? -90 + (parsedClock.Value % 12) * 30
            : -90 + index * (360.0 / total);

        var sweep = finding.ExtentPercent is > 0
            ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
            : 18.0;

        var startDeg = centerDeg - sweep / 2.0;
        var color = MapDetectionSeverityColor(finding.Severity);

        var sector = new System.Windows.Shapes.Path
        {
            Data = BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
            Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 1.0, IsHitTestVisible = false
        };
        DetectionCanvas.Children.Add(sector);

        // Severity-Punkt ausserhalb Ring
        var rad2 = DegToRad(centerDeg);
        var mx = cx + Math.Cos(rad2) * (ringOuter + 2);
        var my = cy + Math.Sin(rad2) * (ringOuter + 2);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White, StrokeThickness = 0.8, IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, mx - 4);
        Canvas.SetTop(dot, my - 4);
        DetectionCanvas.Children.Add(dot);

        // Label-Badge (klickbar)
        var labelText = BuildDetectionLabel(finding);
        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 17, 19, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = labelText, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        var capturedFinding = finding;
        var capturedTimestamp = timestampSec;
        label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
        label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
            + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
            + $"\nSchwere: {finding.Severity}/5";

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var lx = Math.Cos(rad2) >= 0 ? mx + 8 : mx - desired.Width - 8;
        var ly = my - desired.Height / 2.0;
        Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
        Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
        DetectionCanvas.Children.Add(label);
    }

    private static string BuildDetectionLabel(LiveFrameFinding f)
    {
        var baseText = string.IsNullOrWhiteSpace(f.VsaCodeHint)
            ? f.Label : $"{f.VsaCodeHint} {f.Label}";
        if (baseText.Length > 24) baseText = baseText[..24] + "...";

        var clock = string.IsNullOrWhiteSpace(f.PositionClock) ? "?" : f.PositionClock;
        var extent = f.ExtentPercent is > 0 ? $"{f.ExtentPercent}%" : "";
        var extra = "";
        if (f.HeightMm is > 0) extra += $" H:{f.HeightMm}mm";
        if (f.IntrusionPercent is > 0) extra += $" Einr:{f.IntrusionPercent}%";
        return $"{clock}{(extent.Length > 0 ? $" / {extent}" : "")}{extra} - {baseText}";
    }

    private static Color MapDetectionSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),
        4 => Color.FromRgb(249, 115, 22),
        3 => Color.FromRgb(245, 158, 11),
        2 => Color.FromRgb(132, 204, 22),
        _ => Color.FromRgb(34, 197, 94)
    };

    private static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;
        if (hour == 0) return 12;
        if (hour > 12) hour %= 12;
        return hour == 0 ? 12 : hour;
    }

    private static Geometry BuildRingSectorGeometry(
        double cx, double cy, double innerR, double outerR, double startDeg, double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var large = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
        var p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
        var p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
        var p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { fig });
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

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
    private static ICodingSessionService CreateCodingSessionService()
    {
        return new CodingSessionService(
            () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig());
    }

    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= CreateCodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(
                _codingSessionService,
                _codingOverlayService,
                new InfraSelfImproving.CodingFeedbackRecorder());
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
        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null) return;

            var timestampSec = _player.Time / 1000.0;

            // Uhrzeiger-Position aus Overlay-Zentrum berechnen
            string? clockPos = null;
            if (overlay.Points.Count > 0)
            {
                var avgX = overlay.Points.Average(p => p.X);
                var avgY = overlay.Points.Average(p => p.Y);
                var cx = 0.5; var cy = 0.5; // Rohrmitte (normalisiert)
                var dx = avgX - cx;
                var dy = avgY - cy;
                var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var clockAngle = (angleDeg + 90 + 360) % 360;
                var hour = (int)Math.Round(clockAngle / 30.0) % 12;
                if (hour == 0) hour = 12;
                clockPos = hour.ToString();
            }

            // Training speichern: Frame + YOLO-Export + TeacherAnnotation + CodingEvent
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos);

            // Overlay entfernen und Canvas neu zeichnen
            if (_codingVm != null) _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);

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
            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            // Frame in Temp speichern
            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_mark_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Temp aufräumen
            try { System.IO.File.Delete(tempFrame); } catch { }

            // 5. TeacherAnnotation erstellen + persistieren
            var captureMeter = 0.0;
            if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedMeter))
                captureMeter = parsedMeter;

            var annotation = new TeacherAnnotation
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

            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

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
            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();

            foreach (var finding in _detectionPendingFindings)
            {
                var code = finding.VsaCodeHint ?? finding.Label;
                int classId = InfraTeacher.VsaYoloClassMap.GetClassId(code);
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
                var annotation = new TeacherAnnotation
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
                await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);
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

            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"det_corr_{annotationId}";

            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);
            try { System.IO.File.Delete(tempFrame); } catch { }

            var annotation = new TeacherAnnotation
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
            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

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
    private SingleFrameMultiModelService? _codingMultiModel;
    private VisionPipelineClient? _codingVisionClient;
    private bool _codingUseMultiModel;

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
        _codingSessionService = CreateCodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(
            _codingSessionService,
            _codingOverlayService,
            new InfraSelfImproving.CodingFeedbackRecorder());
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

        // Beim Verlassen: IMMER offene Streckenschaeden schliessen
        // (egal ob Rohrende, Abbruch oder einfacher Exit)
        if (_codingVm != null && _codingVm.Events.Count > 0)
        {
            var endMeter = _codingLastOsdMeter ?? _codingVm.EndMeter;
            if (!CloseOpenStreckenschaeden(endMeter))
            {
                // User hat "Abbrechen" geklickt → Exit abbrechen, weiter codieren
                _isCodingMode = true;
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

        // Timer stoppen
        StopCodingOsdTimer();
        _codingLiveAiTimer?.Stop();
        _codingLiveAiTimer = null;
        StopCodingAiPulse();

        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts?.Dispose();
        _codingAnalysisCts = null;

        // Import-Referenzliste leeren
        _codingImportEvents.Clear();
        LstImportEvents.ItemsSource = null;

        // Bestaetigungs-Panel und Detection-Overlays schliessen
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

        // UI ausblenden
        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        CodingOverlayPopup.IsOpen = false;
        CodingOverlayCanvas.Children.Clear();
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
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

        // Tool-State zuruecksetzen
        _activeCodingToolName = null;
        TxtActiveToolLabel.Text = "";
        BtnCodingLiveAi.IsChecked = false;
        TxtCodingAiStage.Text = string.Empty;

        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        // Event-Handler abmelden (Memory Leak verhindern)
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
            var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);
            if (ev.Entry.FotoPaths.Count > 1)
            {
                sample.AdditionalFramePaths ??= new System.Collections.Generic.List<string>();
                for (int i = 1; i < ev.Entry.FotoPaths.Count; i++)
                    sample.AdditionalFramePaths.Add(ev.Entry.FotoPaths[i]);
            }
            InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { sample })
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
            var samples = new List<TrainingSample>();
            foreach (var ev in _codingVm.Events)
            {
                var framePath = ev.Entry.FotoPaths.Count > 0 ? ev.Entry.FotoPaths[0] : null;
                var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);

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
                InfraTraining.TrainingSamplesStore.MergeAndSaveAsync(samples)
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

    private static bool HasValidLength(HaltungRecord record, string fieldName)
    {
        var raw = record.GetFieldValue(fieldName);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = raw.Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0;
    }

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();

    // --- Coding UI-Update ---

    // Flag: wird true wenn Meter-Navigation (Next/Previous) auslÃƒÂ¶st
    private bool _codingNavPending;

    // Benannter Handler fuer sauberes Cleanup via -=
    private void CodingVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => UpdateCodingUi(e.PropertyName));

    private void UpdateCodingUi(string? propertyName)
    {
        if (_codingVm == null) return;
        TxtCodingMeter.Text = $"{_codingVm.CurrentMeter:F2}m";
        PipeTimeline.CurrentMeter = _codingVm.CurrentMeter;
        // Video NUR synchronisieren wenn explizite Navigation (Next/Previous)
        // Verhindert Zurueckspringen beim normalen Abspielen
        if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)
        {
            _codingNavPending = false;
            SyncVideoToCodingMeter();
        }
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);

        // Aktuellen Code am Zeitstempel anzeigen (Echtzeit)
        UpdateCodingCurrentCode();

        // Statistiken aktualisieren (nur bei relevanten Property-Aenderungen)
        if (propertyName is nameof(CodingSessionViewModel.StatAutoAccepted) or
            nameof(CodingSessionViewModel.StatPending) or
            nameof(CodingSessionViewModel.StatReviewRequired) or
            nameof(CodingSessionViewModel.StatAverageConfidence) or
            nameof(CodingSessionViewModel.EventCount) or
            null)
        {
            UpdateCodingStatistics();
        }
    }

    /// <summary>
    /// Zeigt den naechsten existierenden Code in der Toolbar an, basierend auf aktuellem Meter.
    /// </summary>
    private void UpdateCodingCurrentCode()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0)
        {
            CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        // Aktuellen Meter ermitteln: OSD-Wert bevorzugen, sonst Video-Position berechnen
        double currentMeter;
        if (_codingLastOsdMeter.HasValue)
        {
            currentMeter = _codingLastOsdMeter.Value;
        }
        else if (_player.Length > 0 && _codingVm.EndMeter > 0)
        {
            currentMeter = (_player.Time / (double)_player.Length) * _codingVm.EndMeter;
        }
        else
        {
            currentMeter = _codingVm.CurrentMeter;
        }

        // Naechsten Code innerhalb Ã‚Â±0.5m finden
        var nearestEvent = _codingVm.Events
            .Where(ev => Math.Abs(ev.MeterAtCapture - currentMeter) < 0.5)
            .OrderBy(ev => Math.Abs(ev.MeterAtCapture - currentMeter))
            .FirstOrDefault();

        if (nearestEvent != null)
        {
            TxtCodingCurrentCode.Text = $"Ã¢-Â¶ {nearestEvent.MeterAtCapture:F2}m {nearestEvent.Entry.Code} {nearestEvent.Entry.Beschreibung}";
            CodingCurrentCodeBadge.Visibility = Visibility.Visible;
        }
        else
        {
            // Naechsten bevorstehenden Code anzeigen
            var nextEvent = _codingVm.Events
                .Where(ev => ev.MeterAtCapture > currentMeter)
                .OrderBy(ev => ev.MeterAtCapture)
                .FirstOrDefault();

            if (nextEvent != null)
            {
                var distM = nextEvent.MeterAtCapture - currentMeter;
                TxtCodingCurrentCode.Text = $"→ in {distM:F1}m: {nextEvent.Entry.Code}";
                CodingCurrentCodeBadge.Visibility = Visibility.Visible;
            }
            else
            {
                CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SyncVideoToCodingMeter()
    {
        if (_codingVm == null || _player.Length <= 0 || _codingVm.EndMeter <= 0) return;
        double fraction = _codingVm.CurrentMeter / _codingVm.EndMeter;
        long targetMs = (long)(fraction * _player.Length);
        _player.Time = Math.Clamp(targetMs, 0, _player.Length);
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
    }

    /// <summary>
    /// Hält die Overlay-Zeichenfläche exakt auf VideoView-Größe.
    /// Wichtig für Popup-Overlay über VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
    {
        double w = VideoView.ActualWidth;
        double h = VideoView.ActualHeight;
        if (double.IsNaN(w) || double.IsInfinity(w) || w <= 1 ||
            double.IsNaN(h) || double.IsInfinity(h) || h <= 1)
            return;

        if (Math.Abs(CodingOverlayCanvas.Width - w) > 0.5)
            CodingOverlayCanvas.Width = w;
        if (Math.Abs(CodingOverlayCanvas.Height - h) > 0.5)
            CodingOverlayCanvas.Height = h;
    }

    // --- Coding Navigation ---

    private async void CodingNext_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MoveNextCommand.Execute(null);
            // Video pausieren bei Schritt-Navigation
            _player.SetPause(true);
            // OSD-Meter automatisch lesen nach Navigation
            _codingLastOsdMeter = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingNext_Click error: {ex.Message}");
        }
    }

    private async void CodingPrevious_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MovePreviousCommand.Execute(null);
            _player.SetPause(true);
            _codingLastOsdMeter = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingPrevious_Click error: {ex.Message}");
        }
    }

    // --- Coding Werkzeuge ---

    // Vereinfachte Werkzeuge: nur Kalibrieren + Rechteck (Rest im PhotoAssistant)
    // Rechteck nutzt ActivateMarkTool → nach Zeichnen oeffnet sich automatisch der Code-Katalog
    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Markieren");

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
           && _codingOverlayService?.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level;

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
            _ => null
        };
    }

    private string GetDefaultCodingSchemaHandleId()
        => _codingSchemaType switch
        {
            SchemaType.PipeBend => "vertex",
            SchemaType.FillLevel => "level",
            SchemaType.Intrusion => "depth",
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

            // Mark-Modus: direkt VsaCodeExplorer oeffnen + Training speichern
            if (_markToolType != OverlayToolType.None)
            {
                HandleMarkDrawingComplete();
                return;
            }

            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                _ = AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay);
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

    private void ClearTransientCodingCanvas(bool clearManualOverlay)
    {
        var remove = CodingOverlayCanvas.Children
            .OfType<FrameworkElement>()
            .Where(el => el.Tag is string tag &&
                         (tag == "tool_badge" ||
                          tag == "overlay_preview" ||
                          tag == "overlay_measure" ||
                          (clearManualOverlay && tag == "overlay_manual")))
            .ToList();

        foreach (var el in remove)
            CodingOverlayCanvas.Children.Remove(el);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay)
    {
        UpdateCodingOverlayViewport();
        ClearTransientCodingCanvas(clearManualOverlay: true);
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
        if (overlay.ToolType == OverlayToolType.PipeBend && overlay.ArcDegrees.HasValue)
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
        if (overlay.ToolType == OverlayToolType.PipeBend)
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
                if (_codingVm.CurrentOverlay.ArcDegrees.HasValue && _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend)
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

                // Manuell codiert: Noch nicht bestaetigt — User muss "Akzeptieren" klicken.
                // Erst wenn alles gruen ist, stimmen die Daten fuer das KI-Training.
                createdEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = entry.Code,
                    Confidence = 1.0,
                    Reason = "Manuell codiert — bitte bestaetigen",
                    Decision = CodingUserDecision.Ignored
                };

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
            if (_codingVm.CurrentOverlay.ArcDegrees.HasValue && _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend)
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

        // Manuell codiert: Noch nicht bestaetigt — User muss "Akzeptieren" klicken.
        // Erst wenn alles gruen ist, stimmen die Daten fuer das KI-Training.
        manualEvent.AiContext = new CodingEventAiContext
        {
            SuggestedCode = entry.Code,
            Confidence = 1.0,
            Reason = "Manuell codiert — bitte bestaetigen",
            Decision = CodingUserDecision.Ignored
        };

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

    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame und speichert ihn im Projektordner.
    /// </summary>
    private string? CodingCaptureSnapshot(ProtocolEntry entry)
    {
        try
        {
            // Zielverzeichnis: neben dem Video oder im Temp
            var videoDir = !string.IsNullOrEmpty(_videoPath)
                ? Path.GetDirectoryName(_videoPath) ?? Path.GetTempPath()
                : Path.GetTempPath();
            var fotoDir = Path.Combine(videoDir, "Fotos");
            Directory.CreateDirectory(fotoDir);

            var ts = entry.Zeit.HasValue
                ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff")
                : DateTimeOffset.Now.ToString("HHmmss");
            var fileName = $"{entry.Code}_{entry.MeterStart:F2}m_{ts}.png";
            var filePath = Path.Combine(fotoDir, fileName);

            TakeSnapshotSafe(filePath);

            // VLC schreibt asynchron - kurz warten
            for (int i = 0; i < 20; i++)
            {
                System.Threading.Thread.Sleep(50);
                if (File.Exists(filePath) && new FileInfo(filePath).Length > 100)
                    return filePath;
            }

            return File.Exists(filePath) ? filePath : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot-Fehler: {ex.Message}");
            return null;
        }
    }

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

    private void RefreshCodingEventsList()
    {
        if (_codingVm == null) return;

        // Nach Meter sortieren, dann nach Videozeit
        var sorted = _codingVm.Events
            .OrderBy(e => e.MeterAtCapture)
            .ThenBy(e => e.VideoTimestamp)
            .ToList();

        var selected = LstCodingEvents.SelectedItem;
        _codingVm.Events.Clear();
        foreach (var ev in sorted)
            _codingVm.Events.Add(ev);

        LstCodingEvents.ItemsSource = null;
        LstCodingEvents.ItemsSource = _codingVm.Events;
        if (selected != null)
            LstCodingEvents.SelectedItem = selected;

        // Verzoeiert Einfaerbung nach Layout-Update
        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateCodingStatistics();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Defekt-Detail-Panel, Aktionsbuttons, Statistik
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ev)
        {
            if (_codingVm != null) _codingVm.SelectedDefect = ev;
            UpdateCodingDefectDetailPanel(ev);
            UpdateInlineDefectDetail(ev);
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
            HideInlineDefectDetail();
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
            BtnInlineAccept.Visibility = Visibility.Visible;
            BtnInlineReject.Visibility = Visibility.Visible;
        }
        else
        {
            TxtInlineDetailConfidence.Text = "\u2013";
            TxtInlineDetailConfidence.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
            BtnInlineAccept.Visibility = Visibility.Collapsed;
            BtnInlineReject.Visibility = Visibility.Collapsed;
        }

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
        var imagesDir = InfraTeacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        // 4. Lehrer-Annotation erstellen
        var annotation = new TeacherAnnotation
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = Domain.Models.OverlayToolType.None,
            FullFramePath = destFrame,
        };

        await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

        // 5. Visuelles Feedback
        try { System.IO.File.Delete(snapshotPath); } catch { }
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = $"✓ {importEvent.Entry.Code} @ {importEvent.MeterAtCapture:F1}m bestätigt";
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) => { OsdMeterBadge.Visibility = Visibility.Collapsed; resetTimer.Stop(); };
        resetTimer.Start();
    }

    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        _codingVm?.AcceptDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            UpdateCodingDefectDetailPanel(_codingVm.SelectedDefect);
            RefreshCodingEventsList();
            // Overlay kurz gruen blinken lassen, dann entfernen
            FadeOutAiOverlayAfterAction();
        }
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        var ev = _codingVm.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null) return;
        _codingVm.SelectedDefect = ev;
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var entry = ev.Entry;
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
                entry, entry.MeterStart, entry.Zeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _codingVm.VideoPath, _codingVm.CurrentVideoTime)
            {
                Owner = this,
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
                _codingSessionService?.UpdateEvent(ev.EventId, entry, ev.Overlay);
                ev.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? ev.MeterAtCapture;
                ev.VideoTimestamp = entry.Zeit ?? ev.VideoTimestamp;

                if (ev.AiContext != null)
                    _codingVm.EditDefectCommand.Execute(null);
                RefreshCodingEventsList();
                UpdateCodingDefectDetailPanel(ev);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        var ev = _codingVm?.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null || _codingVm == null) return;

        // Ablehnen = Eintrag komplett entfernen (nicht nur Status setzen)
        _codingSessionService?.RemoveEvent(ev.EventId);
        _codingVm.Events.Remove(ev);
        _codingVm.SelectedDefect = null;
        CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        RefreshCodingEventsList();
        FadeOutAiOverlayAfterAction();
    }

    /// <summary>Defekt-Detail-Panel mit Werten des ausgewaehlten Events befuellen.</summary>
    /// Details werden jetzt oben im KI-BEFUNDE Panel angezeigt — unteres Panel bleibt collapsed.
    private void UpdateCodingDefectDetailPanel(CodingEvent ev)
    {
        // CodingDefectDetailPanel.Visibility = Visibility.Visible; // Deaktiviert: Details sind im oberen Panel

        TxtCodingDetailCode.Text = ev.Entry.Code;
        TxtCodingDetailDescription.Text = ev.Entry.Beschreibung;
        TxtCodingDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        // Uhrposition
        TxtCodingDetailClock.Text = ev.Overlay?.ClockFrom != null
            ? $"{ev.Overlay.ClockFrom:F0}h"
            : "\u2013";

        // Schweregrad
        if (ev.Entry.CodeMeta?.Parameters != null &&
            ev.Entry.CodeMeta.Parameters.TryGetValue("vsa.schweregrad", out var sev))
            TxtCodingDetailSeverity.Text = sev;
        else
            TxtCodingDetailSeverity.Text = "\u2013";

        // Konfidenz + Farbe
        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtCodingDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtCodingDetailConfidence.Foreground = CodingSessionViewModel.GetConfidenceBrush(conf);
            CodingDefectDetailBorderBrush.Color = ((SolidColorBrush)CodingSessionViewModel.GetZoneBrush(conf)).Color;
        }
        else
        {
            TxtCodingDetailConfidence.Text = "\u2013";
            TxtCodingDetailConfidence.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            CodingDefectDetailBorderBrush.Color = Color.FromRgb(0x3B, 0x82, 0xF6);
        }

        // Status
        var status = CodingSessionViewModel.GetDefectStatus(ev);
        TxtCodingDetailStatus.Text = $"Status: {CodingStatusToDisplayText(status)}";

        // Alle Aktionen immer verfuegbar — auch manuell codierte Events
        // muessen bestaetigt werden bevor sie als Training-Signal gelten.
        CodingDefectActionGrid.Visibility = Visibility.Visible;
        BtnCodingAcceptDefect.Visibility = Visibility.Visible;
        BtnCodingEditDefect.Visibility = Visibility.Visible;
        BtnCodingRejectDefect.Visibility = Visibility.Visible;
    }

    private static string CodingStatusToDisplayText(DefectStatus status) => status switch
    {
        DefectStatus.AutoAccepted     => "Auto-Akzeptiert (Green Zone)",
        DefectStatus.Pending          => "Review empfohlen (Yellow Zone)",
        DefectStatus.ReviewRequired   => "Manuell erforderlich (Red Zone)",
        DefectStatus.Accepted         => "Akzeptiert",
        DefectStatus.AcceptedWithEdit => "Bearbeitet",
        DefectStatus.Rejected         => "Abgelehnt",
        _ => ""
    };

    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        for (int i = 0; i < LstCodingEvents.Items.Count; i++)
        {
            if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;

            // Zone-Dot einfaerben: Status hat Vorrang vor Konfidenz
            // Akzeptiert = gruen, Abgelehnt = rot, sonst Konfidenz-Farbe
            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                zoneDot.Fill = status switch
                {
                    DefectStatus.Accepted or DefectStatus.AutoAccepted
                        => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // Gruen
                    DefectStatus.AcceptedWithEdit
                        => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), // Blau
                    DefectStatus.Rejected
                        => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // Rot
                    _ => ev.AiContext != null
                        ? CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence)
                        : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))   // Grau (unbestaetigt)
                };
            }

            // Konfidenz-Text einfaerben
            var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
            if (confText != null && ev.AiContext != null)
            {
                confText.Text = $"{ev.AiContext.Confidence * 100:F0}%";
                confText.Foreground = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
            }
            else if (confText != null)
            {
                confText.Text = "";
            }

            // Status-Icon
            var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = status switch
                {
                    DefectStatus.AutoAccepted      => "\u2713",
                    DefectStatus.Accepted           => "\u2713",
                    DefectStatus.AcceptedWithEdit   => "\u270E",
                    DefectStatus.Pending            => "\u23F3",
                    DefectStatus.ReviewRequired     => "\u26A0",
                    DefectStatus.Rejected           => "\u2717",
                    _ => ""
                };
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }
    }

    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindCodingChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindCodingChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Statistiken im Seitenpanel aktualisieren (direkt berechnet).</summary>
    private void UpdateCodingStatistics()
    {
        if (_codingVm == null) return;

        RunCodingDefectCount.Text = _codingVm.Events.Count.ToString();

        // Statistiken direkt aus Events berechnen
        var aiEvents = _codingVm.Events.Where(e => e.AiContext != null).ToList();
        int autoAccepted = 0, pending = 0, reviewRequired = 0;

        foreach (var ev in aiEvents)
        {
            var status = CodingSessionViewModel.GetDefectStatus(ev);
            switch (status)
            {
                case DefectStatus.AutoAccepted:
                case DefectStatus.Accepted:
                case DefectStatus.AcceptedWithEdit:
                    autoAccepted++;
                    break;
                case DefectStatus.Pending:
                    pending++;
                    break;
                case DefectStatus.ReviewRequired:
                    reviewRequired++;
                    break;
            }
        }

        RunCodingOpenCount.Text = (pending + reviewRequired).ToString();
        TxtCodingStatAutoAccepted.Text = autoAccepted.ToString();
        TxtCodingStatPending.Text = pending.ToString();
        TxtCodingStatReviewRequired.Text = reviewRequired.ToString();
        TxtCodingStatAvgConfidence.Text = aiEvents.Count > 0
            ? $"{aiEvents.Average(e => e.AiContext!.Confidence) * 100:F0}%"
            : "\u2013";
    }

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

    private void StartCodingOsdTimer()
    {
        _codingOsdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _codingOsdTimer.Tick += async (_, _) =>
        {
            if (!_isCodingMode || _codingOsdReading || _codingLiveDetection == null) return;
            _codingOsdReading = true;
            try
            {
                await CodingReadOsdMeterAsync();
            }
            finally
            {
                _codingOsdReading = false;
            }
        };
        _codingOsdTimer.Start();
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdTimer?.Stop();
        _codingOsdTimer = null;
        _codingOsdReading = false;
    }

    // --- Coding KI-Analyse ---

    private async void InitCodingAi()
    {
        try
        {
            var config = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Kuenstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel);
            _codingQualityGate = new QualityGateService();

            // Multi-Model Pipeline (YOLO → DINO → SAM) initialisieren
            try
            {
                var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://localhost:8100";
                _codingVisionClient = new VisionPipelineClient(new Uri(sidecarUrl));
                var health = await _codingVisionClient.HealthCheckAsync();
                if (health != null)
                {
                    _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient);
                    _codingUseMultiModel = true;
                    SetCodingAiState("Kuenstliche Intelligenz bereit (Multi-Model)", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"YOLO+DINO+SAM + {CompactModelName(_codingAiModelName)}");
                }
                else
                {
                    _codingUseMultiModel = false;
                    SetCodingAiState("Kuenstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"Sidecar offline → {CompactModelName(_codingAiModelName)}");
                }
            }
            catch
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Kuenstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"Sidecar offline → {CompactModelName(_codingAiModelName)}");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(_codingAiModelName));
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
            "VERSATZ" or "VERSCHIEBUNG" => "BAH",
            "WURZELN" or "BEWUCHS" => "BBB",
            "ABLAGERUNG" => "BBC",
            "INKRUSTATION" => "BBA",
            "WASSERSTAND" => "BDDC",
            "ABBRUCH" => "BDC",
            // Kein exaktes Stichwort → Freitext-Heuristik (z.B. "beule unten", "riss bei 3 uhr")
            _ => VsaCodeResolver.InferCodeFromLabel(keyword)
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
    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }

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

            // ── Multi-Model Pfad: YOLO → DINO → SAM ──
            if (_codingUseMultiModel && _codingMultiModel != null)
            {
                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 1 von 4: Snapshot", pulse: true);

                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 2 von 4: YOLO und DINO", pulse: true);

                int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
                var mmResult = await _codingMultiModel.AnalyzeFrameAsync(
                    pngBytes, dn, _codingOverlayService?.Calibration,
                    _codingAnalysisCts.Token);

                if (mmResult.Error != null)
                {
                    SetCodingAiState($"Fehler: {mmResult.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }

                if (!mmResult.IsRelevant || !mmResult.HasDetections)
                {
                    SetCodingAiState("Kein Schaden erkannt", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"YOLO {mmResult.YoloTimeMs:F0}ms | {mmResult.DinoDetections.Count} Detektionen");
                    Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 3 von 4: SAM-Masken ({mmResult.DinoDetections.Count} Befunde)", pulse: true);

                // Masken und Labels auf Canvas rendern
                ShowMultiModelResults(mmResult);

                SetCodingAiState(
                    $"{mmResult.DinoDetections.Count} Befunde erkannt",
                    Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"YOLO {mmResult.YoloTimeMs:F0}ms | DINO {mmResult.DinoTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms");

                // Events erstellen
                AddMultiModelFindingsAsEvents(mmResult, captureTimestampSec);
                return;
            }

            // ── Qwen-only Fallback-Pfad ──
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1 von 3: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        $"Modell: {CompactModelName(_codingAiModelName)}");
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 2 von 3: Inferenz ({CompactModelName(_codingAiModelName)})", pulse: true);

                LiveDetection result;
                if (_codingEnhancedVision != null)
                {
                    var b64 = Convert.ToBase64String(pngBytes);
                    var importContext = GatherImportContext();
                    var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                        b64, importContext, _codingAnalysisCts.Token);
                    result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
                }
                else
                {
                    result = await _codingLiveDetection!.AnalyzeFrameAsync(
                        pngBytes, captureTimestampSec, _codingAnalysisCts.Token);
                }

                ShowCodingAiResults(result);
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
    private void ShowMultiModelResults(SingleFrameResult mmResult)
    {
        // Alte Masken entfernen
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        // SAM-Masken rendern (gruene Konturen + semi-transparente Fuellung)
        if (mmResult.SamResponse != null)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                mmResult.SamResponse,
                mmResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight);
        }

        // Kalibrierkreis anzeigen
        _showReferenceDn = true;
        RenderReferenceDn();
    }

    /// <summary>
    /// Erstellt CodingEvents aus Multi-Model Befunden (DINO-Detections + SAM-Quantifizierung).
    /// </summary>
    /// <summary>
    /// Multi-Model Findings als CodingEvents — nutzt denselben Resolver-
    /// und Label-Pfad wie der Qwen/Enhanced-Pfad (ResolveFindingCodeForCoding, LookupVsaLabel).
    /// </summary>
    private void AddMultiModelFindingsAsEvents(
        SingleFrameResult mmResult, double captureTimestampSec)
    {
        if (_codingVm == null || _codingSessionService == null) return;

        double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;

        // BCD wird NICHT mehr automatisch erzeugt — nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        for (int i = 0; i < mmResult.QuantifiedMasks.Count; i++)
        {
            var quant = mmResult.QuantifiedMasks[i];
            var dino = i < mmResult.DinoDetections.Count ? mmResult.DinoDetections[i] : null;

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

            var codingEvent = _codingSessionService.AddEvent(entry);
            codingEvent.AiContext = new CodingEventAiContext
            {
                SuggestedCode = code,
                Confidence = gateResult.CompositeConfidence,
                Reason = $"{quant.Label} (DINO {dinoConf:P0})",
                Decision = gateResult.IsGreen
                    ? CodingUserDecision.Accepted
                    : CodingUserDecision.Ignored
            };

            anyAdded = true;
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }
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

        // OSD-Meterstand uebernehmen (Defense-in-Depth: nochmals Plausibilitaet pruefen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            _codingLastOsdMeter = result.MeterReading.Value;
            _codingSessionService?.MoveToMeter(result.MeterReading.Value);
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{result.MeterReading.Value:F2}m (OSD)";
        }

        // ── Findings filtern: VSA-Validierung + Deduplizierung ──
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = result.MeterReading ?? (_codingVm?.CurrentMeter ?? 0);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Schaden"
                : "Kein Schaden";
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
                // Kein VSA-Code ableitbar — Finding verwerfen
                System.Diagnostics.Debug.WriteLine(
                    $"[KI-Filter] Verworfen: Label='{f.Label}' (kein VSA-Code ableitbar)");
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
    /// <summary>Delegiert an VsaCodeResolver.LookupLabel.</summary>
    private static string? LookupVsaLabel(string code) => VsaCodeResolver.LookupLabel(code);

    /// <summary>
    /// Traegt SAM-Quantifizierungsdaten in ProtocolEntry.CodeMeta ein.
    /// Gemeinsam genutzt von Qwen- und Multi-Model-Pfad.
    /// </summary>
    private static void ApplyQuantificationToEntry(
        ProtocolEntry entry, string code, MaskQuantificationService.QuantifiedMask quant)
    {
        if (!string.IsNullOrEmpty(quant.ClockPosition))
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.uhr.von"] = quant.ClockPosition;
        }
        if (quant.HeightMm.HasValue)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.hoehe.mm"] = quant.HeightMm.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (quant.WidthMm.HasValue)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.breite.mm"] = quant.WidthMm.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (quant.CrossSectionReductionPercent is > 0)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.querschnitt.prozent"] =
                quant.CrossSectionReductionPercent.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Schaetzt Severity (1-5) aus SAM-Quantifizierung.
    /// Groesse der Maske relativ zum Rohrquerschnitt.
    /// </summary>
    private static int EstimateSeverityFromQuantification(MaskQuantificationService.QuantifiedMask q)
    {
        // Querschnittsreduktion als primaerer Indikator
        if (q.CrossSectionReductionPercent is > 30) return 5;
        if (q.CrossSectionReductionPercent is > 15) return 4;
        if (q.CrossSectionReductionPercent is > 5) return 3;
        // Einragung
        if (q.IntrusionPercent is > 20) return 4;
        if (q.IntrusionPercent is > 10) return 3;
        // Hoehe relativ (grob: >50mm = ernsthaft)
        if (q.HeightMm is > 50) return 3;
        if (q.HeightMm is > 20) return 2;
        return 2; // Default: leichter Schaden
    }

    /// <summary>Delegiert an VsaCodeResolver.NormalizeClock.</summary>
    private static string? NormalizeClockPosition(string? raw) => VsaCodeResolver.NormalizeClock(raw);

    /// <summary>
    /// Einzige Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Delegiert an VsaCodeResolver (zentrale Utility) + Import-Verfeinerung.
    /// Gibt validen VSA-Code oder null zurueck — nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        // 1. VsaCodeHint normalisieren
        var hinted = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint);
        if (hinted != null)
            return RefineGenericCodeFromImport(hinted, currentMeter) ?? hinted;

        // 2. Label-Heuristik
        var coarse = VsaCodeResolver.InferCodeFromLabel(finding.Label);
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
    private static bool IsAllowedImportFallbackCode(string code)
        => code.StartsWith("BCD", StringComparison.OrdinalIgnoreCase)   // Rohranfang
           || code.StartsWith("BCE", StringComparison.OrdinalIgnoreCase) // Rohrende
           || code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) // Seitl. Anschluss
           || code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase) // Bogen
           || code.StartsWith("BBC", StringComparison.OrdinalIgnoreCase) // Ablagerung
           || code.StartsWith("BDDC", StringComparison.OrdinalIgnoreCase) // Wasserspiegel
           // Strukturschaeden (BA-Gruppe)
           || code.StartsWith("BAB", StringComparison.OrdinalIgnoreCase) // Riss
           || code.StartsWith("BAC", StringComparison.OrdinalIgnoreCase) // Bruch
           || code.StartsWith("BAF", StringComparison.OrdinalIgnoreCase) // Deformation
           || code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase) // Versatz
           || code.StartsWith("BAI", StringComparison.OrdinalIgnoreCase) // Einragender Stutzen
           || code.StartsWith("BAJ", StringComparison.OrdinalIgnoreCase) // Undichtheit
           // Betriebliche Stoerungen (BB-Gruppe)
           || code.StartsWith("BBA", StringComparison.OrdinalIgnoreCase) // Inkrustation
           || code.StartsWith("BBB", StringComparison.OrdinalIgnoreCase) // Wurzeleinwuchs
           || code.StartsWith("BBD", StringComparison.OrdinalIgnoreCase); // Eindringender Boden

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
            bool isStrecke = VsaCodeResolver.IsStreckenschadenCode(code);

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

            var codingEvent = _codingSessionService.AddEvent(entry);
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
                codingEvent.Overlay = new OverlayGeometry
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

    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingLiveAi.IsChecked == true)
        {
            _codingLiveAiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
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
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            if (_codingLiveDetection == null) return;
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

    /// <summary>VLC-Snapshot als PNG-Bytes extrahieren.</summary>
    private async Task<byte[]?> CaptureSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
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
                return await File.ReadAllBytesAsync(snapFile);
            return null;
        }
        finally
        {
            try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch { }
        }
    }

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
            OverlayToolType.PipeBend => "Bogen",
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

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;
            }
        }

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

        var label = VsaCodeResolver.LookupLabel("BCD") ?? "Rohranfang";
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

        var label = VsaCodeResolver.LookupLabel("BCE") ?? "Rohrende";
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
            var config = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);
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














