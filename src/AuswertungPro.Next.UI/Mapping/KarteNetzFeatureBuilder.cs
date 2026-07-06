using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;
using Mapsui.Nts;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Baut aus einer projizierten Haltungs-Geometrie ein Mapsui-Feature (Linie + Zustandsfarbe).
/// Bewusst aus dem KarteViewModel ausgelagert, damit der Netz-Cache es ohne ViewModel nutzen kann.
/// </summary>
public static class KarteNetzFeatureBuilder
{
    public static GeometryFeature Build(
        ProjectedHaltungGeometry hg,
        IReadOnlyDictionary<string, int?> kondition,
        bool invertiert)
    {
        var coords = hg.Points.Select(p => new Coordinate(p.X, p.Y)).ToArray();

        var farbe = ZustandColorMapper.Map(
            kondition.TryGetValue(hg.Haltungsname, out var k) ? k : null,
            invertiert);

        // Farben spiegeln die Theme-Severity-Brushes (Severity1/3/5, Muted), damit Netzlinien
        // und Kartenlegende dieselbe Farbsprache nutzen. Feste Hex-Werte, weil Mapsui nicht
        // theme-abhaengig ist.
        var color = farbe switch
        {
            ZustandFarbe.Gut => new Color(22, 163, 74),      // Severity1 #16A34A
            ZustandFarbe.Mittel => new Color(245, 158, 11),  // Severity3 #F59E0B
            ZustandFarbe.Schlecht => new Color(220, 38, 38), // Severity5 #DC2626
            _ => new Color(61, 77, 99),                      // MutedBrush #3D4D63
        };

        var feature = new GeometryFeature { Geometry = new LineString(coords) };
        feature["Haltungsname"] = hg.Haltungsname;
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, 4) });
        return feature;
    }
}
