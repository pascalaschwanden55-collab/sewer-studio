using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerStatusColorsTests
{
    [Fact]
    public void Status_colors_keep_existing_rgb_values()
    {
        Assert.Equal(Color.FromRgb(0x22, 0xC5, 0x5E), PlayerStatusColors.Success);
        Assert.Equal(Color.FromRgb(0xF5, 0x9E, 0x0B), PlayerStatusColors.Warning);
        Assert.Equal(Color.FromRgb(0xEF, 0x44, 0x44), PlayerStatusColors.Error);
        Assert.Equal(Color.FromRgb(0x94, 0xA3, 0xB8), PlayerStatusColors.Muted);
        Assert.Equal(Color.FromRgb(0x3B, 0x82, 0xF6), PlayerStatusColors.Info);
    }
}
