using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;

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

public partial class PlayerWindow : Window
{
    private const float MinRate = 0.25f;
    private const float MaxRate = 8.0f;

    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly DispatcherTimer _timer;
    private bool _isDragging;
    private readonly string _videoPath;
    private readonly PlayerWindowOptions _options;
    private readonly string? _initialOverlayText;

    private static PlayerWindow? _lastOpened;

    public PlayerWindow(string videoPath, PlayerWindowOptions? options = null, string? initialOverlayText = null)
    {
        InitializeComponent();

        _videoPath = videoPath;
        _options = PlayerWindowOptions.Normalize(options);
        _initialOverlayText = initialOverlayText;
        _lastOpened = this;

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

        PositionSlider.AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler((_, __) =>
            {
                _isDragging = true;
                UpdateSeekPreview();
            }),
            true);
        PositionSlider.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler((_, __) =>
            {
                SeekToSlider();
                _isDragging = false;
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
                SeekToSlider();
                _isDragging = false;
            }
        };

        Loaded += (_, __) =>
        {
            Play(_videoPath);
            if (!string.IsNullOrWhiteSpace(_initialOverlayText))
                ShowOverlay(_initialOverlayText!, TimeSpan.FromSeconds(6));

            Focusable = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }));
        };

        Closed += (_, __) =>
        {
            if (ReferenceEquals(_lastOpened, this))
                _lastOpened = null;
            Cleanup();
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
    }

    private static string FormatMs(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
    }

    private void Play_Click(object sender, RoutedEventArgs e) => _player.SetPause(false);

    private void Pause_Click(object sender, RoutedEventArgs e) => _player.SetPause(true);

    private void Stop_Click(object sender, RoutedEventArgs e) => _player.Stop();

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
    }
}
