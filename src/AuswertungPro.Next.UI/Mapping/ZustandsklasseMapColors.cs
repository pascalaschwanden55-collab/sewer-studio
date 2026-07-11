using System;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.DataPage;
using MapsuiColor = Mapsui.Styles.Color;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Kartenfarben fuer Zustandsklassen, abgeleitet aus der Excel-Palette
/// (ZustandsklasseColorPalette). Bewusst theme-neutral: Mapsui rendert nicht
/// theme-abhaengig, und ein Theme-Wechsel darf keinen Netz-Cache-Rebuild erzwingen.
/// </summary>
public static class ZustandsklasseMapColors
{
    /// <summary>Muted-Ton fuer Haltungen ohne Zustand (heutiger Kartenwert).</summary>
    public static MapsuiColor Unbekannt { get; } = new(61, 77, 99);

    /// <summary>Fuellfarbe der Zustandsklasse "0".."4" (Excel-Palette); null wenn unbekannt.</summary>
    public static MapsuiColor? Fill(string? klasse)
    {
        var key = ZustandsklasseColorPalette.NormalizeClass(klasse);
        return ZustandsklasseColorPalette.HaltungenPalette.TryGetValue(key, out var brush)
            && brush is System.Windows.Media.SolidColorBrush solid
                ? new MapsuiColor(solid.Color.R, solid.Color.G, solid.Color.B)
                : null;
    }

    /// <summary>Konturfarbe = Fuellfarbe * 0.7, fuer Lesbarkeit auf hellem/Satelliten-Hintergrund.</summary>
    public static MapsuiColor? Outline(string? klasse)
    {
        var fill = Fill(klasse);
        return fill is null
            ? null
            : new MapsuiColor(Darken(fill.Value.R), Darken(fill.Value.G), Darken(fill.Value.B));
    }

    /// <summary>Heutige 3-Stufen-Netzfarben als Rueckfall, solange kein 5-Klassen-Wert vorliegt.</summary>
    public static MapsuiColor Fallback3Stufen(ZustandFarbe farbe) => farbe switch
    {
        ZustandFarbe.Gut => new MapsuiColor(22, 163, 74),      // Severity1 hell #16A34A
        ZustandFarbe.Mittel => new MapsuiColor(245, 158, 11),  // Severity3 hell #F59E0B
        ZustandFarbe.Schlecht => new MapsuiColor(220, 38, 38), // Severity5 hell #DC2626
        _ => Unbekannt
    };

    private static int Darken(int channel)
        => (int)Math.Round(channel * 0.7, MidpointRounding.AwayFromZero);
}
