using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ZustandsklasseColorPaletteTests
{
    [Theory]
    [InlineData("0", 0xFF, 0x00, 0x00)] // rot
    [InlineData("4", 0x92, 0xD0, 0x50)] // gruen
    public void TryGetBackground_maps_known_classes(string value, byte r, byte g, byte b)
    {
        var brush = Assert.IsType<SolidColorBrush>(ZustandsklasseColorPalette.TryGetBackground(value));
        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }

    [Theory]
    [InlineData("3.4", "3")] // rundet auf gueltige Klasse
    [InlineData("2,0", "2")] // Komma-Dezimal
    public void TryGetBackground_rounds_decimal_classes(string value, string equivalentInteger)
    {
        Assert.Equal(
            ((SolidColorBrush)ZustandsklasseColorPalette.TryGetBackground(equivalentInteger)!).Color,
            ((SolidColorBrush)ZustandsklasseColorPalette.TryGetBackground(value)!).Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7")]
    [InlineData(null)]
    public void TryGetBackground_returns_null_for_unknown(string? value)
    {
        Assert.Null(ZustandsklasseColorPalette.TryGetBackground(value));
    }
}
