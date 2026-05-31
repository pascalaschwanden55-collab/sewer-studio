using AuswertungPro.Next.UI.LiveControl;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Tests;

public class LiveControlCommandTests
{
    [Theory]
    [InlineData("gelb", 0xFF, 0xF5, 0x9E, 0x0B)]
    [InlineData("yellow", 0xFF, 0xF5, 0x9E, 0x0B)]
    [InlineData("#112233", 0xFF, 0x11, 0x22, 0x33)]
    [InlineData("#80112233", 0x80, 0x11, 0x22, 0x33)]
    public void TryParseColor_AkzeptiertNamenUndHex(string input, byte a, byte r, byte g, byte b)
    {
        var ok = LiveControlColorParser.TryParse(input, out var color);

        Assert.True(ok);
        Assert.Equal(Color.FromArgb(a, r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("../../../x")]
    public void TryParseColor_LehntUngueltigeWerteAb(string input)
    {
        var ok = LiveControlColorParser.TryParse(input, out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("AccentBrush")]
    [InlineData("PrimaryButton.Background")]
    [InlineData("button-1")]
    public void IsSafeResourceKey_AkzeptiertNormaleKeys(string key)
    {
        Assert.True(LiveControlRequestValidator.IsSafeResourceKey(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../AccentBrush")]
    [InlineData("Accent Brush")]
    [InlineData("AccentBrush;delete")]
    public void IsSafeResourceKey_LehntUnsichereKeysAb(string key)
    {
        Assert.False(LiveControlRequestValidator.IsSafeResourceKey(key));
    }
}
