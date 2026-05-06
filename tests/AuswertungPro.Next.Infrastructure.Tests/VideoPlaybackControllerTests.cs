using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Player;
using AuswertungPro.Next.Infrastructure.Player;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

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
