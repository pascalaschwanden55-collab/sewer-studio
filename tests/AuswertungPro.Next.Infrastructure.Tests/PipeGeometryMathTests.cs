using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer PipeGeometryMath — sichern IST-Verhalten ab.
/// </summary>
public sealed class PipeGeometryMathTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CircleSegmentPercent
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 100.0)]
    [InlineData(0.5, 50.0)]  // Halbgefuellt = 50%
    public void CircleSegmentPercent_Randwerte_korrekt(double hRatio, double expected)
    {
        double result = PipeGeometryMath.CircleSegmentPercent(hRatio);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData(-1.0, 0.0)]   // Klammerung: < 0 → 0
    [InlineData(2.0, 100.0)]  // Klammerung: > 1 → 100
    public void CircleSegmentPercent_Klammert_ausserhalb_0_1(double hRatio, double expected)
    {
        double result = PipeGeometryMath.CircleSegmentPercent(hRatio);
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void CircleSegmentPercent_Viertel_kleiner_als_50_Prozent()
    {
        // h=0.25 (Viertel) muss unter 50% liegen (Segment-Flaeche nicht linear)
        double result = PipeGeometryMath.CircleSegmentPercent(0.25);
        Assert.True(result < 50.0 && result > 0.0);
    }

    [Fact]
    public void CircleSegmentPercent_Dreiviertel_groesser_als_50_Prozent()
    {
        double result = PipeGeometryMath.CircleSegmentPercent(0.75);
        Assert.True(result > 50.0 && result < 100.0);
    }

    // Numerische Uebereinstimmung mit dem IST-Verhalten aus OverlayToolService
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.9)]
    public void CircleSegmentPercent_Identisch_mit_OverlayToolService(double hRatio)
    {
        double fromMath = PipeGeometryMath.CircleSegmentPercent(hRatio);
        double fromService = OverlayToolService.CircleSegmentPercent(hRatio);
        Assert.Equal(fromService, fromMath);
    }

    // ═══════════════════════════════════════════════════════════════════
    // InverseCircleSegmentPercent
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(100.0, 1.0)]
    [InlineData(50.0, 0.5)]
    public void InverseCircleSegmentPercent_Randwerte_korrekt(double percent, double expected)
    {
        double result = PipeGeometryMath.InverseCircleSegmentPercent(percent);
        Assert.Equal(expected, result, precision: 5);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(25.0)]
    [InlineData(50.0)]
    [InlineData(75.0)]
    [InlineData(90.0)]
    public void InverseCircleSegmentPercent_Roundtrip(double targetPercent)
    {
        // InverseCircleSegmentPercent(CircleSegmentPercent(x)) muss x ergeben
        double hRatio = PipeGeometryMath.InverseCircleSegmentPercent(targetPercent);
        double backPercent = PipeGeometryMath.CircleSegmentPercent(hRatio);
        Assert.Equal(targetPercent, backPercent, precision: 4);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(50.0)]
    [InlineData(90.0)]
    public void InverseCircleSegmentPercent_Identisch_mit_OverlayToolService(double percent)
    {
        double fromMath = PipeGeometryMath.InverseCircleSegmentPercent(percent);
        double fromService = OverlayToolService.InverseCircleSegmentPercent(percent);
        Assert.Equal(fromService, fromMath);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SnapPipeBendAngle
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(20.0, 15.0)]  // naeher an 15 als an 30
    [InlineData(40.0, 45.0)]  // naeher an 45 als an 30
    [InlineData(70.0, 90.0)]  // naeher an 90 als an 45
    [InlineData(15.0, 15.0)]  // exakt
    [InlineData(45.0, 45.0)]  // exakt
    [InlineData(90.0, 90.0)]  // exakt
    public void SnapPipeBendAngle_Snappt_auf_Standard(double input, double expected)
    {
        double result = PipeGeometryMath.SnapPipeBendAngle(input);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BendAngleDeg
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void BendAngleDeg_GeradeLinien_0_Grad()
    {
        // Zwei parallele, gleichgerichtete Vektoren → 0°
        var a1 = new NormalizedPoint(0, 0);
        var a2 = new NormalizedPoint(1, 0);
        var b1 = new NormalizedPoint(0, 0.1);
        var b2 = new NormalizedPoint(1, 0.1);

        double? angle = PipeGeometryMath.BendAngleDeg(a1, a2, b1, b2);

        Assert.NotNull(angle);
        Assert.Equal(0.0, angle!.Value, precision: 6);
    }

    [Fact]
    public void BendAngleDeg_Rechter_Winkel_90_Grad()
    {
        var a1 = new NormalizedPoint(0, 0);
        var a2 = new NormalizedPoint(1, 0);  // waagrecht
        var b1 = new NormalizedPoint(0, 0);
        var b2 = new NormalizedPoint(0, 1);  // senkrecht

        double? angle = PipeGeometryMath.BendAngleDeg(a1, a2, b1, b2);

        Assert.NotNull(angle);
        Assert.Equal(90.0, angle!.Value, precision: 6);
    }

    [Fact]
    public void BendAngleDeg_NullVektor_gibt_null_zurueck()
    {
        var zero = new NormalizedPoint(0.5, 0.5);
        double? angle = PipeGeometryMath.BendAngleDeg(zero, zero, zero, new NormalizedPoint(1, 1));
        Assert.Null(angle);
    }

    // ═══════════════════════════════════════════════════════════════════
    // DistanceSquared
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DistanceSquared_GleichePunkte_ist_null()
    {
        var p = new NormalizedPoint(0.3, 0.7);
        Assert.Equal(0.0, PipeGeometryMath.DistanceSquared(p, p));
    }

    [Fact]
    public void DistanceSquared_BekanntesErgebnis()
    {
        var p1 = new NormalizedPoint(0, 0);
        var p2 = new NormalizedPoint(3, 4);
        // 3^2 + 4^2 = 25
        Assert.Equal(25.0, PipeGeometryMath.DistanceSquared(p1, p2), precision: 10);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Circumcircle
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Circumcircle_DreiPunkte_aufEinheitskreis_korrekt()
    {
        // Drei Punkte auf dem Kreis mit Mittelpunkt (0.5, 0.5), Radius 0.5
        var p1 = new NormalizedPoint(0.5, 0.0);  // oben
        var p2 = new NormalizedPoint(1.0, 0.5);  // rechts
        var p3 = new NormalizedPoint(0.5, 1.0);  // unten

        var (center, radius) = PipeGeometryMath.Circumcircle(p1, p2, p3);

        Assert.Equal(0.5, center.X, precision: 8);
        Assert.Equal(0.5, center.Y, precision: 8);
        Assert.Equal(0.5, radius, precision: 8);
    }

    [Fact]
    public void Circumcircle_KollinearePunkte_Fallback()
    {
        // Kollineare Punkte → kein Absturz, Fallback-Ergebnis
        var p1 = new NormalizedPoint(0, 0);
        var p2 = new NormalizedPoint(0.5, 0);
        var p3 = new NormalizedPoint(1, 0);

        var (center, radius) = PipeGeometryMath.Circumcircle(p1, p2, p3);

        // Mittelpunkt = Schwerpunkt, Radius > 0
        Assert.Equal(0.5, center.X, precision: 8);
        Assert.Equal(0.0, center.Y, precision: 8);
        Assert.True(radius > 0);
    }
}
