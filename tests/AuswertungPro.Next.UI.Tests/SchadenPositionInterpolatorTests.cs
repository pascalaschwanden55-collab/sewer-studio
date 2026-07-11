using System.Collections.Generic;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Meterstand -> Kartenpunkt entlang der Haltungslinie. WICHTIG: WebMercator ist
/// verzerrt, deshalb wird ueber den ANTEIL an der Soll-Laenge (echte Meter)
/// interpoliert, nicht ueber absolute Mercator-Distanzen.
/// </summary>
public sealed class SchadenPositionInterpolatorTests
{
    private static readonly IReadOnlyList<(double X, double Y)> Linie =
        [(0, 0), (100, 0)]; // 100 Mercator-Einheiten

    [Fact]
    public void Meter_wird_als_anteil_der_soll_laenge_skaliert()
    {
        // Haltung ist real 50 m lang -> 25 m = halbe Strecke = Mercator-X 50.
        var p = SchadenPositionInterpolator.Interpoliere(Linie, meter: 25d, sollLaengeMeter: 50d);
        Assert.NotNull(p);
        Assert.Equal(50d, p.Value.X, 6);
    }

    [Fact]
    public void Ueberlaenge_wird_ans_ende_geklemmt()
    {
        var p = SchadenPositionInterpolator.Interpoliere(Linie, meter: 80d, sollLaengeMeter: 50d);
        Assert.Equal(100d, p!.Value.X, 6);
    }

    [Fact]
    public void Negative_meter_bleiben_am_anfang()
    {
        var p = SchadenPositionInterpolator.Interpoliere(Linie, meter: -3d, sollLaengeMeter: 50d);
        Assert.Equal(0d, p!.Value.X, 6);
    }

    [Fact]
    public void Ohne_soll_laenge_wird_direkt_geklemmt_interpoliert()
    {
        // Fallback: Meter als Mercator-Distanz (besser als gar kein Punkt).
        var p = SchadenPositionInterpolator.Interpoliere(Linie, meter: 30d, sollLaengeMeter: null);
        Assert.Equal(30d, p!.Value.X, 6);
    }

    [Fact]
    public void Leere_linie_liefert_null()
    {
        Assert.Null(SchadenPositionInterpolator.Interpoliere([], 10d, 50d));
        Assert.Null(SchadenPositionInterpolator.Interpoliere([(1, 1)], 10d, 50d));
    }
}
