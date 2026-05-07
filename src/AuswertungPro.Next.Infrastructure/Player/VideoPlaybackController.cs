using System;
using System.Collections.Generic;
using System.Diagnostics;
using AuswertungPro.Next.Application.Player;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace AuswertungPro.Next.Infrastructure.Player;

public sealed class VideoPlaybackController : IVideoPlaybackController
{
    public const float MinRate = 0.25f;
    public const float MaxRate = 8.0f;

    private readonly string _videoPath;
    private readonly IVlcSurface _surface;
    private readonly IPlaybackDispatcher _dispatcher;
    private readonly IPlaybackTimer _timer;
    private readonly IPlaybackTimer _scrubTimer;
    private readonly IVideoPlaybackBackend _backend;
    private bool _wasPlayingBeforeDrag;
    private bool _cleanupStarted;

    public VideoPlaybackController(
        string videoPath,
        IVideoPlaybackOptions options,
        IVlcSurface surface,
        IPlaybackDispatcher dispatcher,
        IPlaybackTimer timer,
        IPlaybackTimer scrubTimer)
        : this(videoPath, surface, dispatcher, timer, scrubTimer, new LibVlcPlaybackBackend(options))
    {
    }

    public VideoPlaybackController(
        string videoPath,
        IVlcSurface surface,
        IPlaybackDispatcher dispatcher,
        IPlaybackTimer timer,
        IPlaybackTimer scrubTimer,
        IVideoPlaybackBackend backend)
    {
        _videoPath = string.IsNullOrWhiteSpace(videoPath)
            ? throw new ArgumentException("Video path must not be empty.", nameof(videoPath))
            : videoPath;
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _scrubTimer = scrubTimer ?? throw new ArgumentNullException(nameof(scrubTimer));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));

        _surface.MediaPlayer = _backend.NativePlayer;
        _timer.Tick += OnTimerTick;
        _scrubTimer.Tick += OnScrubTimerTick;
    }

    public event EventHandler? PositionUpdateRequested;

    public event EventHandler? ScrubRequested;

    public object NativePlayer => _backend.NativePlayer;

    public bool IsDragging { get; private set; }

    public bool IsPlaying => _backend.IsPlaying;

    public long LengthMs => _backend.LengthMs;

    public long TimeMs
    {
        get => _backend.TimeMs;
        set => _backend.TimeMs = value;
    }

    public float CurrentRate => NormalizeRate(_backend.Rate);

    public void Play() => Play(_videoPath);

    public void Play(string path)
    {
        _backend.Play(path);
        _timer.Start();
    }

    public void EnsurePlaying()
    {
        if (_backend.State is VideoPlaybackState.Stopped or VideoPlaybackState.Ended)
            Play(_videoPath);
    }

    public void Pause() => _backend.SetPause(true);

    public void Resume()
    {
        EnsurePlaying();
        _backend.SetPause(false);
    }

    public void Stop() => _backend.Stop();

    public bool TogglePlayPause()
    {
        EnsurePlaying();
        var willPause = _backend.IsPlaying;
        _backend.SetPause(willPause);
        return willPause;
    }

    public PlaybackRateChange SetSpeed(float rate)
    {
        var clamped = Math.Clamp(rate, MinRate, MaxRate);
        var result = _backend.SetRate(clamped);
        return new PlaybackRateChange(rate, CurrentRate, result == 0);
    }

    public PlaybackRateChange ChangeSpeed(float delta)
    {
        var current = CurrentRate;
        return SetSpeed(current + delta);
    }

    public void BeginDrag(double sliderValue, double sliderMaximum)
    {
        _wasPlayingBeforeDrag = _backend.IsPlaying;
        IsDragging = true;
        if (_wasPlayingBeforeDrag)
            _backend.SetPause(true);
        ScrubSeekToSlider(sliderValue, sliderMaximum);
    }

    public PlaybackSeekSnapshot PreviewSeek(double sliderValue, double sliderMaximum)
    {
        var snapshot = BuildSeekSnapshot(sliderValue, sliderMaximum);
        if (IsDragging && !_scrubTimer.IsEnabled)
            _scrubTimer.Start();

        return snapshot;
    }

    public PlaybackSeekSnapshot SeekToSlider(double sliderValue, double sliderMaximum)
    {
        var snapshot = BuildSeekSnapshot(sliderValue, sliderMaximum);
        ApplySeek(snapshot.Position);
        return BuildSeekSnapshot(sliderValue, sliderMaximum);
    }

    public PlaybackSeekSnapshot ScrubSeekToSlider(double sliderValue, double sliderMaximum)
    {
        var snapshot = BuildSeekSnapshot(sliderValue, sliderMaximum);
        ApplySeek(snapshot.Position);
        return BuildSeekSnapshot(sliderValue, sliderMaximum);
    }

    public void CompleteDrag(double sliderValue, double sliderMaximum)
    {
        _scrubTimer.Stop();
        SeekToSlider(sliderValue, sliderMaximum);
        IsDragging = false;
        if (_wasPlayingBeforeDrag)
            _backend.SetPause(false);
    }

    public void CancelDrag(double sliderValue, double sliderMaximum)
    {
        _scrubTimer.Stop();
        SeekToSlider(sliderValue, sliderMaximum);
        IsDragging = false;
        if (_wasPlayingBeforeDrag)
            _backend.SetPause(false);
    }

    public bool JumpSeconds(int seconds)
    {
        var length = _backend.LengthMs;
        if (length <= 0)
            return false;

        var newTime = _backend.TimeMs + seconds * 1000L;
        _backend.TimeMs = Math.Clamp(newTime, 0, length);
        return true;
    }

    public PlaybackPositionSnapshot GetPositionSnapshot()
    {
        return new PlaybackPositionSnapshot(
            Math.Max(0, _backend.TimeMs),
            _backend.LengthMs,
            CurrentRate);
    }

    public bool TryGetCurrentTime(out TimeSpan time)
    {
        try
        {
            time = TimeSpan.FromMilliseconds(Math.Max(0, _backend.TimeMs));
            return true;
        }
        catch
        {
            time = default;
            return false;
        }
    }

    public bool TrySeekTo(TimeSpan time)
    {
        try
        {
            EnsurePlaying();
            var ms = (long)Math.Max(0, time.TotalMilliseconds);
            var length = _backend.LengthMs;
            if (length > 0 && ms > length)
                ms = length;
            _backend.TimeMs = ms;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Cleanup()
    {
        if (_cleanupStarted)
            return;

        _cleanupStarted = true;
        Try(_timer.Stop);
        Try(_scrubTimer.Stop);

        try
        {
            if (_backend.IsPlaying)
                _backend.Stop();
            _surface.MediaPlayer = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoPlaybackController] MediaPlayer detach error: {ex.Message}");
        }

        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                _backend.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlaybackController] backend dispose error: {ex.Message}");
            }
        }, PlaybackDispatcherPriority.ApplicationIdle);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        PositionUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnScrubTimerTick(object? sender, EventArgs e)
    {
        _scrubTimer.Stop();
        if (IsDragging)
            ScrubRequested?.Invoke(this, EventArgs.Empty);
    }

    private PlaybackSeekSnapshot BuildSeekSnapshot(double sliderValue, double sliderMaximum)
    {
        if (sliderMaximum <= 0)
            return new PlaybackSeekSnapshot(null, null, 0);

        var position = Math.Clamp(sliderValue / sliderMaximum, 0.0, 1.0);
        var length = _backend.LengthMs;
        if (length > 0)
            return new PlaybackSeekSnapshot((long)(position * length), length, position);

        return new PlaybackSeekSnapshot(null, null, position);
    }

    private void ApplySeek(double position)
    {
        var length = _backend.LengthMs;
        if (length > 0)
            _backend.TimeMs = (long)(position * length);
        else
            _backend.Position = (float)position;
    }

    private static float NormalizeRate(float rate) => rate <= 0f ? 1.0f : rate;

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Cleanup path: best-effort only.
        }
    }

    private sealed class LibVlcPlaybackBackend : IVideoPlaybackBackend
    {
        private readonly LibVLC _libVlc;
        private readonly MediaPlayer _player;

        public LibVlcPlaybackBackend(IVideoPlaybackOptions options)
        {
            Core.Initialize();
            _libVlc = CreateLibVlc(options);
            _player = new MediaPlayer(_libVlc)
            {
                EnableHardwareDecoding = options.EnableHardwareDecoding
            };
        }

        public object NativePlayer => _player;

        public bool IsPlaying => _player.IsPlaying;

        public long LengthMs => _player.Length;

        public long TimeMs
        {
            get => _player.Time;
            set => _player.Time = value;
        }

        public float Position
        {
            get => _player.Position;
            set => _player.Position = value;
        }

        public float Rate => _player.Rate;

        public VideoPlaybackState State => MapState(_player.State);

        public void Play(string path)
        {
            using var media = new VlcMedia(_libVlc, path, FromType.FromPath);
            _player.Play(media);
        }

        public void Play() => _player.Play();

        public void SetPause(bool pause) => _player.SetPause(pause);

        public void Stop() => _player.Stop();

        public int SetRate(float rate) => _player.SetRate(rate);

        public void Dispose()
        {
            try
            {
                _player.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlaybackController] player.Dispose error: {ex.Message}");
            }

            // Do not dispose LibVLC here. In this app, LibVLC.Dispose can throw a
            // native AccessViolation in LibVLCLogUnset on shutdown; process exit
            // cleans up the singleton-like native engine.
            GC.KeepAlive(_libVlc);
        }

        private static LibVLC CreateLibVlc(IVideoPlaybackOptions options)
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

        private static VideoPlaybackState MapState(VLCState state) => state switch
        {
            VLCState.Opening => VideoPlaybackState.Opening,
            VLCState.Buffering => VideoPlaybackState.Buffering,
            VLCState.Playing => VideoPlaybackState.Playing,
            VLCState.Paused => VideoPlaybackState.Paused,
            VLCState.Stopped => VideoPlaybackState.Stopped,
            VLCState.Ended => VideoPlaybackState.Ended,
            VLCState.Error => VideoPlaybackState.Error,
            _ => VideoPlaybackState.NothingSpecial
        };
    }
}
