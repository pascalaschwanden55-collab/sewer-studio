using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer PhotoMeasurementGeometryService.
/// Sichern die reine Berechnungslogik aus PhotoMeasurementWindow ab (X9-Extraktion).
/// </summary>
public sealed class PhotoMeasurementGeometryServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // LetterboxRect
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void LetterboxRect_QuadratControl_QuadratBild_fuelltVoll()
    {
        var (ox, oy, rw, rh) = PhotoMeasurementGeometryService.LetterboxRect(100, 100, 100, 100);
        Assert.Equal(0, ox, precision: 6);
        Assert.Equal(0, oy, precision: 6);
        Assert.Equal(100, rw, precision: 6);
        Assert.Equal(100, rh, precision: 6);
    }

    [Fact]
    public void LetterboxRect_BreiteresControl_LiefertVertikalesLetterboxing()
    {
        // Control 200x100, Bild 100x100 (quadratisch)
        // Scale = min(200/100, 100/100) = 1.0 → renderedW=100, renderedH=100
        // offsetX = 50, offsetY = 0
        var (ox, oy, rw, rh) = PhotoMeasurementGeometryService.LetterboxRect(200, 100, 100, 100);
        Assert.Equal(50, ox, precision: 6);
        Assert.Equal(0, oy, precision: 6);
        Assert.Equal(100, rw, precision: 6);
        Assert.Equal(100, rh, precision: 6);
    }

    [Fact]
    public void LetterboxRect_HoehereresControl_LiefertHorizontalesLetterboxing()
    {
        // Control 100x200, Bild 100x100 (quadratisch)
        // Scale = 1.0 → renderedW=100, renderedH=100, offsetX=0, offsetY=50
        var (ox, oy, rw, rh) = PhotoMeasurementGeometryService.LetterboxRect(100, 200, 100, 100);
        Assert.Equal(0, ox, precision: 6);
        Assert.Equal(50, oy, precision: 6);
        Assert.Equal(100, rw, precision: 6);
        Assert.Equal(100, rh, precision: 6);
    }

    [Fact]
    public void LetterboxRect_BildGroesserAlsControl_SkalierungKorrekt()
    {
        // Control 50x50, Bild 100x200 (Hochformat)
        // scaleX = 50/100 = 0.5, scaleY = 50/200 = 0.25 → scale = 0.25
        // renderedW = 25, renderedH = 50, offsetX = 12.5, offsetY = 0
        var (ox, oy, rw, rh) = PhotoMeasurementGeometryService.LetterboxRect(50, 50, 100, 200);
        Assert.Equal(12.5, ox, precision: 6);
        Assert.Equal(0, oy, precision: 6);
        Assert.Equal(25, rw, precision: 6);
        Assert.Equal(50, rh, precision: 6);
    }

    [Fact]
    public void LetterboxRect_UngueltigeEingaben_GibtControlGroessZurueck()
    {
        var (ox, oy, rw, rh) = PhotoMeasurementGeometryService.LetterboxRect(100, 100, 0, 0);
        Assert.Equal(0, ox);
        Assert.Equal(0, oy);
        Assert.Equal(100, rw);
        Assert.Equal(100, rh);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SortDeformationPoints
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void SortDeformationPoints_AchsenausgerichteteVierPunkte_KorrekteZuordnung()
    {
        // Rohrmitte 0.5/0.5; vier Punkte genau auf den Achsen
        double cx = 0.5, cy = 0.5;
        var pts = new List<NormalizedPoint>
        {
            new(cx, cy - 0.3),   // oben  → top (12 Uhr)
            new(cx, cy + 0.3),   // unten → bottom (6 Uhr)
            new(cx + 0.3, cy),   // rechts → right (3 Uhr)
            new(cx - 0.3, cy)    // links → left (9 Uhr)
        };

        var (top, bottom, right, left) = PhotoMeasurementGeometryService.SortDeformationPoints(pts, cx, cy);

        Assert.Equal(cy - 0.3, top.Y, precision: 6);
        Assert.Equal(cy + 0.3, bottom.Y, precision: 6);
        Assert.Equal(cx + 0.3, right.X, precision: 6);
        Assert.Equal(cx - 0.3, left.X, precision: 6);
    }

    [Fact]
    public void SortDeformationPoints_NichtAchsenausgerichteteVierPunkte_BestePunkteGewonnen()
    {
        // Punkte leicht versetzt von den Achsen, aber trotzdem eindeutig zuordenbar
        double cx = 0.5, cy = 0.5;
        var pts = new List<NormalizedPoint>
        {
            new(0.55, 0.15),  // leicht rechts-oben → top (12 Uhr-nahe)
            new(0.45, 0.85),  // leicht links-unten → bottom (6 Uhr-nahe)
            new(0.85, 0.55),  // rechts → right (3 Uhr-nahe)
            new(0.15, 0.45)   // links → left (9 Uhr-nahe)
        };

        var (top, bottom, right, left) = PhotoMeasurementGeometryService.SortDeformationPoints(pts, cx, cy);

        // top muss der obere Punkt sein (kleinste Y-Koordinate)
        Assert.Equal(0.15, top.Y, precision: 6);
        // bottom muss der unterste Punkt sein (groesste Y-Koordinate)
        Assert.Equal(0.85, bottom.Y, precision: 6);
        // right muss der rechteste Punkt sein
        Assert.Equal(0.85, right.X, precision: 6);
        // left muss der linkeste Punkt sein
        Assert.Equal(0.15, left.X, precision: 6);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DeformationPercent
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DeformationPercent_KreisfoermigesRohr_GibtNull()
    {
        // Gleichseitig: top/bottom und left/right gleich weit → 0% Verformung
        double cx = 0.5, cy = 0.5, r = 0.3;
        var top    = new NormalizedPoint(cx, cy - r);
        var bottom = new NormalizedPoint(cx, cy + r);
        var left   = new NormalizedPoint(cx - r, cy);
        var right  = new NormalizedPoint(cx + r, cy);

        double result = PhotoMeasurementGeometryService.DeformationPercent(
            top, bottom, left, right, imageAspect: 1.0, nominalDiameter: r * 2);

        Assert.Equal(0, result, precision: 4);
    }

    [Fact]
    public void DeformationPercent_EllipsoidesRohr_GibtPositivenWert()
    {
        // Vertikal gestaucht: top/bottom = 0.4 apart, links/rechts = 0.6 apart
        var top    = new NormalizedPoint(0.5, 0.3);
        var bottom = new NormalizedPoint(0.5, 0.7);
        var left   = new NormalizedPoint(0.2, 0.5);
        var right  = new NormalizedPoint(0.8, 0.5);

        double result = PhotoMeasurementGeometryService.DeformationPercent(
            top, bottom, left, right, imageAspect: 1.0, nominalDiameter: 0.6);

        // dVert = 0.4, dHoriz = 0.6, dMax - dMin = 0.2, dNominal = 0.6
        // → (0.2 / 0.6) * 100 = 33.33%
        Assert.Equal(33.33, result, precision: 1);
    }

    [Fact]
    public void DeformationPercent_OhneNominalDurchmesser_FaelltAufMaxDistanceZurueck()
    {
        var top    = new NormalizedPoint(0.5, 0.3);
        var bottom = new NormalizedPoint(0.5, 0.7);
        var left   = new NormalizedPoint(0.2, 0.5);
        var right  = new NormalizedPoint(0.8, 0.5);

        // nominalDiameter = 0 → dNominal = dMax = 0.6
        double result = PhotoMeasurementGeometryService.DeformationPercent(
            top, bottom, left, right, imageAspect: 1.0, nominalDiameter: 0);

        // (0.6 - 0.4) / 0.6 * 100 ≈ 33.33
        Assert.Equal(33.33, result, precision: 1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ShoelaceAreaPx
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ShoelaceAreaPx_EinQuadrat_KorrekteFlaeche()
    {
        // Einheitsquadrat 0.0–1.0 in normierten Koordinaten,
        // rendWidth=rendHeight=100 → Flaeche = 100*100 = 10000 Pixel²
        var pts = new List<NormalizedPoint>
        {
            new(0, 0), new(1, 0), new(1, 1), new(0, 1)
        };

        double area = PhotoMeasurementGeometryService.ShoelaceAreaPx(pts, 100, 100);

        Assert.Equal(10000, area, precision: 4);
    }

    [Fact]
    public void ShoelaceAreaPx_DreieckMittlereGroesse_KorrekteFlaeche()
    {
        // Rechteck-Dreieck (0,0), (0.5,0), (0,0.5) in Norm,
        // rendW=rendH=200 → Dreieck-Flaeche = 0.5 * 100 * 100 = 5000
        var pts = new List<NormalizedPoint>
        {
            new(0, 0), new(0.5, 0), new(0, 0.5)
        };

        double area = PhotoMeasurementGeometryService.ShoelaceAreaPx(pts, 200, 200);

        Assert.Equal(5000, area, precision: 4);
    }

    [Fact]
    public void ShoelaceAreaPx_WenigerAlsDreiPunkte_GibtNull()
    {
        var pts = new List<NormalizedPoint> { new(0, 0), new(1, 0) };
        double area = PhotoMeasurementGeometryService.ShoelaceAreaPx(pts, 100, 100);
        Assert.Equal(0, area);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CrossSectionPercent + PipeRadiusPx
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CrossSectionPercent_FlaechemitPipegleich_GibtHundertProzent()
    {
        double r = 50.0;
        double pipeArea = System.Math.PI * r * r;

        double result = PhotoMeasurementGeometryService.CrossSectionPercent(pipeArea, r);

        Assert.Equal(100, result, precision: 4);
    }

    [Fact]
    public void CrossSectionPercent_HalbePipeFlaeche_GibtFuenfzigProzent()
    {
        double r = 50.0;
        double halfPipeArea = System.Math.PI * r * r / 2.0;

        double result = PhotoMeasurementGeometryService.CrossSectionPercent(halfPipeArea, r);

        Assert.Equal(50, result, precision: 4);
    }

    [Fact]
    public void PipeRadiusPx_NormierterDurchmesser07_BerechnungKorrekt()
    {
        // normDiam=0.7, refSize=min(200,200)=200 → radius = 0.35 * 200 = 70
        double r = PhotoMeasurementGeometryService.PipeRadiusPx(0.7, 200, 200);
        Assert.Equal(70, r, precision: 6);
    }

    [Fact]
    public void PipeRadiusPx_NullDurchmesser_FaelltAuf07Zurueck()
    {
        // normDiam=0 → 0.7 / 2.0 * min(100,100) = 35
        double r = PhotoMeasurementGeometryService.PipeRadiusPx(0, 100, 100);
        Assert.Equal(35, r, precision: 6);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Winkel-Umrechnung
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 0)]       // 0° → 0h
    [InlineData(90, 3)]      // 90° → 3h
    [InlineData(180, 6)]     // 180° → 6h
    [InlineData(270, 9)]     // 270° → 9h
    [InlineData(360, 12)]    // 360° → 12h
    public void PositionDegToClockHour_KorrekteUmrechnung(double deg, double expected)
    {
        double result = PhotoMeasurementGeometryService.PositionDegToClockHour(deg);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData(0, -1.5707963267948966)]   // 0° (12 Uhr) → -PI/2
    [InlineData(90, 0)]                    // 90° (3 Uhr) → 0 rad
    [InlineData(180, 1.5707963267948966)]  // 180° (6 Uhr) → PI/2
    public void PositionDegToRadians_KorrekteUmrechnung(double deg, double expected)
    {
        double result = PhotoMeasurementGeometryService.PositionDegToRadians(deg);
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void BuildAngleGeometry_Lateral_RechnetEndpunktUndUhrlage()
    {
        var center = new NormalizedPoint(0.5, 0.5);

        var result = PhotoMeasurementGeometryService.BuildAngleGeometry(
            OverlayToolType.LateralCircle,
            center,
            normalizedDiameter: 0.7,
            positionDeg: 90,
            angleDeg: 45);

        Assert.Equal(0, result.PositionRad, precision: 6);
        Assert.Equal(3, result.ClockHour, precision: 6);
        Assert.Equal(OverlayToolType.LateralCircle, result.Geometry.ToolType);
        Assert.Equal(45, result.Geometry.ArcDegrees);
        Assert.Equal(3, result.Geometry.ClockFrom);
        Assert.Equal(2, result.Geometry.Points.Count);
        Assert.Equal(0.5, result.Geometry.Points[0].X, precision: 6);
        Assert.Equal(0.5, result.Geometry.Points[0].Y, precision: 6);
        Assert.Equal(0.85, result.EdgePoint.X, precision: 6);
        Assert.Equal(0.5, result.EdgePoint.Y, precision: 6);
        Assert.Equal(result.EdgePoint.X, result.Geometry.Points[1].X, precision: 6);
        Assert.Equal(result.EdgePoint.Y, result.Geometry.Points[1].Y, precision: 6);
    }

    [Fact]
    public void BuildAngleGeometry_InvalidesWerkzeug_Wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhotoMeasurementGeometryService.BuildAngleGeometry(
                OverlayToolType.Rectangle,
                new NormalizedPoint(0.5, 0.5),
                normalizedDiameter: 0.7,
                positionDeg: 0,
                angleDeg: 30));
    }

    [Fact]
    public void BuildLateralOverlayPlan_RechnetOeffnungSchenkelUndLabel()
    {
        var plan = PhotoMeasurementGeometryService.BuildLateralOverlayPlan(
            centerX: 100,
            centerY: 100,
            pipeRadiusPx: 50,
            positionRad: 0,
            angleDeg: 60);

        Assert.Equal(150, plan.OpeningCenter.X, precision: 6);
        Assert.Equal(100, plan.OpeningCenter.Y, precision: 6);
        Assert.Equal(7.5, plan.OpeningRadius, precision: 6);
        Assert.Equal(175.980762, plan.Arm1End.X, precision: 6);
        Assert.Equal(85, plan.Arm1End.Y, precision: 6);
        Assert.Equal(175.980762, plan.Arm2End.X, precision: 6);
        Assert.Equal(115, plan.Arm2End.Y, precision: 6);
        Assert.Equal(12, plan.ArcRadius, precision: 6);
        Assert.Equal(-0.523599, plan.ArcStartRad, precision: 6);
        Assert.Equal(0.523599, plan.ArcEndRad, precision: 6);
        Assert.Equal(165, plan.LabelPosition.X, precision: 6);
        Assert.Equal(86, plan.LabelPosition.Y, precision: 6);
    }

    [Fact]
    public void BuildBendOverlayPlan_RechnetRingeUndAchse()
    {
        var plan = PhotoMeasurementGeometryService.BuildBendOverlayPlan(
            centerX: 100,
            centerY: 100,
            pipeRadiusPx: 50,
            positionRad: 0,
            angleDeg: 60);

        Assert.Equal(100, plan.ArcCenter.X, precision: 6);
        Assert.Equal(275, plan.ArcCenter.Y, precision: 6);
        Assert.Equal(175, plan.BendRadius, precision: 6);
        Assert.Equal(0.523599, plan.HalfAngleRad, precision: 6);
        Assert.Equal(8, plan.Rings.Count);
        Assert.Equal(21, plan.AxisPoints.Count);

        var firstRing = plan.Rings[0];
        Assert.Equal(12.5, firstRing.Center.X, precision: 6);
        Assert.Equal(123.445554, firstRing.Center.Y, precision: 6);
        Assert.Equal(31.5, firstRing.RadiusX, precision: 6);
        Assert.Equal(10.5, firstRing.RadiusY, precision: 6);
        Assert.Equal(0.7, firstRing.PerspectiveScale, precision: 6);

        var lastRing = plan.Rings[^1];
        Assert.Equal(187.5, lastRing.Center.X, precision: 6);
        Assert.Equal(123.445554, lastRing.Center.Y, precision: 6);

        Assert.Equal(firstRing.Center.X, plan.AxisPoints[0].X, precision: 6);
        Assert.Equal(firstRing.Center.Y, plan.AxisPoints[0].Y, precision: 6);
        Assert.Equal(lastRing.Center.X, plan.AxisPoints[^1].X, precision: 6);
        Assert.Equal(lastRing.Center.Y, plan.AxisPoints[^1].Y, precision: 6);
    }
}
