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
            stop: () => { });

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
            stop: () => stopCount++);

        host.SetPause(true);
        host.Play();
        host.Stop();

        Assert.True(pauseSeen);
        Assert.Equal(1, playCount);
        Assert.Equal(1, stopCount);
    }
}
