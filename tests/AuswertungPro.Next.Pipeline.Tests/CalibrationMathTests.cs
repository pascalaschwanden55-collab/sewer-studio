using System;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer CalibrationMath (pure static).
/// Alle Erwartungswerte entsprechen dem IST-Verhalten von PipeCalibration vor der Extraktion.
/// </summary>
public sealed class CalibrationMathTests
{
    // ── MmPerNormUnit ────────────────────────────────────────────────────────

    [Fact]
    public void MmPerNormUnit_NormaldurchmesserNullProzent_LiefertNull()
    {
        // Kein normierter Durchmesser → Ergebnis 0
        double result = CalibrationMath.MmPerNormUnit(nominalDiameterMm: 300, normalizedDiameter: 0.0);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void MmPerNormUnit_DN300MitHalberBildbreite_Liefert600()
    {
        // DN300, Rohr nimmt halbe Bildbreite ein → 300 / 0.5 = 600 mm/Norm
        double result = CalibrationMath.MmPerNormUnit(nominalDiameterMm: 300, normalizedDiameter: 0.5);
        Assert.Equal(600.0, result, precision: 6);
    }

    // ── NormToMm ─────────────────────────────────────────────────────────────

    [Fact]
    public void NormToMm_KeineDurchmesserInfo_FallbackAuf500()
    {
        // Fallback: normalizedLength * 500
        double result = CalibrationMath.NormToMm(normalizedLength: 0.2, nominalDiameterMm: 300, normalizedDiameter: 0.0);
        Assert.Equal(100.0, result, precision: 6);
    }

    [Fact]
    public void NormToMm_DN300HalfWidth_KorrekteUmrechnung()
    {
        // 0.5 normiert × (300 / 0.5) = 300 mm
        double result = CalibrationMath.NormToMm(normalizedLength: 0.5, nominalDiameterMm: 300, normalizedDiameter: 0.5);
        Assert.Equal(300.0, result, precision: 6);
    }

    // ── PixelToMm ────────────────────────────────────────────────────────────

    [Fact]
    public void PixelToMm_MitNormalizedDiameter_DelegiertAnNormToMm()
    {
        // NormalizedDiameter > 0 → gleiche Logik wie NormToMm
        double result = CalibrationMath.PixelToMm(
            normalizedPixels: 0.3,
            frameWidthPx: 1920,
            nominalDiameterMm: 300,
            normalizedDiameter: 0.5,
            pipePixelDiameter: 0.0);
        // 0.3 × (300 / 0.5) = 180 mm
        Assert.Equal(180.0, result, precision: 6);
    }

    [Fact]
    public void PixelToMm_OhneNormalizedDiameter_NutztPipePixelDiameter()
    {
        // normalizedDiameter=0, pipePixelDiameter=960 Px (halbe 1920er Breite)
        // pipePixelNormalized = 960/1920 = 0.5
        // mmPerNormPixel = 300 / 0.5 = 600
        // result = 0.3 * 600 = 180 mm
        double result = CalibrationMath.PixelToMm(
            normalizedPixels: 0.3,
            frameWidthPx: 1920,
            nominalDiameterMm: 300,
            normalizedDiameter: 0.0,
            pipePixelDiameter: 960.0);
        Assert.Equal(180.0, result, precision: 6);
    }

    [Fact]
    public void PixelToMm_OhneBeideKalibrierquellen_LiefertNull()
    {
        // Kein NormalizedDiameter, kein PipePixelDiameter → 0
        double result = CalibrationMath.PixelToMm(
            normalizedPixels: 0.3,
            frameWidthPx: 1920,
            nominalDiameterMm: 300,
            normalizedDiameter: 0.0,
            pipePixelDiameter: 0.0);
        Assert.Equal(0.0, result);
    }

    // ── AspectCorrectedDistance ───────────────────────────────────────────────

    [Fact]
    public void AspectCorrectedDistance_QuadratischGleicheX_LiefertDeltaY()
    {
        var a = new NormalizedPoint(0.0, 0.0);
        var b = new NormalizedPoint(0.0, 1.0);
        double dist = CalibrationMath.AspectCorrectedDistance(a, b, imageAspect: 1.0);
        Assert.Equal(1.0, dist, precision: 10);
    }

    [Fact]
    public void AspectCorrectedDistance_16zu9_SkalierungKorrekt()
    {
        // dx=0.1 × 1.778 = 0.1778, dy=0
        var a = new NormalizedPoint(0.0, 0.5);
        var b = new NormalizedPoint(0.1, 0.5);
        double aspect = 16.0 / 9.0;
        double dist = CalibrationMath.AspectCorrectedDistance(a, b, imageAspect: aspect);
        Assert.Equal(0.1 * aspect, dist, precision: 10);
    }

    // ── NormToMmAspect ────────────────────────────────────────────────────────

    [Fact]
    public void NormToMmAspect_HorizontaleDiagonale_KorrektesMm()
    {
        var a = new NormalizedPoint(0.0, 0.5);
        var b = new NormalizedPoint(0.5, 0.5);
        // dist = 0.5 (keine Aspect-Korrektur noetig bei aspect=1)
        // NormToMm(0.5, 300, 0.5) = 300 mm
        double result = CalibrationMath.NormToMmAspect(a, b, nominalDiameterMm: 300, normalizedDiameter: 0.5, imageAspect: 1.0);
        Assert.Equal(300.0, result, precision: 6);
    }

    // ── PointToClockHour ──────────────────────────────────────────────────────

    [Fact]
    public void PointToClockHour_ScheitelOben_Liefert0Uhr()
    {
        // Punkt direkt oberhalb der Mitte → 0 Uhr (12 Uhr)
        var center = new NormalizedPoint(0.5, 0.5);
        var point  = new NormalizedPoint(0.5, 0.0); // Y kleiner = oben
        double hour = CalibrationMath.PointToClockHour(point, pipeCenter: center);
        Assert.Equal(0.0, hour, precision: 6);
    }

    [Fact]
    public void PointToClockHour_Rechts_Liefert3Uhr()
    {
        var center = new NormalizedPoint(0.5, 0.5);
        var point  = new NormalizedPoint(1.0, 0.5);
        double hour = CalibrationMath.PointToClockHour(point, pipeCenter: center);
        Assert.Equal(3.0, hour, precision: 6);
    }

    [Fact]
    public void PointToClockHour_Unten_Liefert6Uhr()
    {
        var center = new NormalizedPoint(0.5, 0.5);
        var point  = new NormalizedPoint(0.5, 1.0);
        double hour = CalibrationMath.PointToClockHour(point, pipeCenter: center);
        Assert.Equal(6.0, hour, precision: 6);
    }

    [Fact]
    public void PointToClockHour_Links_Liefert9Uhr()
    {
        var center = new NormalizedPoint(0.5, 0.5);
        var point  = new NormalizedPoint(0.0, 0.5);
        double hour = CalibrationMath.PointToClockHour(point, pipeCenter: center);
        Assert.Equal(9.0, hour, precision: 6);
    }
}
