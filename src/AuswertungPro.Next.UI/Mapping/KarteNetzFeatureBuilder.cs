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

        // Eine Farbsprache: Netzfarben kommen aus der zentralen Karten-Palette
        // (theme-neutral, weil Mapsui nicht theme-abhaengig rendert).
        var color = ZustandsklasseMapColors.Fallback3Stufen(farbe);

        var feature = new GeometryFeature { Geometry = new LineString(coords) };
        feature["Haltungsname"] = hg.Haltungsname;
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, 4) });
        return feature;
    }
}
