using System.Collections.Generic;
using System.Linq;
using Mapsui.Nts;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Detail-Features fuer den Zoom-Nahbereich: Haltungs-Beschriftungen und
/// Fliessrichtungs-Pfeile. Wird on-the-fly NUR fuer die gerade sichtbaren
/// Netzlinien gebaut (nie in den Netz-Cache gemischt) und strikt gedeckelt.
/// </summary>
public static class KarteDetailFeatureBuilder
{
    /// <summary>Obergrenze, damit der Detail-Layer beim Pan nie zum Bremsklotz wird.</summary>
    public const int MaxHaltungen = 400;

    public static IReadOnlyList<GeometryFeature> Build(
        IEnumerable<GeometryFeature> sichtbareNetzFeatures,
        double aufloesungMeterProPixel,
        bool mitLabels,
        bool mitPfeilen)
    {
        var ergebnis = new List<GeometryFeature>();
        if (!mitLabels && !mitPfeilen)
            return ergebnis;

        // Pfeilgroesse an die Aufloesung koppeln (~9 px auf dem Bildschirm).
        var pfeilGroesse = aufloesungMeterProPixel * 9d;

        foreach (var netz in sichtbareNetzFeatures.Take(MaxHaltungen))
        {
            if (netz.Geometry is not LineString linie || linie.Coordinates.Length < 2)
                continue;

            var punkte = linie.Coordinates.Select(c => (c.X, c.Y)).ToArray();
            var name = netz["Haltungsname"] as string;

            if (mitLabels && !string.IsNullOrWhiteSpace(name)
                && PolylineMath.PunktBeiAnteil(punkte, 0.5) is { } mitte)
            {
                ergebnis.Add(BuildLabel(name!, mitte));
            }

            if (mitPfeilen)
            {
                foreach (var (spitze, ende) in FliessrichtungsPfeilBuilder.BauePfeilLinien(punkte, pfeilGroesse))
                {
                    var fluegel = new GeometryFeature
                    {
                        Geometry = new LineString(
                        [
                            new Coordinate(spitze.X, spitze.Y),
                            new Coordinate(ende.X, ende.Y)
                        ])
                    };
                    // Dunkle Linie mit hellem Kern — lesbar auf Satellit UND Karte.
                    fluegel.Styles.Add(new VectorStyle { Line = new Pen(new Color(255, 255, 255, 230), 3.2) });
                    fluegel.Styles.Add(new VectorStyle { Line = new Pen(new Color(30, 41, 59, 235), 1.6) });
                    ergebnis.Add(fluegel);
                }
            }
        }

        return ergebnis;
    }

    private static GeometryFeature BuildLabel(string name, (double X, double Y) position)
    {
        var feature = new GeometryFeature { Geometry = new Point(position.X, position.Y) };
        feature.Styles.Add(new LabelStyle
        {
            Text = name,
            Font = new Font { Size = 11 },
            ForeColor = new Color(15, 23, 42),
            BackColor = new Brush(new Color(255, 255, 255, 205)), // heller Chip, lesbar auf Satellit
            Offset = new Offset(0, -14),
            CollisionDetection = true
        });
        return feature;
    }
}
