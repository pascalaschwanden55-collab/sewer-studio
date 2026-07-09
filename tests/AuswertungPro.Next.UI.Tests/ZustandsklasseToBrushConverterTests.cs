using System.Globalization;
using System.Windows.Media;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ZustandsklasseToBrushConverterTests
{
    [Theory]
    [InlineData("0", 0xFF, 0x00, 0x00)]
    [InlineData("4", 0x92, 0xD0, 0x50)]
    [InlineData("ohne", 142, 150, 162)]
    [InlineData("unbekannt", 142, 150, 162)]
    public void Convert_liefert_zustandsfarbe_oder_ohne_grau(string value, byte r, byte g, byte b)
    {
        var brush = Assert.IsType<SolidColorBrush>(
            ZustandsklasseToBrushConverter.Instance.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }
}
