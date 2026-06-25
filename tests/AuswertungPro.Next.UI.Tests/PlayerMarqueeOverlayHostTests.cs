using AuswertungPro.Next.UI.Player;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMarqueeOverlayHostTests
{
    [Fact]
    public void Host_forwards_show_state_to_marquee_options()
    {
        var integers = new List<(VideoMarqueeOption Option, int Value)>();
        VideoMarqueeOption? stringOption = null;
        string? stringValue = null;
        var host = new PlayerMarqueeOverlayHost(
            setMarqueeInt: (option, value) => integers.Add((option, value)),
            setMarqueeString: (option, value) =>
            {
                stringOption = option;
                stringValue = value;
            });

        host.Show(new PlayerMarqueeOverlayState(1, 2, 3, 4, 5, 6, "text"));

        Assert.Equal(
            new[]
            {
                (VideoMarqueeOption.Enable, 1),
                (VideoMarqueeOption.X, 2),
                (VideoMarqueeOption.Y, 3),
                (VideoMarqueeOption.Size, 4),
                (VideoMarqueeOption.Color, 5),
                (VideoMarqueeOption.Opacity, 6)
            },
            integers);
        Assert.Equal(VideoMarqueeOption.Text, stringOption);
        Assert.Equal("text", stringValue);
    }

    [Fact]
    public void Host_disables_marquee()
    {
        VideoMarqueeOption? optionSeen = null;
        int? valueSeen = null;
        var host = new PlayerMarqueeOverlayHost(
            setMarqueeInt: (option, value) =>
            {
                optionSeen = option;
                valueSeen = value;
            },
            setMarqueeString: (_, _) => { });

        host.Disable();

        Assert.Equal(VideoMarqueeOption.Enable, optionSeen);
        Assert.Equal(PlayerMarqueeOverlayPolicy.DisabledEnable, valueSeen);
    }
}
