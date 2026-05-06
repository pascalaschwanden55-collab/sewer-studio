using System;
using System.Collections.Generic;
using System.Windows;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;

namespace AuswertungPro.Next.UI.Ai.PhotoAssistant;

/// <summary>
/// Werkzeug "Bogen / Knick" (BAJ) — echte 3D-Lochkamera-Projektion eines Knickrohrs.
///
/// Welt-Modell:
///   - Rohr 1: Achse parallel zur Sicht-Z-Achse, Laenge tube1Len
///   - Rohr 2: am Knickpunkt nach links abgewinkelt (-x) um bendAngle Grad, Laenge tube2Len
///   - Rohrradius tubeR
/// Pro Rohrstueck 7 Achsen-Punkte, pro Achsen-Punkt ein 32-Segment-Ring senkrecht zur Tangente.
/// </summary>
public static class BendAngleToolService
{
    public const int AxisPointsPerTube = 7;
    public const int RingSegments = 32;

    /// <summary>Default-Werte fuer Welt-Modell (entspricht Mockup).</summary>
    public static readonly BendModelDefaults Defaults = new(
        Tube1Len: 1.6,
        Tube2Len: 2.4,
        TubeRadius: 1.0,
        Focal: 360,
        CamDist: 0.6);

    public sealed record BendModelDefaults(
        double Tube1Len,
        double Tube2Len,
        double TubeRadius,
        double Focal,
        double CamDist);

    /// <summary>Ein projizierter Ring + Achsen-Mittelpunkt + Tiefe (fuer Opazitaets-Sortierung).</summary>
    public sealed record ProjectedRing(
        Point AxisCenterScreen,
        double DepthAtAxis,
        IReadOnlyList<Point> RingPoints);

    /// <summary>
    /// Generiert die zwei Rohrstuecke als Liste projizierter Ringe.
    /// </summary>
    /// <param name="bendAngleDegrees">Knickwinkel 0..90 Grad.</param>
    /// <param name="bendScale">Multiplikator fuer Welt-Skalierung (Mausrad-Zoom, 0.3..2.5).</param>
    /// <param name="cameraHeightPercent">0..100, 50 = Standard (Mitte).</param>
    /// <param name="canvasWidth">Bildbreite in Pixel.</param>
    /// <param name="canvasHeight">Bildhoehe in Pixel.</param>
    /// <param name="dragOffsetX">Drag-Verschiebung in Pixel.</param>
    /// <param name="dragOffsetY">Drag-Verschiebung in Pixel.</param>
    /// <param name="bendDirectionDegrees">Richtung des Knicks im XY-Bild-Plan (0=oben/12h, 90=rechts/3h, 180=unten/6h, 270=links/9h). Default 270 = links (Kompatibilitaet zur alten Hardcoded-Variante).</param>
    public static (IReadOnlyList<ProjectedRing> Tube1, IReadOnlyList<ProjectedRing> Tube2, Point? KinkPointScreen)
        BuildProjectedRings(
            double bendAngleDegrees,
            double bendScale,
            double cameraHeightPercent,
            double canvasWidth,
            double canvasHeight,
            double dragOffsetX = 0,
            double dragOffsetY = 0,
            BendModelDefaults? overrides = null,
            double bendDirectionDegrees = 270)
    {
        var def = overrides ?? Defaults;
        var cx = canvasWidth / 2.0;
        var cy = canvasHeight / 2.0;
        var camDist = def.CamDist;
        var camYOffset = (50.0 - cameraHeightPercent) * 0.012;
        var focal = def.Focal;

        var tube1Len = def.Tube1Len * bendScale;
        var tube2Len = def.Tube2Len * bendScale;
        var tubeR = def.TubeRadius * bendScale;

        var bendRad = bendAngleDegrees * Math.PI / 180.0;
        // Knickpunkt im Welt-Raum: Ende von Rohr 1 entlang +Z.
        var kinkZ = tube1Len;
        var kinkX = 0.0;
        var kinkY = 0.0;

        // Rohr 1: Achse von (0, 0, 0) bis (0, 0, tube1Len), Tangente = (0, 0, 1).
        var tube1 = BuildTubeRings(
            startX: 0, startY: 0, startZ: 0,
            tangentX: 0, tangentY: 0, tangentZ: 1,
            length: tube1Len, tubeR: tubeR,
            focal: focal, camDist: camDist, camYOffset: camYOffset,
            cx: cx, cy: cy, dragOffsetX: dragOffsetX, dragOffsetY: dragOffsetY);

        // Rohr 2: Tangente abgewinkelt um bendAngle in Richtung bendDirectionDegrees.
        // Im Bild-Koordinatensystem zeigt 0° nach oben (12h, -y), 90° rechts (3h, +x),
        // 180° unten (6h, +y), 270° links (9h, -x). Wir bauen die Querkomponente in der
        // X/Y-Ebene anhand der Richtung und addieren die Vorwaerts-Komponente in Z.
        var dirRad = (bendDirectionDegrees - 90.0) * Math.PI / 180.0;
        var tan2X = Math.Sin(bendRad) * Math.Cos(dirRad);
        var tan2Y = Math.Sin(bendRad) * Math.Sin(dirRad);
        var tan2Z = Math.Cos(bendRad);
        var tube2 = BuildTubeRings(
            startX: kinkX, startY: kinkY, startZ: kinkZ,
            tangentX: tan2X, tangentY: tan2Y, tangentZ: tan2Z,
            length: tube2Len, tubeR: tubeR,
            focal: focal, camDist: camDist, camYOffset: camYOffset,
            cx: cx, cy: cy, dragOffsetX: dragOffsetX, dragOffsetY: dragOffsetY);

        // Knickpunkt projizieren
        var kinkProj = PinholeProjection.Project(
            kinkX, kinkY, kinkZ, focal, camDist, camYOffset, cx, cy, dragOffsetX, dragOffsetY);
        Point? kinkScreen = kinkProj.HasValue
            ? new Point(kinkProj.Value.sx, kinkProj.Value.sy) : null;

        return (tube1, tube2, kinkScreen);
    }

    private static IReadOnlyList<ProjectedRing> BuildTubeRings(
        double startX, double startY, double startZ,
        double tangentX, double tangentY, double tangentZ,
        double length, double tubeR,
        double focal, double camDist, double camYOffset,
        double cx, double cy, double dragOffsetX, double dragOffsetY)
    {
        // Tangente normalisieren
        var tLen = Math.Sqrt(tangentX * tangentX + tangentY * tangentY + tangentZ * tangentZ);
        if (tLen < 1e-9) tLen = 1;
        tangentX /= tLen; tangentY /= tLen; tangentZ /= tLen;

        // Basis-Vektoren u, v senkrecht zur Tangente: u = tangent x (0,1,0), normalisiert.
        // Wenn Tangente parallel zu (0,1,0): fallback auf (1,0,0).
        var ux = tangentY * 0 - tangentZ * 1;
        var uy = tangentZ * 0 - tangentX * 0;
        var uz = tangentX * 1 - tangentY * 0;
        var uLen = Math.Sqrt(ux * ux + uy * uy + uz * uz);
        if (uLen < 1e-9) { ux = 1; uy = 0; uz = 0; uLen = 1; }
        ux /= uLen; uy /= uLen; uz /= uLen;

        // v = tangent x u
        var vx = tangentY * uz - tangentZ * uy;
        var vy = tangentZ * ux - tangentX * uz;
        var vz = tangentX * uy - tangentY * ux;

        var rings = new List<ProjectedRing>(AxisPointsPerTube);
        for (var i = 0; i < AxisPointsPerTube; i++)
        {
            var t = i / (double)(AxisPointsPerTube - 1);
            var ax = startX + tangentX * length * t;
            var ay = startY + tangentY * length * t;
            var az = startZ + tangentZ * length * t;

            var axisProj = PinholeProjection.Project(
                ax, ay, az, focal, camDist, camYOffset, cx, cy, dragOffsetX, dragOffsetY);

            var ringPts = new List<Point>(RingSegments);
            for (var s = 0; s < RingSegments; s++)
            {
                var phi = s / (double)RingSegments * 2 * Math.PI;
                var cosPhi = Math.Cos(phi);
                var sinPhi = Math.Sin(phi);
                var px = ax + (ux * cosPhi + vx * sinPhi) * tubeR;
                var py = ay + (uy * cosPhi + vy * sinPhi) * tubeR;
                var pz = az + (uz * cosPhi + vz * sinPhi) * tubeR;
                var p = PinholeProjection.Project(
                    px, py, pz, focal, camDist, camYOffset, cx, cy, dragOffsetX, dragOffsetY);
                if (p.HasValue)
                    ringPts.Add(new Point(p.Value.sx, p.Value.sy));
            }

            rings.Add(new ProjectedRing(
                AxisCenterScreen: axisProj.HasValue ? new Point(axisProj.Value.sx, axisProj.Value.sy) : new Point(double.NaN, double.NaN),
                DepthAtAxis: axisProj?.depth ?? double.MaxValue,
                RingPoints: ringPts));
        }
        return rings;
    }

    /// <summary>
    /// Begrenzt Mausrad-Zoom auf [0.3, 2.5].
    /// </summary>
    public static double ClampScale(double scale) => scale switch
    {
        < 0.3 => 0.3,
        > 2.5 => 2.5,
        _ => scale
    };
}
