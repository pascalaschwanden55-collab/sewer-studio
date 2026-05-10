using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Player;
using AuswertungPro.Next.Infrastructure.Player;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

[Trait("Category", "Unit")]
public sealed class VideoPlaybackControllerTests
{
    [Fact]
    public void Constructor_AttachesNativePlayerToSurface()
    {
        var ctx = CreateController();

        Assert.Same(ctx.Backend.NativePlayer, ctx.Surface.MediaPlayer);
    }

    [Fact]
    public void SetSpeed_ClampsRateAndReportsUnsupportedBackendResult()
    {
        var ctx = CreateController();
        ctx.Backend.SetRateResult = 1;

        var change = ctx.Controller.SetSpeed(99f);

        Assert.Equal(VideoPlaybackController.MaxRate, ctx.Backend.LastSetRate);
        Assert.False(change.IsSupported);
        Assert.Equal(99f, change.RequestedRate);
    }

    [Fact]
    public void JumpSeconds_ClampsToVideoBounds()
    {
        var ctx = CreateController();
        ctx.Backend.LengthMs = 10_000;
        ctx.Backend.TimeMs = 9_000;

        var jumpedForward = ctx.Controller.JumpSeconds(5);
        var jumpedBackward = ctx.Controller.JumpSeconds(-20);

        Assert.True(jumpedForward);
        Assert.True(jumpedBackward);
        Assert.Equal(0, ctx.Backend.TimeMs);
    }

    [Fact]
    public void Dragging_PausesSchedulesScrubAndResumesPreviousPlayback()
    {
        var ctx = CreateController();
        ctx.Backend.LengthMs = 10_000;
        ctx.Backend.IsPlaying = true;
        var scrubEvents = 0;
        ctx.Controller.ScrubRequested += (_, _) =>
        {
            scrubEvents++;
            ctx.Controller.ScrubSeekToSlider(700, 1000);
        };

        ctx.Controller.BeginDrag(500, 1000);
        var preview = ctx.Controller.PreviewSeek(700, 1000);
        ctx.ScrubTimer.Fire();
        ctx.Controller.CompleteDrag(700, 1000);

        Assert.False(ctx.Backend.PauseStates[0]);
        Assert.Equal(7_000, preview.CurrentTimeMs);
        Assert.Equal(1, scrubEvents);
        Assert.Equal(7_000, ctx.Backend.TimeMs);
        Assert.True(ctx.Backend.IsPlaying);
    }

    [Fact]
    public void LengthChanged_PropagatesFromBackendToController()
    {
        var ctx = CreateController();
        long received = 0;
        var fired = 0;
        ctx.Controller.LengthChanged += (_, lengthMs) => { received = lengthMs; fired++; };

        ctx.Backend.RaiseLengthChanged(42_000);

        Assert.Equal(1, fired);
        Assert.Equal(42_000, received);
    }

    [Fact]
    public void EncounteredError_PropagatesFromBackendToController()
    {
        var ctx = CreateController();
        string? received = null;
        ctx.Controller.EncounteredError += (_, msg) => received = msg;

        ctx.Backend.RaiseEncounteredError("boom");

        Assert.Equal("boom", received);
    }

    [Fact]
    public void FirstPlayingOnce_PropagatesEachBackendFire()
    {
        // Backend selbst stellt Once-Garantie sicher (siehe LibVlcPlaybackBackend
        // mit _firstPlayingFired-Flag); der Controller reicht durch und
        // multipliziert die Events nicht.
        var ctx = CreateController();
        var fired = 0;
        ctx.Controller.FirstPlayingOnce += (_, _) => fired++;

        ctx.Backend.RaiseFirstPlayingOnce();
        ctx.Backend.RaiseFirstPlayingOnce();

        Assert.Equal(2, fired);
    }

    [Fact]
    public void Cleanup_DetachesBackendLifecycleEvents()
    {
        var ctx = CreateController();
        var lengthFired = 0;
        var errorFired = 0;
        var firstPlayFired = 0;
        ctx.Controller.LengthChanged += (_, _) => lengthFired++;
        ctx.Controller.EncounteredError += (_, _) => errorFired++;
        ctx.Controller.FirstPlayingOnce += (_, _) => firstPlayFired++;

        ctx.Controller.Cleanup();

        ctx.Backend.RaiseLengthChanged(1);
        ctx.Backend.RaiseEncounteredError("late");
        ctx.Backend.RaiseFirstPlayingOnce();

        Assert.Equal(0, lengthFired);
        Assert.Equal(0, errorFired);
        Assert.Equal(0, firstPlayFired);
    }

    [Fact]
    public void Cleanup_StopsDetachesAndDefersBackendDispose()
    {
        var ctx = CreateController();
        ctx.Backend.IsPlaying = true;
        ctx.Surface.MediaPlayer = ctx.Backend.NativePlayer;
        ctx.Timer.Start();
        ctx.ScrubTimer.Start();

        ctx.Controller.Cleanup();
        ctx.Controller.Cleanup();

        Assert.False(ctx.Timer.IsEnabled);
        Assert.False(ctx.ScrubTimer.IsEnabled);
        Assert.True(ctx.Backend.StopCalled);
        Assert.Null(ctx.Surface.MediaPlayer);
        Assert.False(ctx.Backend.Disposed);
        Assert.Single(ctx.Dispatcher.Pending);

        ctx.Dispatcher.RunAll();

        Assert.True(ctx.Backend.Disposed);
    }

    private static TestContext CreateController()
    {
        var surface = new FakeSurface();
        var dispatcher = new FakeDispatcher();
        var timer = new FakeTimer();
        var scrubTimer = new FakeTimer();
        var backend = new FakeBackend();
        var controller = new VideoPlaybackController(
            @"C:\video.mp4",
            surface,
            dispatcher,
            timer,
            scrubTimer,
            backend);

        return new TestContext(controller, surface, dispatcher, timer, scrubTimer, backend);
    }

    private sealed record TestContext(
        VideoPlaybackController Controller,
        FakeSurface Surface,
        FakeDispatcher Dispatcher,
        FakeTimer Timer,
        FakeTimer ScrubTimer,
        FakeBackend Backend);

    private sealed class FakeSurface : IVlcSurface
    {
        public object? MediaPlayer { get; set; }
    }

    private sealed class FakeDispatcher : IPlaybackDispatcher
    {
        public List<Action> Pending { get; } = new();

        public void BeginInvoke(Action action, PlaybackDispatcherPriority priority)
        {
            Pending.Add(action);
        }

        public void RunAll()
        {
            foreach (var action in Pending.ToArray())
                action();
            Pending.Clear();
        }
    }

    private sealed class FakeTimer : IPlaybackTimer
    {
        public event EventHandler? Tick;

        public bool IsEnabled { get; private set; }

        public void Start() => IsEnabled = true;

        public void Stop() => IsEnabled = false;

        public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeBackend : IVideoPlaybackBackend
    {
        public event EventHandler<long>? LengthChanged;
        public event EventHandler<string>? EncounteredError;
        public event EventHandler? FirstPlayingOnce;

        public void RaiseLengthChanged(long lengthMs) => LengthChanged?.Invoke(this, lengthMs);

        public void RaiseEncounteredError(string message) => EncounteredError?.Invoke(this, message);

        public void RaiseFirstPlayingOnce() => FirstPlayingOnce?.Invoke(this, EventArgs.Empty);

        public object NativePlayer { get; } = new();

        public bool IsPlaying { get; set; }

        public long LengthMs { get; set; }

        public long TimeMs { get; set; }

        public float Position { get; set; }

        public float Rate { get; private set; } = 1.0f;

        public VideoPlaybackState State { get; set; } = VideoPlaybackState.Stopped;

        public int SetRateResult { get; set; }

        public float LastSetRate { get; private set; }

        public bool StopCalled { get; private set; }

        public bool Disposed { get; private set; }

        public List<bool> PauseStates { get; } = new();

        public void Play(string path)
        {
            IsPlaying = true;
            State = VideoPlaybackState.Playing;
        }

        public void Play()
        {
            IsPlaying = true;
            State = VideoPlaybackState.Playing;
        }

        public void SetPause(bool pause)
        {
            PauseStates.Add(!pause);
            IsPlaying = !pause;
            State = pause ? VideoPlaybackState.Paused : VideoPlaybackState.Playing;
        }

        public void Stop()
        {
            StopCalled = true;
            IsPlaying = false;
            State = VideoPlaybackState.Stopped;
        }

        public int SetRate(float rate)
        {
            LastSetRate = rate;
            Rate = rate;
            return SetRateResult;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
