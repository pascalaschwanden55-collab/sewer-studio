using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fliessrichtungs-Pfeil als "V" aus zwei kurzen Linien: Spitze bei halber Strecke,
/// Fluegel zeigen entgegen der Fliessrichtung (Digitalisierungsrichtung von -> bis).
/// </summary>
public sealed class FliessrichtungsPfeilBuilderTests
{
    private const double Tol = 1e-6;

    [Fact]
    public void Horizontale_linie_pfeil_zeigt_nach_rechts()
    {
        IReadOnlyList<(double X, double Y)> linie = [(0d, 0d), (10d, 0d)];
        var fluegel = FliessrichtungsPfeilBuilder.BauePfeilLinien(linie, groesse: 1d);

        Assert.Equal(2, fluegel.Count);
        foreach (var (spitze, _) in fluegel)
        {
            Assert.Equal(5d, spitze.X, 6);
            Assert.Equal(0d, spitze.Y, 6);
        }

        // Fluegel-Enden liegen hinter der Spitze (kleineres X), symmetrisch ober-/unterhalb.
        var (_, endeA) = fluegel[0];
        var (_, endeB) = fluegel[1];
        Assert.True(endeA.X < 5d && endeB.X < 5d);
        Assert.Equal(endeA.Y, -endeB.Y, 6);
        Assert.Equal(1d, Math.Sqrt(Math.Pow(endeA.X - 5d, 2) + Math.Pow(endeA.Y, 2)), 6);
    }

    [Fact]
    public void Vertikale_linie_pfeil_zeigt_nach_oben()
    {
        IReadOnlyList<(double X, double Y)> linie = [(0d, 0d), (0d, 10d)];
        var fluegel = FliessrichtungsPfeilBuilder.BauePfeilLinien(linie, groesse: 1d);

        foreach (var (spitze, ende) in fluegel)
        {
            Assert.Equal(0d, spitze.X, 6);
            Assert.Equal(5d, spitze.Y, 6);
            Assert.True(ende.Y < 5d); // Fluegel zeigen zurueck (nach unten)
        }
    }

    [Fact]
    public void Degenerierte_linie_liefert_nichts()
    {
        Assert.Empty(FliessrichtungsPfeilBuilder.BauePfeilLinien([], 1d));
        Assert.Empty(FliessrichtungsPfeilBuilder.BauePfeilLinien([(3d, 3d)], 1d));
    }
}
