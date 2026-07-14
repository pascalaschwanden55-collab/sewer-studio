using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackControllerTests
{
    [Fact]
    public void TrySeekTo_starts_playback_clamps_time_and_updates_coding_ui_in_order()
    {
        var calls = new List<string>();
        long currentTimeMs = 2_000;
        var controller = CreateController(
            calls,
            readTime: () => currentTimeMs,
            readLength: () => 10_000,
            seek: value =>
            {
                currentTimeMs = value;
                calls.Add($"seek:{value}");
            },
            shouldStartPlayback: () => true,
            isCodingMode: () => true);

        var success = controller.TrySeekTo(TimeSpan.FromSeconds(12));

        Assert.True(success);
        Assert.Equal(
            [
                "play:video.mp4",
                "timer",
                "rate",
                "seek:10000",
                "ui:10000/10000",
                "rate",
                "coding"
            ],
            calls);
    }

    [Fact]
    public void JumpSeconds_moves_timeline_clears_overlays_and_updates_ui()
    {
        var calls = new List<string>();
        long currentTimeMs = 2_000;
        var controller = CreateController(
            calls,
            readTime: () => currentTimeMs,
            readLength: () => 10_000,
            seek: value =>
            {
                currentTimeMs = value;
                calls.Add($"seek:{value}");
            });

        var success = controller.JumpSeconds(5);

        Assert.True(success);
        Assert.Equal(
            ["seek:7000", "clear", "ui:7000/10000", "rate"],
            calls);
    }

    [Fact]
    public void UpdateUi_while_dragging_does_not_overwrite_preview()
    {
        var calls = new List<string>();
        var controller = CreateController(
            calls,
            readTime: () => 3_000,
            readLength: () => 10_000,
            seek: _ => { },
            isDragging: () => true,
            isCodingMode: () => true);

        controller.UpdateUi();

        Assert.Empty(calls);
    }

    [Fact]
    public void Playback_buttons_keep_resume_pause_and_stop_side_effects()
    {
        var calls = new List<string>();
        var controller = CreateController(
            calls,
            readTime: () => 0,
            readLength: () => 10_000,
            seek: _ => { });

        controller.Resume();
        controller.Pause();
        controller.Stop();

        Assert.Equal(
            [
                "pause:False", "rate", "clear",
                "pause:True", "rate",
                "stop", "rate"
            ],
            calls);
    }

    private static PlayerPlaybackController CreateController(
        List<string> calls,
        Func<long> readTime,
        Func<long> readLength,
        Action<long> seek,
        Func<bool>? shouldStartPlayback = null,
        Func<bool>? isDragging = null,
        Func<bool>? isCodingMode = null)
    {
        var playbackHost = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: pause => calls.Add($"pause:{pause}"),
            play: () => calls.Add("play-current"),
            stop: () => calls.Add("stop"),
            readRate: () => 1.0f,
            setRate: _ => 0,
            readVolume: () => 100,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: shouldStartPlayback ?? (() => false),
            playPath: path => calls.Add($"play:{path}"));
        var timelineHost = new PlayerTimelineHost(
            () => readTime(),
            () => readLength(),
            seek);

        return new PlayerPlaybackController(
            "video.mp4",
            playbackHost,
            timelineHost,
            isDragging ?? (() => false),
            isCodingMode ?? (() => false),
            new PlayerPlaybackControllerActions(
                StartUpdateTimer: () => calls.Add("timer"),
                UpdateRateLabel: () => calls.Add("rate"),
                ClearDetectionOverlays: () => calls.Add("clear"),
                ApplyPlaybackState: (current, duration) => calls.Add($"ui:{current}/{duration}"),
                UpdateCodingCurrentCode: () => calls.Add("coding")));
    }
}
