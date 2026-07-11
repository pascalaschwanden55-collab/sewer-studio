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
        => Build(hg, kondition, invertiert, dnByName: null);

    /// <summary>
    /// Premium-Variante: 5-Klassen-Farbe (Excel-Palette) + Linienbreite nach Nennweite.
    /// Ohne DN-Daten bleibt die bisherige Einheitsbreite; ohne EZ-Skala der 3-Stufen-Rueckfall.
    /// </summary>
    public static GeometryFeature Build(
        ProjectedHaltungGeometry hg,
        IReadOnlyDictionary<string, int?> kondition,
        bool invertiert,
        IReadOnlyDictionary<string, int?>? dnByName)
    {
        var coords = hg.Points.Select(p => new Coordinate(p.X, p.Y)).ToArray();

        var wert = kondition.TryGetValue(hg.Haltungsname, out var k) ? k : null;

        // Eine Farbsprache: 5 Zustandsklassen aus der Excel-Palette, wenn die EZ-Skala
        // aktiv ist (invertiert, 0=schlecht..4=gut — dieselbe Skala wie die Palette).
        // Sonst bzw. bei Werten ausserhalb 0..4 der bisherige 3-Stufen-Rueckfall.
        var color = invertiert && wert is >= 0 and <= 4
            ? ZustandsklasseMapColors.Fill(wert.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                ?? ZustandsklasseMapColors.Unbekannt
            : ZustandsklasseMapColors.Fallback3Stufen(ZustandColorMapper.Map(wert, invertiert));

        var dn = dnByName is not null && dnByName.TryGetValue(hg.Haltungsname, out var d) ? d : null;

        var feature = new GeometryFeature { Geometry = new LineString(coords) };
        feature["Haltungsname"] = hg.Haltungsname;
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, DnLineWidthMapper.Breite(dn)) });
        return feature;
    }
}
