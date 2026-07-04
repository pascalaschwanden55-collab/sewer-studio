using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerPlaybackControlHostTests
{
    [Fact]
    public void Host_exposes_is_playing()
    {
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => true,
            setPause: _ => { },
            play: () => { },
            stop: () => { },
            readRate: () => 1.0f,
            setRate: _ => 0,
            readVolume: () => 80,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: () => false,
            playPath: _ => { });

        Assert.True(host.IsPlaying);
    }

    [Fact]
    public void Host_forwards_pause_play_and_stop_commands()
    {
        bool? pauseSeen = null;
        var playCount = 0;
        var stopCount = 0;
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: pause => pauseSeen = pause,
            play: () => playCount++,
            stop: () => stopCount++,
            readRate: () => 1.0f,
            setRate: _ => 0,
            readVolume: () => 80,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: () => false,
            playPath: _ => { });

        host.SetPause(true);
        host.Play();
        host.Stop();

        Assert.True(pauseSeen);
        Assert.Equal(1, playCount);
        Assert.Equal(1, stopCount);
    }

    [Fact]
    public void Host_exposes_rate_and_forwards_rate_changes()
    {
        float? rateSeen = null;
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: _ => { },
            play: () => { },
            stop: () => { },
            readRate: () => 1.5f,
            setRate: rate =>
            {
                rateSeen = rate;
                return 0;
            },
            readVolume: () => 80,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: () => false,
            playPath: _ => { });

        var result = host.SetRate(2.0f);

        Assert.Equal(1.5f, host.Rate);
        Assert.Equal(0, result);
        Assert.Equal(2.0f, rateSeen);
    }

    [Fact]
    public void Host_exposes_start_decision_and_forwards_play_path()
    {
        string? pathSeen = null;
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: _ => { },
            play: () => { },
            stop: () => { },
            readRate: () => 1.0f,
            setRate: _ => 0,
            readVolume: () => 80,
            setVolume: _ => { },
            readMute: () => false,
            setMute: _ => { },
            shouldStartPlayback: () => true,
            playPath: path => pathSeen = path);

        host.PlayPath("video.mp4");

        Assert.True(host.ShouldStartPlayback);
        Assert.Equal("video.mp4", pathSeen);
    }

    [Fact]
    public void Host_exposes_volume_and_forwards_mute()
    {
        int? volumeSeen = null;
        bool? muteSeen = null;
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: _ => { },
            play: () => { },
            stop: () => { },
            readRate: () => 1.0f,
            setRate: _ => 0,
            readVolume: () => 55,
            setVolume: volume => volumeSeen = volume,
            readMute: () => true,
            setMute: mute => muteSeen = mute,
            shouldStartPlayback: () => false,
            playPath: _ => { });

        host.SetVolume(120);
        host.SetMute(false);

        Assert.Equal(55, host.Volume);
        Assert.True(host.IsMuted);
        Assert.Equal(100, volumeSeen);
        Assert.False(muteSeen);
    }
}
