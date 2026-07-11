using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Geometrie-Basis fuer Karten-Features (Laenge, Punkt/Richtung entlang der Linie).</summary>
public sealed class PolylineMathTests
{
    private static readonly IReadOnlyList<(double X, double Y)> Gerade =
        [(0, 0), (100, 0)];

    private static readonly IReadOnlyList<(double X, double Y)> Winkel =
        [(0, 0), (100, 0), (100, 50)];

    [Fact]
    public void Laenge_summiert_segmente()
    {
        Assert.Equal(100d, PolylineMath.Laenge(Gerade), 6);
        Assert.Equal(150d, PolylineMath.Laenge(Winkel), 6);
        Assert.Equal(0d, PolylineMath.Laenge([(5, 5)]), 6);
        Assert.Equal(0d, PolylineMath.Laenge([]), 6);
    }

    [Fact]
    public void PunktBeiDistanz_laeuft_ueber_knicke()
    {
        var p = PolylineMath.PunktBeiDistanz(Winkel, 120d);
        Assert.NotNull(p);
        Assert.Equal(100d, p.Value.X, 6);
        Assert.Equal(20d, p.Value.Y, 6);
    }

    [Fact]
    public void PunktBeiDistanz_klemmt_an_den_enden()
    {
        var start = PolylineMath.PunktBeiDistanz(Gerade, -10d);
        var ende = PolylineMath.PunktBeiDistanz(Gerade, 500d);
        Assert.Equal((0d, 0d), (start!.Value.X, start.Value.Y));
        Assert.Equal((100d, 0d), (ende!.Value.X, ende.Value.Y));
    }

    [Fact]
    public void PunktBeiDistanz_null_bei_leerer_linie()
    {
        Assert.Null(PolylineMath.PunktBeiDistanz([], 10d));
        Assert.Null(PolylineMath.PunktBeiDistanz([(3, 3)], 10d));
    }

    [Fact]
    public void PunktBeiAnteil_halbe_strecke()
    {
        var p = PolylineMath.PunktBeiAnteil(Winkel, 0.5);
        Assert.NotNull(p);
        Assert.Equal(75d, p.Value.X, 6);
        Assert.Equal(0d, p.Value.Y, 6);
    }

    [Fact]
    public void RichtungGrad_folgt_dem_segment()
    {
        // Erste Haelfte zeigt nach rechts (0 Grad), letzter Abschnitt nach oben (90 Grad math. = +Y).
        Assert.Equal(0d, PolylineMath.RichtungGradBeiAnteil(Winkel, 0.25)!.Value, 6);
        Assert.Equal(90d, PolylineMath.RichtungGradBeiAnteil(Winkel, 0.9)!.Value, 6);
        Assert.Null(PolylineMath.RichtungGradBeiAnteil([(1, 1)], 0.5));
    }

    [Fact]
    public void DistanzZuPunkt_misst_lot_und_endpunkte()
    {
        // Lot mitten auf das Segment.
        Assert.Equal(5d, PolylineMath.DistanzZuPunkt(Gerade, (50, 5)), 6);
        // Vor dem Anfang: Distanz zum Startpunkt.
        Assert.Equal(10d, PolylineMath.DistanzZuPunkt(Gerade, (-10, 0)), 6);
        // Auf der Linie: 0.
        Assert.Equal(0d, PolylineMath.DistanzZuPunkt(Winkel, (100, 25)), 6);
        // Degeneriert: unendlich.
        Assert.Equal(double.PositiveInfinity, PolylineMath.DistanzZuPunkt([], (0, 0)));
    }
}
