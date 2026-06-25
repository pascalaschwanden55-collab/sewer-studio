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
            play: () => { });

        Assert.True(host.IsPlaying);
    }

    [Fact]
    public void Host_forwards_pause_and_play_commands()
    {
        bool? pauseSeen = null;
        var playCount = 0;
        var host = new PlayerPlaybackControlHost(
            readIsPlaying: () => false,
            setPause: pause => pauseSeen = pause,
            play: () => playCount++);

        host.SetPause(true);
        host.Play();

        Assert.True(pauseSeen);
        Assert.Equal(1, playCount);
    }
}
