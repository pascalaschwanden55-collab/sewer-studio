using Mapsui.Nts;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Baut aus einem Schacht (Abwasserknoten) ein Mapsui-Punkt-Feature mit KREIS-Symbol
/// (wie der QGIS-Schaechte-Layer): dunkler Rand, heller Fuellkreis. Koordinaten bereits
/// nach WebMercator projiziert. Bewusst eigene Einheit, damit der Netz-Cache es nutzen kann.
/// </summary>
public static class KarteSchachtFeatureBuilder
{
    // Blauer Kreis mit dunklem Rand — deutlich abgesetzt von den Netzlinien (Zustandsfarben).
    private static readonly Color Fuellung = new(219, 234, 254);   // hellblau #DBEAFE
    private static readonly Color Rand = new(30, 58, 138);         // dunkelblau #1E3A8A

    public static GeometryFeature Build(string bezeichnung, double mercatorX, double mercatorY)
    {
        var feature = new GeometryFeature { Geometry = new Point(mercatorX, mercatorY) };
        feature["Schachtnummer"] = bezeichnung;
        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.55,
            Fill = new Brush(Fuellung),
            Outline = new Pen(Rand, 2),
        });
        return feature;
    }
}
