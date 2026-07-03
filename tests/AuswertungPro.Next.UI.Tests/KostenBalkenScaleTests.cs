using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Skalierung der Kostenbalken in der Sanierungs-Matrix:
/// Balkenbreite proportional zum teuersten Zeilen-Total.
/// </summary>
public sealed class KostenBalkenScaleTests
{
    [Fact]
    public void Anteil_ProportionalZumMaximum()
    {
        Assert.Equal(0.5, KostenBalkenScale.Anteil(50m, 100m), 3);
        Assert.Equal(1.0, KostenBalkenScale.Anteil(100m, 100m), 3);
    }

    [Fact]
    public void Anteil_NullBeiLeeremMaximumOderNegativemTotal()
    {
        Assert.Equal(0.0, KostenBalkenScale.Anteil(50m, 0m), 3);
        Assert.Equal(0.0, KostenBalkenScale.Anteil(-5m, 100m), 3);
    }

    [Fact]
    public void Breite_MindestbreiteFuerKleinePositiveWerte()
    {
        // 1 CHF von 100'000 waere unsichtbar — kleine Werte bekommen 2px Minimum.
        Assert.Equal(2.0, KostenBalkenScale.Breite(1m, 100_000m, 120), 3);
    }

    [Fact]
    public void Breite_NullOhneKosten_UndVolleBreiteBeimMaximum()
    {
        Assert.Equal(0.0, KostenBalkenScale.Breite(0m, 100m, 120), 3);
        Assert.Equal(120.0, KostenBalkenScale.Breite(100m, 100m, 120), 3);
    }
}
