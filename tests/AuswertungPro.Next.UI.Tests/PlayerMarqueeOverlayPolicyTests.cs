using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMarqueeOverlayPolicyTests
{
    [Fact]
    public void BuildShow_returns_standard_vlc_marquee_settings()
    {
        var state = InvokeBuildShow("Foto gespeichert");

        Assert.Equal(1, state.Enable);
        Assert.Equal(16, state.X);
        Assert.Equal(16, state.Y);
        Assert.Equal(24, state.Size);
        Assert.Equal(0xFFFFFF, state.Color);
        Assert.Equal(200, state.Opacity);
        Assert.Equal("Foto gespeichert", state.Text);
    }

    [Fact]
    public void DisabledEnable_returns_zero_for_clearing_marquee()
    {
        Assert.Equal(0, PlayerMarqueeOverlayPolicy.DisabledEnable);
    }

    private static (
        int Enable,
        int X,
        int Y,
        int Size,
        int Color,
        int Opacity,
        string Text) InvokeBuildShow(string text)
    {
        var result = PlayerMarqueeOverlayPolicy.BuildShow(text);
        return (
            result.Enable,
            result.X,
            result.Y,
            result.Size,
            result.Color,
            result.Opacity,
            result.Text);
    }
}
