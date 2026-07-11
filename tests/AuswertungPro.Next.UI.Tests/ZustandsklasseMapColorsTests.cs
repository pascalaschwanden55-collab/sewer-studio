using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;
using MapsuiColor = Mapsui.Styles.Color;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Kartenfarben werden aus der Excel-Zustandsklassen-Palette abgeleitet
/// (Mapsui ist nicht theme-faehig, deshalb feste Werte).
/// </summary>
public sealed class ZustandsklasseMapColorsTests
{
    [Fact]
    public void Fill_matches_excel_palette()
    {
        Assert.Equal(new MapsuiColor(0xFF, 0x00, 0x00), ZustandsklasseMapColors.Fill("0"));
        Assert.Equal(new MapsuiColor(0xFF, 0x66, 0x00), ZustandsklasseMapColors.Fill("1"));
        Assert.Equal(new MapsuiColor(0xFF, 0xFF, 0x00), ZustandsklasseMapColors.Fill("2"));
        Assert.Equal(new MapsuiColor(0xAE, 0xB1, 0x35), ZustandsklasseMapColors.Fill("3"));
        Assert.Equal(new MapsuiColor(0x92, 0xD0, 0x50), ZustandsklasseMapColors.Fill("4"));
        Assert.Null(ZustandsklasseMapColors.Fill("7"));
        Assert.Null(ZustandsklasseMapColors.Fill(null));
    }

    [Fact]
    public void Outline_is_darkened_fill()
    {
        // Kontur = Fuellfarbe * 0.7 (gerundet), fuer Lesbarkeit auf Satellitenbild.
        var outline = ZustandsklasseMapColors.Outline("0");
        Assert.True(outline.HasValue);
        Assert.Equal(179, outline.Value.R); // round(255 * 0.7) = 178.5 -> 179 (kaufmaennisch)
        Assert.Equal(0, outline.Value.G);
        Assert.Equal(0, outline.Value.B);
    }

    [Fact]
    public void Fallback3Stufen_keeps_previous_map_colors()
    {
        // Heutige Netzfarben (Severity1/3/5 hell + Muted) bleiben als 3-Stufen-Rueckfall erhalten.
        Assert.Equal(new MapsuiColor(22, 163, 74), ZustandsklasseMapColors.Fallback3Stufen(ZustandFarbe.Gut));
        Assert.Equal(new MapsuiColor(245, 158, 11), ZustandsklasseMapColors.Fallback3Stufen(ZustandFarbe.Mittel));
        Assert.Equal(new MapsuiColor(220, 38, 38), ZustandsklasseMapColors.Fallback3Stufen(ZustandFarbe.Schlecht));
        Assert.Equal(new MapsuiColor(61, 77, 99), ZustandsklasseMapColors.Fallback3Stufen(ZustandFarbe.Unbekannt));
    }

    [Fact]
    public void Unbekannt_is_muted_map_color()
    {
        Assert.Equal(new MapsuiColor(61, 77, 99), ZustandsklasseMapColors.Unbekannt);
    }

    [Fact]
    public void ToMapsui_preserves_rgba()
    {
        var wpf = System.Windows.Media.Color.FromArgb(200, 10, 20, 30);
        var mapsui = wpf.ToMapsui();
        Assert.Equal(10, mapsui.R);
        Assert.Equal(20, mapsui.G);
        Assert.Equal(30, mapsui.B);
        Assert.Equal(200, mapsui.A);
    }
}
