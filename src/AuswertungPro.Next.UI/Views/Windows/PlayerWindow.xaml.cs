using System;
using System.Collections.Generic;
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
    private bool _isDetectionInFlight;
    private bool _isManualMarkMode;
    private double _lastDetectionTimestamp;
    private readonly List<LiveFrameFinding> _currentFindings = new();

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

        Loaded += (_, __) =>
        {
            Play(_videoPath);
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
            return _lastOpened._player.TakeSnapshot(0, snapshotPath, 0, 0);
        }
        catch
        {
            return false;
        }
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
            LiveDetectionButton.IsChecked = !(LiveDetectionButton.IsChecked == true);
            LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M)
        {
            ManualMarkButton.IsChecked = !(ManualMarkButton.IsChecked == true);
            ManualMark_Click(ManualMarkButton, new RoutedEventArgs());
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
        UpdateUi();
    }

    private void Play(string path)
    {
        using var media = new Media(_libVlc, path, FromType.FromPath);
        _player.Play(media);
        _timer.Start();
        UpdateRateLabel();
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
        AiRuntimeConfig cfg;
        try { cfg = AiRuntimeConfig.Load(); }
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
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            // Show overlay layer
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            AiStatusBadge.Visibility = Visibility.Visible;
            AiStatusText.Text = $"KI aktiv ({visionModel})";
            AiStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _detectionTimer.Tick += DetectionTimer_Tick;
            _detectionTimer.Start();

            // Run first detection immediately
            _ = RunDetectionAsync();
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

        // Hide overlay layer (unless manual mark mode is still active)
        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;
        _currentFindings.Clear();

        LiveDetectionStatusText.Text = "Gestoppt";
        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
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

        _isDetectionInFlight = true;
        AiStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // amber = working

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                AiStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                return;
            }

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

                AiStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // green = ready
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
                AiStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // red = error
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
            var success = _player.TakeSnapshot(0, tempPath, 640, 0);
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

        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        // Outer guide ring
        var guide = new System.Windows.Shapes.Ellipse
        {
            Width = ringOuter * 2,
            Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        DetectionCanvas.Children.Add(guide);

        // Inner guide ring
        var guideInner = new System.Windows.Shapes.Ellipse
        {
            Width = ringInner * 2,
            Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        DetectionCanvas.Children.Add(guideInner);

        // Clock ticks
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            var x1 = cx + Math.Cos(rad) * (ringInner - 4);
            var y1 = cy + Math.Sin(rad) * (ringInner - 4);
            var x2 = cx + Math.Cos(rad) * (ringOuter + 4);
            var y2 = cy + Math.Sin(rad) * (ringOuter + 4);
            DetectionCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            });
        }

        if (findings.Count == 0)
            return;

        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var finding = findings[i];
            var parsedClock = ParseClockHour(finding.PositionClock);
            var centerDeg = parsedClock.HasValue
                ? -90 + (parsedClock.Value % 12) * 30
                : -90 + i * (360.0 / findings.Count);

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
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };
            DetectionCanvas.Children.Add(sector);

            // Severity dot outside ring
            var rad2 = DegToRad(centerDeg);
            var markerRadius = ringOuter + 2;
            var mx = cx + Math.Cos(rad2) * markerRadius;
            var my = cy + Math.Sin(rad2) * markerRadius;

            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, mx - 4);
            Canvas.SetTop(dot, my - 4);
            DetectionCanvas.Children.Add(dot);

            // Label badge (clickable)
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
                    Text = labelText,
                    FontSize = 11,
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

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        _isManualMarkMode = ManualMarkButton.IsChecked == true;
        if (_isManualMarkMode)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            DetectionOverlayGrid.IsHitTestVisible = true;
            DetectionCanvas.IsHitTestVisible = true;
            DetectionCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            DetectionCanvas.Cursor = Cursors.Arrow;
            DetectionCanvas.IsHitTestVisible = false;
            // Only hide overlay if Live-KI is also not running
            if (!_isDetecting)
            {
                DetectionOverlayGrid.IsHitTestVisible = false;
                DetectionOverlayGrid.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void DetectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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
            null,
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

    // Kalibrierung
    private bool _codingIsCalibrating;
    private NormalizedPoint? _codingCalibStart;

    // Overlay-Vorschau
    private System.Windows.Shapes.Line? _codingPreviewLine;

    // Referenz-DN Toggle
    private bool _showReferenceDn;

    // KI Live-Analyse
    private LiveDetectionService? _codingLiveDetection;
    private CancellationTokenSource? _codingAnalysisCts;
    private bool _codingIsAnalyzing;

    // Live-KI Timer (automatische Analyse alle 5s)
    private DispatcherTimer? _codingLiveAiTimer;
    private QualityGateService? _codingQualityGate;

    // Bestaetigungs-Panel: aktuell wartendes Event
    private CodingEvent? _codingPendingConfirmEvent;
    private QualityGateResult? _codingPendingGateResult;

    // OSD-Meter Timer (liest Meterstand kontinuierlich)
    private DispatcherTimer? _codingOsdTimer;
    private bool _codingOsdReading;

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

        // Video pausieren
        _player.SetPause(true);

        // Session-Services erstellen
        _codingSessionService = new CodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingVm = new CodingSessionViewModel(_codingSessionService, _codingOverlayService);
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += (_, ev) => Dispatcher.Invoke(() => UpdateCodingUi(ev.PropertyName));

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

        // Bestehende Beobachtungen werden jetzt direkt vom CodingSessionService
        // via LoadExistingObservations geladen und vom ViewModel uebernommen
        // (Protocol.Entries + Primaere_Schaeden Fallback fuer PDF-Import)

        // Events-Liste binden
        LstCodingEvents.ItemsSource = _codingVm.Events;

        // UI einblenden
        CodingOverlayCanvas.Visibility = Visibility.Visible;
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(320);
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
        PipeTimeline.Visibility = Visibility.Visible;

        // KI initialisieren + OSD-Timer starten
        InitCodingAi();
        StartCodingOsdTimer();

        // OSD-Badge sofort sichtbar
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";

        // Bestehende Protokoll-Eintraege in Events laden
        LoadExistingProtocolEvents();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Events-Liste.
    /// </summary>
    private void LoadExistingProtocolEvents()
    {
        if (_codingVm == null || _haltungRecord?.Protocol?.Current?.Entries == null) return;

        var entries = _haltungRecord.Protocol.Current.Entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        foreach (var entry in entries)
        {
            // Pruefen ob Event schon existiert (bei erneutem Oeffnen)
            if (_codingVm.Events.Any(ev => ev.Entry.EntryId == entry.EntryId))
                continue;

            _codingVm.Events.Add(new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            });
        }

        RefreshCodingEventsList();
    }

    private void ExitCodingMode()
    {
        if (!_isCodingMode) return;
        _isCodingMode = false;

        // Timer stoppen
        StopCodingOsdTimer();
        _codingLiveAiTimer?.Stop();
        _codingLiveAiTimer = null;

        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts?.Dispose();
        _codingAnalysisCts = null;

        // Bestaetigungs-Panel schliessen
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;

        // UI ausblenden
        CodingOverlayCanvas.Visibility = Visibility.Collapsed;
        CodingOverlayCanvas.Children.Clear();
        CodingSidePanel.Visibility = Visibility.Collapsed;
        CodingSidePanelColumn.Width = new GridLength(0);
        CodingToolbar.Visibility = Visibility.Collapsed;
        PipeTimeline.Visibility = Visibility.Collapsed;
        CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        CodingMeasurementPanel.Visibility = Visibility.Collapsed;
        OsdMeterBadge.Visibility = Visibility.Collapsed;

        // Alle Tool-Buttons unchecken
        BtnCodingCalibrate.IsChecked = false;
        BtnCodingLine.IsChecked = false;
        BtnCodingArc.IsChecked = false;
        BtnCodingRect.IsChecked = false;
        BtnCodingPoint.IsChecked = false;
        BtnCodingStretch.IsChecked = false;
        BtnCodingLiveAi.IsChecked = false;

        // Protokoll uebernehmen wenn Events vorhanden
        if (_codingVm != null && _codingVm.Events.Count > 0 && _haltungRecord != null)
        {
            try
            {
                // Session fortsetzen damit Complete funktioniert
                if (_codingVm.IsPaused)
                    _codingSessionService!.ResumeSession();
                var doc = _codingSessionService!.CompleteSession();
                _haltungRecord.Protocol = doc;
                _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;

                // Primaere Schaeden ins DataGrid uebertragen
                SyncCodingToPrimaryDamages(doc);

                // Protokoll-Vorschau oeffnen (nachtraeglich bearbeitbar)
                ShowCodingProtocolPreview(doc);
            }
            catch { /* Session war evtl. schon beendet */ }
        }

        _codingVm = null;
        _codingSessionService = null;
        _codingOverlayService = null;
        _codingIsCalibrating = false;
        _codingCalibStart = null;
        _codingLastOsdMeter = null;
    }

    private void CodingApply_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null || _haltungRecord == null) return;

        if (_codingVm.Events.Count == 0)
        {
            ShowOverlay("Keine Ereignisse zum Uebernehmen", TimeSpan.FromSeconds(3));
            return;
        }

        // ProtocolDocument aus allen Events aufbauen
        var doc = _haltungRecord.Protocol ?? new ProtocolDocument();
        doc.Current ??= new ProtocolRevision();

        // Bestehende Eintraege markieren, neue hinzufuegen
        var existingIds = new HashSet<Guid>(doc.Current.Entries.Select(e2 => e2.EntryId));
        foreach (var codingEvent in _codingVm.Events)
        {
            if (!existingIds.Contains(codingEvent.Entry.EntryId))
                doc.Current.Entries.Add(codingEvent.Entry);
        }

        _haltungRecord.Protocol = doc;
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;

        // Primaere Schaeden ins DataGrid uebertragen
        SyncCodingToPrimaryDamages(doc);

        ShowOverlay($"{_codingVm.Events.Count} Ereignisse in Primaere Schaeden uebernommen", TimeSpan.FromSeconds(4));
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

    // --- Coding Navigation ---

    private async void CodingNext_Click(object sender, RoutedEventArgs e)
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

    private async void CodingPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        _codingNavPending = true;
        _codingVm.MovePreviousCommand.Execute(null);
        _player.SetPause(true);
        _codingLastOsdMeter = null;
        await CodingReadOsdMeterAsync();
    }

    // --- Coding Werkzeuge ---

    private void CodingToolLine_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingLine, OverlayToolType.Line);
    private void CodingToolArc_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingArc, OverlayToolType.Arc);
    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingRect, OverlayToolType.Rectangle);
    private void CodingToolPoint_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingPoint, OverlayToolType.Point);
    private void CodingToolStretch_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingStretch, OverlayToolType.Stretch);
    private void CodingToolProtractor_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingProtractor, OverlayToolType.Protractor);
    private void CodingToolDnCircle_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingDnCircle, OverlayToolType.DnCircle);
    private void CodingToolRuler_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(BtnCodingRuler, OverlayToolType.Ruler);

    private void CodingRefDn_Click(object sender, RoutedEventArgs e)
    {
        _showReferenceDn = BtnCodingRefDn.IsChecked == true;
        RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
    }

    private void SetCodingTool(ToggleButton activeBtn, OverlayToolType tool)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = false;
        _codingCalibStart = null;
        BtnCodingCalibrate.IsChecked = false;

        // Andere Tool-Buttons unchecken
        foreach (var btn in new[] { BtnCodingLine, BtnCodingArc, BtnCodingRect, BtnCodingPoint, BtnCodingStretch, BtnCodingProtractor, BtnCodingDnCircle, BtnCodingRuler })
        {
            if (btn != activeBtn) btn.IsChecked = false;
        }

        _codingOverlayService.ActiveTool = activeBtn.IsChecked == true ? tool : OverlayToolType.None;

        // Offene Zeichnung verwerfen, damit das naechste Tool sauber startet.
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = BtnCodingCalibrate.IsChecked == true;
        _codingCalibStart = null;
        _codingOverlayService.ActiveTool = OverlayToolType.None;

        // Andere Tool-Buttons unchecken
        foreach (var btn in new[] { BtnCodingLine, BtnCodingArc, BtnCodingRect, BtnCodingPoint, BtnCodingStretch, BtnCodingProtractor, BtnCodingDnCircle, BtnCodingRuler })
            btn.IsChecked = false;

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationHint.Visibility = _codingIsCalibrating ? Visibility.Visible : Visibility.Collapsed;
        TxtCodingCalibHint.Text = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
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
                    _ = AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay);
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
        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            CodingOverlayCanvas.ReleaseMouseCapture();
            ApplyCodingCalibration(_codingCalibStart, norm);
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
            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Live-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                _ = AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay);
        }
        else
        {
            UpdateCodingOverlayInfo(null);
            BtnCodingCreateEvent.IsEnabled = false;
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
            PipeCenter = center
        };
        _codingOverlayService.SetCalibration(cal);

        TxtCodingCalibStatus.Text = $"Kalibriert: {cal.MmPerNormUnit:F1} mm/norm";
        TxtCodingCalibHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        BtnCodingCalibrate.IsChecked = false;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
    }

    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        double w = CodingOverlayCanvas.ActualWidth, h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return new NormalizedPoint(0.5, 0.5);
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
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();

        if (includeManualOverlay && _codingVm?.CurrentOverlay != null)
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

            case OverlayToolType.Protractor:
                RenderProtractorOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.DnCircle:
                RenderDnCircleOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ruler:
                RenderRulerOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering
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

    private void RenderProtractorOverlay(
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

    private void RenderDnCircleOverlay(
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
        if (overlay.ToolType == OverlayToolType.Protractor && overlay.ArcDegrees.HasValue)
            return $"Winkel: {overlay.ArcDegrees.Value:F1}\u00B0";

        if (overlay.ToolType == OverlayToolType.DnCircle)
        {
            var dnParts = new List<string>();
            if (overlay.Q1Mm.HasValue) dnParts.Add($"DN {overlay.Q1Mm.Value:F0}");
            if (overlay.DnRatioPercent.HasValue) dnParts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");
            return string.Join(" ", dnParts);
        }

        if (overlay.ToolType == OverlayToolType.Ruler && overlay.Q1Mm.HasValue)
            return $"Laenge: {overlay.Q1Mm.Value:F1} mm";

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
        TxtCodingArc.Text = overlay.ArcDegrees.HasValue
            ? (overlay.ToolType == OverlayToolType.Protractor
                ? $"Winkel: {overlay.ArcDegrees:F1}\u00B0"
                : $"Bogen: {overlay.ArcDegrees:F0} deg")
            : "Bogen: -";

        CodingMeasurementPanel.Visibility = Visibility.Visible;
        var parts = new List<string>();

        // Werkzeug-spezifische Anzeige
        if (overlay.ToolType == OverlayToolType.Protractor)
        {
            if (overlay.ArcDegrees.HasValue) parts.Add($"Winkel:{overlay.ArcDegrees:F1}\u00B0");
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.DnCircle)
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
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
            if (overlay.ArcDegrees.HasValue) parts.Add($"{overlay.ArcDegrees:F0}deg");
        }
        TxtCodingMeasurement.Text = string.Join("  |  ", parts);
    }

    // --- Coding Code-Auswahl ---

    private async void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null || _serviceProvider == null) return;

        // Video pausieren
        _player.SetPause(true);

        var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

        // Meterstand: OSD bevorzugen, sonst Timeline-Fallback.
        var timelineMeter = _codingVm.CurrentMeter;
        if (_player.Length > 0 && _codingVm.EndMeter > 0)
        {
            timelineMeter = Math.Round((_player.Time / (double)_player.Length) * _codingVm.EndMeter, 2);
        }

        var osdMeter = await CodingReadOsdMeterAsync();
        var meterValue = Math.Round(Math.Max(0, osdMeter ?? _codingLastOsdMeter ?? timelineMeter), 2);

        // Foto 1 automatisch vom aktuellen Frame
        var snapshotSeed = new ProtocolEntry
        {
            Code = "SNAP",
            MeterStart = meterValue,
            Zeit = videoZeit
        };
        var fotoPath = CodingCaptureSnapshot(snapshotSeed);

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            MeterStart = meterValue,
            MeterEnd = meterValue,
            Zeit = videoZeit
        };

        if (fotoPath != null)
            entry.FotoPaths.Add(fotoPath);

        if (_codingVm.CurrentOverlay != null)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
        }

        // VsaCodeExplorerWindow oeffnen (vereintes Fenster: Code + Position + Foto)
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, meterValue, videoZeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoZeit)
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

            // Event hinzufuegen
            _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

            // Nach Meter sortiert anzeigen
            RefreshCodingEventsList();

            // Reset
            _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);
            TxtCodingSelectedCode.Text = "";
            BtnCodingCreateEvent.IsEnabled = false;
            UpdateCodingOverlayInfo(null);
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
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath != null)
            entry.FotoPaths.Add(fotoPath);

        _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

        // Nach Meter sortiert anzeigen
        RefreshCodingEventsList();

        // Reset
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

            _player.TakeSnapshot(0, filePath, 0, 0);

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

        var entry = codingEvent.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
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

            // Meter aktualisieren falls geaendert
            codingEvent.MeterAtCapture = entry.MeterStart ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;

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

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (MessageBox.Show($"Ereignis '{codingEvent.Entry.Code}' loeschen?", "Loeschen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _codingVm?.Events.Remove(codingEvent);
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
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        _codingVm?.AcceptDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            UpdateCodingDefectDetailPanel(_codingVm.SelectedDefect);
            RefreshCodingEventsList();
        }
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm?.SelectedDefect == null) return;

        var ev = _codingVm.SelectedDefect;
        var sp = App.Services as ServiceProvider;
        if (sp == null) return;

        var entry = ev.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _codingVm.VideoPath, _codingVm.CurrentVideoTime) { Owner = this };
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

            _codingVm.EditDefectCommand.Execute(null);
            RefreshCodingEventsList();
            UpdateCodingDefectDetailPanel(ev);
        }
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        _codingVm?.RejectDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            UpdateCodingDefectDetailPanel(_codingVm.SelectedDefect);
            RefreshCodingEventsList();
        }
    }

    /// <summary>Defekt-Detail-Panel mit Werten des ausgewaehlten Events befuellen.</summary>
    private void UpdateCodingDefectDetailPanel(CodingEvent ev)
    {
        CodingDefectDetailPanel.Visibility = Visibility.Visible;

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

        // Aktionsbuttons nur bei offenen KI-Events
        CodingDefectActionGrid.Visibility = ev.AiContext != null &&
            status is DefectStatus.Pending or DefectStatus.ReviewRequired
            ? Visibility.Visible
            : Visibility.Collapsed;
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

            // Zone-Dot einfaerben
            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                if (ev.AiContext != null)
                    zoneDot.Fill = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
                else
                    zoneDot.Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Manuell = blau
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
        if (entries == null || entries.Count == 0) return;

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

    private void InitCodingAi()
    {
        try
        {
            var config = AiRuntimeConfig.Load();
            if (!config.Enabled)
            {
                TxtCodingAiStatus.Text = "KI deaktiviert";
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = config.CreateOllamaClient();
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingQualityGate = new QualityGateService();
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            TxtCodingAiStatus.Text = "KI Bereit";
        }
        catch (Exception ex)
        {
            TxtCodingAiStatus.Text = $"Fehler: {ex.Message}";
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

    private async void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        if (_codingLiveDetection == null || _codingIsAnalyzing) return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            BtnCodingAnalyze.IsEnabled = false;
            TxtCodingAiStatus.Text = "Analysiere...";
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));

            var pngBytes = await CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                TxtCodingAiStatus.Text = "Frame nicht extrahierbar";
                CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                return;
            }

            var result = await _codingLiveDetection.AnalyzeFrameAsync(
                pngBytes, _player.Time / 1000.0, _codingAnalysisCts.Token);

            ShowCodingAiResults(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TxtCodingAiStatus.Text = $"Fehler: {ex.Message}";
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        }
        finally
        {
            _codingIsAnalyzing = false;
            BtnCodingAnalyze.IsEnabled = true;
        }
    }

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            TxtCodingAiStatus.Text = $"Fehler: {result.Error}";
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // OSD-Meterstand uebernehmen
        if (result.MeterReading.HasValue && _codingVm != null)
        {
            _codingLastOsdMeter = result.MeterReading.Value;
            _codingSessionService?.MoveToMeter(result.MeterReading.Value);
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{result.MeterReading.Value:F2}m (OSD)";
        }

        if (result.Findings.Count == 0)
        {
            TxtCodingAiStatus.Text = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Schaden"
                : "Kein Schaden";
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            CodingFindingsList.ItemsSource = null;
            return;
        }

        TxtCodingAiStatus.Text = result.MeterReading.HasValue
            ? $"OSD {result.MeterReading.Value:F2}m \u2013 {result.Findings.Count} Befund(e)"
            : $"{result.Findings.Count} Befund(e)";
        CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        CodingFindingsList.ItemsSource = result.Findings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result);
    }

    /// <summary>
    /// KI-Befunde als CodingEvents eintragen â€" mit QualityGate-Ampelsystem.
    /// Gruen: auto-akzeptiert. Gelb/Rot: Video pausieren, Bestaetigung verlangen.
    /// </summary>
    private void AddAiFindingsAsEvents(LiveDetection result)
    {
        if (_codingVm == null || _codingSessionService == null) return;
        if (result.Findings.Count == 0) return;

        double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;
        CodingEvent? firstUnsure = null;
        QualityGateResult? firstUnsureGate = null;

        foreach (var finding in result.Findings)
        {
            string code = finding.VsaCodeHint ?? "???";

            // Duplikat-Check: gleicher Code innerhalb +/-0.3m bereits vorhanden?
            bool isDuplicate = _codingVm.Events.Any(e =>
                string.Equals(e.Entry.Code, code, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(e.MeterAtCapture - meter) < 0.3);
            if (isDuplicate) continue;

            // QualityGate: Severity -> EvidenceVector -> Ampel
            var evidence = new EvidenceVector(
                QwenVisionConf: finding.Severity / 5.0,
                PlausibilityScore: !string.IsNullOrWhiteSpace(finding.VsaCodeHint) ? 0.8 : 0.3
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(
                    finding.Severity / 5.0,
                    finding.Severity >= 4 ? TrafficLight.Green : TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Fallback");

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = finding.Label,
                MeterStart = meter,
                Zeit = videoTime
            };

            if (!string.IsNullOrWhiteSpace(finding.PositionClock))
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
                entry.CodeMeta.Parameters["vsa.uhr.von"] = finding.PositionClock!;
            }

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
            TxtCodingAiStatus.Text = "Live-KI aktiv";
        }
        else
        {
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;
            TxtCodingAiStatus.Text = "KI Bereit";
        }
    }

    private async void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
    {
        // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
        if (_codingIsAnalyzing) return;
        if (_codingLiveDetection == null) return;
        if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput) return;

        // Nur analysieren wenn Video tatsaechlich laeuft
        if (_player == null || !_player.IsPlaying) return;

        // Frame capturen und analysieren (wie manueller Button)
        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            TxtCodingAiStatus.Text = "Live: Analysiere...";

            var pngBytes = await CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0) return;

            var result = await _codingLiveDetection.AnalyzeFrameAsync(
                pngBytes, _player.Time / 1000.0, _codingAnalysisCts.Token);

            ShowCodingAiResults(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TxtCodingAiStatus.Text = $"Live-Fehler: {ex.Message}";
        }
        finally
        {
            _codingIsAnalyzing = false;
        }
    }

    /// <summary>VLC-Snapshot als PNG-Bytes extrahieren.</summary>
    private async Task<byte[]?> CaptureSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
        try
        {
            _player.TakeSnapshot(0, snapFile, 0, 0);
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
        CodingAiDot.Fill = new SolidColorBrush(ampelColor);

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
            _codingPendingConfirmEvent.AiContext.Decision = CodingUserDecision.Accepted;

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

        // Video weiterlaufen lassen (wenn Live-KI aktiv)
        if (BtnCodingLiveAi.IsChecked == true)
            _player.SetPause(false);

        // Globale Ampel zuruecksetzen
        CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        TxtCodingAiStatus.Text = BtnCodingLiveAi.IsChecked == true ? "Live-KI aktiv" : "KI Bereit";
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
            OverlayToolType.Protractor => "Winkel",
            OverlayToolType.DnCircle => "DN-Kreis",
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
                        var rect = new Rectangle
                        {
                            Width = Math.Abs(rw),
                            Height = Math.Abs(rh),
                            Stroke = stroke,
                            StrokeThickness = 2.5,
                            StrokeDashArray = new DoubleCollection { 5, 3 },
                            Fill = amberFill,
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(rect, Math.Min(rx, rx + rw));
                        Canvas.SetTop(rect, Math.Min(ry, ry + rh));
                        CodingOverlayCanvas.Children.Add(rect);

                        var label = new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(ev.Entry.Code) ? "?" : ev.Entry.Code,
                            FontSize = 10,
                            Foreground = stroke,
                            Background = new SolidColorBrush(Color.FromArgb(180, 17, 19, 24)),
                            Padding = new Thickness(3, 1, 3, 1),
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(label, Math.Min(rx, rx + rw));
                        Canvas.SetTop(label, Math.Min(ry, ry + rh) - 16);
                        CodingOverlayCanvas.Children.Add(label);
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

                case OverlayToolType.Protractor:
                    RenderProtractorOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.DnCircle:
                    RenderDnCircleOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;
            }
        }
    }

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        if (_codingLiveDetection == null || _codingIsAnalyzing) return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            CodingAiDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            TxtCodingAiStatus.Text = "Analyse: markierte Stelle...";

            var pngBytes = await CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0) return;

            var result = await _codingLiveDetection.AnalyzeFrameAsync(
                pngBytes, _player.Time / 1000.0, _codingAnalysisCts.Token);

            ShowCodingAiResults(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TxtCodingAiStatus.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _codingIsAnalyzing = false;
        }
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
                _player.TakeSnapshot(0, snapFile, 0, 0);
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













