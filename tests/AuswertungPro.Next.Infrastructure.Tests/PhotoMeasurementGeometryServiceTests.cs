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
    public void BuildDeformationGeometry_BautOverlaySortierungUndDistanzen()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };
        var points = new List<NormalizedPoint>
        {
            new(0.2, 0.5),
            new(0.5, 0.7),
            new(0.8, 0.5),
            new(0.5, 0.3)
        };

        var result = PhotoMeasurementGeometryService.BuildDeformationGeometry(
            points,
            calibration,
            imageAspect: 1.0);

        Assert.NotNull(result);
        Assert.Equal(OverlayToolType.Ellipse, result.Geometry.ToolType);
        Assert.Equal(4, result.Geometry.Points.Count);
        Assert.Equal(33.333333, result.DeformationPercent, precision: 6);
        Assert.Equal(33.3, result.Geometry.FillPercent);
        Assert.Equal(0.4, result.VerticalDistance, precision: 6);
        Assert.Equal(0.6, result.HorizontalDistance, precision: 6);
        Assert.Equal(0.3, result.Top.Y, precision: 6);
        Assert.Equal(0.7, result.Bottom.Y, precision: 6);
        Assert.Equal(0.8, result.Right.X, precision: 6);
        Assert.Equal(0.2, result.Left.X, precision: 6);
    }

    [Fact]
    public void BuildDeformationGeometry_ZuWenigePunkte_GibtNull()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };
        var points = new List<NormalizedPoint>
        {
            new(0.2, 0.5),
            new(0.5, 0.7),
            new(0.8, 0.5)
        };

        Assert.Null(PhotoMeasurementGeometryService.BuildDeformationGeometry(
            points,
            calibration,
            imageAspect: 1.0));
    }

    [Fact]
    public void BuildDeformationPlan_BerechnetCanvasPunkteUndLabel()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };
        var points = new List<NormalizedPoint>
        {
            new(0.2, 0.5),
            new(0.5, 0.7),
            new(0.8, 0.5),
            new(0.5, 0.3)
        };

        var plan = PhotoMeasurementGeometryService.BuildDeformationPlan(
            points,
            calibration,
            imageAspect: 1.0,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100);

        Assert.NotNull(plan);
        Assert.Equal(110, plan.Top.X, precision: 6);
        Assert.Equal(50, plan.Top.Y, precision: 6);
        Assert.Equal(110, plan.Bottom.X, precision: 6);
        Assert.Equal(90, plan.Bottom.Y, precision: 6);
        Assert.Equal(50, plan.Left.X, precision: 6);
        Assert.Equal(70, plan.Left.Y, precision: 6);
        Assert.Equal(170, plan.Right.X, precision: 6);
        Assert.Equal(70, plan.Right.Y, precision: 6);
        Assert.Equal(110, plan.LabelPosition.X, precision: 6);
        Assert.Equal(50, plan.LabelPosition.Y, precision: 6);
        Assert.Equal(33.3, plan.Geometry.FillPercent);
        Assert.Equal(0.4, plan.VerticalDistance, precision: 6);
        Assert.Equal(0.6, plan.HorizontalDistance, precision: 6);
    }

    [Fact]
    public void BuildDeformationPlan_UngueltigeRendergroesse_GibtNull()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };
        var points = new List<NormalizedPoint>
        {
            new(0.2, 0.5),
            new(0.5, 0.7),
            new(0.8, 0.5),
            new(0.5, 0.3)
        };

        Assert.Null(PhotoMeasurementGeometryService.BuildDeformationPlan(
            points,
            calibration,
            imageAspect: 1.0,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 0,
            renderedHeight: 100));
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
    // Overlay-Planung und Werkzeug-Geometrie
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BuildPipeCirclePlan_BerechnetMittelpunktRadiusUndFadenkreuz()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.25, 0.75)
        };

        var plan = PhotoMeasurementGeometryService.BuildPipeCirclePlan(
            calibration,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100);

        Assert.NotNull(plan);
        Assert.Equal(60, plan.Center.X, precision: 6);
        Assert.Equal(95, plan.Center.Y, precision: 6);
        Assert.Equal(30, plan.Radius, precision: 6);
        Assert.Equal(54, plan.HorizontalStart.X, precision: 6);
        Assert.Equal(66, plan.HorizontalEnd.X, precision: 6);
        Assert.Equal(89, plan.VerticalStart.Y, precision: 6);
        Assert.Equal(101, plan.VerticalEnd.Y, precision: 6);
    }

    [Fact]
    public void BuildPipeCirclePlan_UngueltigeRendergroesse_GibtNull()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        Assert.Null(PhotoMeasurementGeometryService.BuildPipeCirclePlan(
            calibration,
            renderedX: 0,
            renderedY: 0,
            renderedWidth: 0,
            renderedHeight: 100));
    }

    [Fact]
    public void IsInsidePipeCenterHitArea_NutztRadiusfaktor()
    {
        var plan = new PhotoMeasurementPipeCirclePlan(
            Center: new PhotoMeasurementCanvasPoint(100, 100),
            Radius: 50,
            HorizontalStart: new PhotoMeasurementCanvasPoint(94, 100),
            HorizontalEnd: new PhotoMeasurementCanvasPoint(106, 100),
            VerticalStart: new PhotoMeasurementCanvasPoint(100, 94),
            VerticalEnd: new PhotoMeasurementCanvasPoint(100, 106));

        Assert.True(PhotoMeasurementGeometryService.IsInsidePipeCenterHitArea(plan, 109, 100));
        Assert.False(PhotoMeasurementGeometryService.IsInsidePipeCenterHitArea(plan, 110, 100));
    }

    [Fact]
    public void BuildCrossSectionGeometry_BerechnetFlaecheProzentUndLabelpunkt()
    {
        var points = new List<NormalizedPoint>
        {
            new(0.25, 0.25),
            new(0.75, 0.25),
            new(0.75, 0.75),
            new(0.25, 0.75)
        };

        var result = PhotoMeasurementGeometryService.BuildCrossSectionGeometry(
            points,
            renderWidth: 200,
            renderHeight: 200,
            normalizedDiameter: 1.0);

        Assert.NotNull(result);
        Assert.Equal(OverlayToolType.CrossSection, result.Geometry.ToolType);
        Assert.Equal(4, result.Geometry.Points.Count);
        Assert.Equal(10000, result.PolygonAreaPx, precision: 6);
        Assert.Equal(100, result.PipeRadiusPx, precision: 6);
        Assert.Equal(31.830989, result.ReductionPercent, precision: 6);
        Assert.Equal(31.8, result.Geometry.FillPercent);
        Assert.Equal(0.5, result.LabelPoint.X, precision: 6);
        Assert.Equal(0.5, result.LabelPoint.Y, precision: 6);
    }

    [Fact]
    public void BuildCrossSectionGeometry_UngueltigeEingaben_GibtNull()
    {
        var twoPoints = new List<NormalizedPoint> { new(0, 0), new(1, 0) };

        Assert.Null(PhotoMeasurementGeometryService.BuildCrossSectionGeometry(
            twoPoints,
            renderWidth: 100,
            renderHeight: 100,
            normalizedDiameter: 1.0));

        var triangle = new List<NormalizedPoint> { new(0, 0), new(1, 0), new(0, 1) };
        Assert.Null(PhotoMeasurementGeometryService.BuildCrossSectionGeometry(
            triangle,
            renderWidth: 0,
            renderHeight: 100,
            normalizedDiameter: 1.0));
    }

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
    public void BuildCalibrationGeometry_BerechnetDurchmesserUndMitte()
    {
        var result = PhotoMeasurementGeometryService.BuildCalibrationGeometry(
            new NormalizedPoint(0.2, 0.4),
            new NormalizedPoint(0.8, 0.4),
            imageAspect: 1.0);

        Assert.NotNull(result);
        Assert.Equal(0.6, result.NormalizedDiameter, precision: 6);
        Assert.Equal(0.5, result.PipeCenter.X, precision: 6);
        Assert.Equal(0.4, result.PipeCenter.Y, precision: 6);
    }

    [Fact]
    public void BuildCalibrationGeometry_ZuKurzeLinie_GibtNull()
    {
        var result = PhotoMeasurementGeometryService.BuildCalibrationGeometry(
            new NormalizedPoint(0.2, 0.4),
            new NormalizedPoint(0.205, 0.4),
            imageAspect: 1.0);

        Assert.Null(result);
    }

    [Fact]
    public void BuildMarkRectangleGeometry_NormalisiertPunkte()
    {
        var geometry = PhotoMeasurementGeometryService.BuildMarkRectangleGeometry(
            new NormalizedPoint(0.8, 0.7),
            new NormalizedPoint(0.2, 0.3));

        Assert.NotNull(geometry);
        Assert.Equal(OverlayToolType.Rectangle, geometry.ToolType);
        Assert.Equal(4, geometry.Points.Count);
        Assert.Equal(0.2, geometry.Points[0].X, precision: 6);
        Assert.Equal(0.3, geometry.Points[0].Y, precision: 6);
        Assert.Equal(0.8, geometry.Points[2].X, precision: 6);
        Assert.Equal(0.7, geometry.Points[2].Y, precision: 6);
    }

    [Fact]
    public void BuildMarkRectangleGeometry_ZuKlein_GibtNull()
    {
        var geometry = PhotoMeasurementGeometryService.BuildMarkRectangleGeometry(
            new NormalizedPoint(0.2, 0.3),
            new NormalizedPoint(0.205, 0.7));

        Assert.Null(geometry);
    }

    [Fact]
    public void BuildMarkRectangleGeometry_BegrenztDragEndeAufDasFoto()
    {
        var geometry = PhotoMeasurementGeometryService.BuildMarkRectangleGeometry(
            new NormalizedPoint(0.2, 0.3),
            new NormalizedPoint(1.4, -0.2));

        Assert.NotNull(geometry);
        Assert.Equal(0.2, geometry.Points[0].X, precision: 6);
        Assert.Equal(0.0, geometry.Points[0].Y, precision: 6);
        Assert.Equal(1.0, geometry.Points[2].X, precision: 6);
        Assert.Equal(0.3, geometry.Points[2].Y, precision: 6);
    }

    [Fact]
    public void BuildLineGeometry_BerechnetMillimeterUndUhrlagen()
    {
        var calibration = new PipeCalibration
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        var result = PhotoMeasurementGeometryService.BuildLineGeometry(
            OverlayToolType.Ruler,
            new NormalizedPoint(0.5, 0.2),
            new NormalizedPoint(0.5, 0.8),
            calibration,
            imageAspect: 1.0);

        Assert.NotNull(result);
        Assert.Equal(300, result.Millimeters, precision: 6);
        Assert.Equal(0.6, result.NormalizedLength, precision: 6);
        Assert.Equal(OverlayToolType.Ruler, result.Geometry.ToolType);
        Assert.Equal(300, result.Geometry.Q1Mm);
        Assert.Equal(0, result.Geometry.ClockFrom);
        Assert.Equal(6, result.Geometry.ClockTo);
    }

    [Fact]
    public void BuildLineGeometry_InvalidesWerkzeug_Wirft()
    {
        var calibration = new PipeCalibration { NormalizedDiameter = 0.6 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhotoMeasurementGeometryService.BuildLineGeometry(
                OverlayToolType.Rectangle,
                new NormalizedPoint(0.5, 0.2),
                new NormalizedPoint(0.5, 0.8),
                calibration,
                imageAspect: 1.0));
    }

    [Fact]
    public void BuildLevelOverlayPlan_Wasser_PlantUnteresSegment()
    {
        var geometry = new OverlayGeometry
        {
            LevelSubMode = LevelMode.Water,
            Points = new List<NormalizedPoint>
            {
                new(0.5, 0.6),
                new(0.5, 0.6)
            }
        };
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        var plan = PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geometry,
            calibration,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100,
            cameraHeightPercent: 50);

        Assert.NotNull(plan);
        Assert.Equal(110, plan.Center.X, precision: 6);
        Assert.Equal(70, plan.Center.Y, precision: 6);
        Assert.Equal(30, plan.PipeRadius, precision: 6);
        Assert.Equal(80, plan.LevelY, precision: 6);
        Assert.Equal(80, plan.FillRect.X, precision: 6);
        Assert.Equal(80, plan.FillRect.Y, precision: 6);
        Assert.Equal(60, plan.FillRect.Width, precision: 6);
        Assert.Equal(20, plan.FillRect.Height, precision: 6);
        Assert.Equal(28.284271, plan.ChordHalf, precision: 6);
        Assert.Equal(81.715729, plan.LineStart.X, precision: 6);
        Assert.Equal(138.284271, plan.LineEnd.X, precision: 6);
        Assert.Equal(80, plan.LineStart.Y, precision: 6);
        Assert.Equal(110, plan.LabelPosition.X, precision: 6);
        Assert.Equal(62, plan.LabelPosition.Y, precision: 6);
    }

    [Fact]
    public void BuildLevelOverlayPlan_Hindernis_PlantOberesSegment()
    {
        var geometry = new OverlayGeometry
        {
            LevelSubMode = LevelMode.Obstacle,
            Points = new List<NormalizedPoint>
            {
                new(0.5, 0.4),
                new(0.5, 0.4)
            }
        };
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        var plan = PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geometry,
            calibration,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100,
            cameraHeightPercent: 50);

        Assert.NotNull(plan);
        Assert.Equal(80, plan.FillRect.X, precision: 6);
        Assert.Equal(40, plan.FillRect.Y, precision: 6);
        Assert.Equal(60, plan.FillRect.Width, precision: 6);
        Assert.Equal(20, plan.FillRect.Height, precision: 6);
        Assert.Equal(60, plan.LevelY, precision: 6);
        Assert.Equal(28.284271, plan.ChordHalf, precision: 6);
    }

    [Fact]
    public void BuildLevelOverlayPlan_UngueltigeEingaben_GibtNull()
    {
        var geometry = new OverlayGeometry { LevelSubMode = LevelMode.Water };
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        Assert.Null(PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geometry,
            calibration,
            renderedX: 0,
            renderedY: 0,
            renderedWidth: 100,
            renderedHeight: 100,
            cameraHeightPercent: 50));

        geometry.Points.Add(new NormalizedPoint(0.5, 0.5));
        geometry.Points.Add(new NormalizedPoint(0.5, 0.5));
        Assert.Null(PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geometry,
            calibration,
            renderedX: 0,
            renderedY: 0,
            renderedWidth: 0,
            renderedHeight: 100,
            cameraHeightPercent: 50));
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
    public void BuildAngleOverlayPlan_Lateral_BautPipeUndLateralPlan()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        var plan = PhotoMeasurementGeometryService.BuildAngleOverlayPlan(
            OverlayToolType.LateralCircle,
            calibration,
            positionDeg: 90,
            angleDeg: 60,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100);

        Assert.NotNull(plan);
        Assert.Equal(110, plan.Center.X, precision: 6);
        Assert.Equal(70, plan.Center.Y, precision: 6);
        Assert.Equal(30, plan.PipeRadius, precision: 6);
        Assert.Equal(3, plan.ClockHour, precision: 6);
        Assert.Equal(OverlayToolType.LateralCircle, plan.Geometry.ToolType);
        Assert.NotNull(plan.Lateral);
        Assert.Null(plan.Bend);
        Assert.Equal(140, plan.Lateral.OpeningCenter.X, precision: 6);
        Assert.Equal(70, plan.Lateral.OpeningCenter.Y, precision: 6);
    }

    [Fact]
    public void BuildAngleOverlayPlan_Bend_BautPipeUndBogenPlan()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        var plan = PhotoMeasurementGeometryService.BuildAngleOverlayPlan(
            OverlayToolType.PipeBend,
            calibration,
            positionDeg: 0,
            angleDeg: 45,
            renderedX: 10,
            renderedY: 20,
            renderedWidth: 200,
            renderedHeight: 100);

        Assert.NotNull(plan);
        Assert.Equal(110, plan.Center.X, precision: 6);
        Assert.Equal(70, plan.Center.Y, precision: 6);
        Assert.Equal(30, plan.PipeRadius, precision: 6);
        Assert.Equal(0, plan.ClockHour, precision: 6);
        Assert.Equal(OverlayToolType.PipeBend, plan.Geometry.ToolType);
        Assert.Null(plan.Lateral);
        Assert.NotNull(plan.Bend);
        Assert.Equal(8, plan.Bend.Rings.Count);
        Assert.Equal(21, plan.Bend.AxisPoints.Count);
    }

    [Fact]
    public void BuildAngleOverlayPlan_UngueltigeRendergroesse_GibtNull()
    {
        var calibration = new PipeCalibration
        {
            NormalizedDiameter = 0.6,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };

        Assert.Null(PhotoMeasurementGeometryService.BuildAngleOverlayPlan(
            OverlayToolType.LateralCircle,
            calibration,
            positionDeg: 90,
            angleDeg: 60,
            renderedX: 0,
            renderedY: 0,
            renderedWidth: 0,
            renderedHeight: 100));
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
    public void BuildArcPlan_KleinerUhrzeigersinnbogen_RechnetStartEndeUndFlags()
    {
        var plan = PhotoMeasurementGeometryService.BuildArcPlan(
            centerX: 100,
            centerY: 100,
            radius: 50,
            startRad: 0,
            endRad: Math.PI / 2.0);

        Assert.Equal(150, plan.Start.X, precision: 6);
        Assert.Equal(100, plan.Start.Y, precision: 6);
        Assert.Equal(100, plan.End.X, precision: 6);
        Assert.Equal(150, plan.End.Y, precision: 6);
        Assert.Equal(50, plan.Radius, precision: 6);
        Assert.False(plan.IsLargeArc);
        Assert.True(plan.IsClockwise);
    }

    [Fact]
    public void BuildArcPlan_GrosserGegenuhrzeigersinnbogen_RechnetFlags()
    {
        var plan = PhotoMeasurementGeometryService.BuildArcPlan(
            centerX: 100,
            centerY: 100,
            radius: 50,
            startRad: Math.PI,
            endRad: -Math.PI);

        Assert.True(plan.IsLargeArc);
        Assert.False(plan.IsClockwise);
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
