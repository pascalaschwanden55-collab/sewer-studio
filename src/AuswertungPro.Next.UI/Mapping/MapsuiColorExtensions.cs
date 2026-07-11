namespace AuswertungPro.Next.UI.Mapping;

/// <summary>WPF-Farbe nach Mapsui uebersetzen (Mapsui ist nicht theme-faehig, Farben kommen aus der C#-Palette).</summary>
public static class MapsuiColorExtensions
{
    public static Mapsui.Styles.Color ToMapsui(this System.Windows.Media.Color color)
        => new(color.R, color.G, color.B, color.A);
}
