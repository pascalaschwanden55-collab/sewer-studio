using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer KbMeterWeighting.Weight.
/// Stellt sicher, dass die extrahierte Formel identisch zur urspruenglichen
/// Inline-Formel in OllamaProtocolAiService und FullProtocolGenerationService verhaelt.
/// </summary>
public sealed class KbMeterWeightingTests
{
    [Fact]
    public void Weight_ExaktGleicherMeterstand_GibtEins()
    {
        // Distanz 0 -> 1.0 - 0 = 1.0
        var w = KbMeterWeighting.Weight(10.0, 10.0);
        Assert.Equal(1.0, w, precision: 10);
    }

    [Fact]
    public void Weight_Distanz12_GibtMinimum()
    {
        // Distanz 12 -> min(1, 12/12) = 1 -> 1 - 1 = 0 -> max(0.35, 0) = 0.35
        var w = KbMeterWeighting.Weight(0.0, 12.0);
        Assert.Equal(0.35, w, precision: 10);
    }

    [Fact]
    public void Weight_DistanzGroesserAls12_BleibtBeiMinimum()
    {
        // Distanz 100 -> wird auf 1.0 geclampt -> Ergebnis 0.35
        var w = KbMeterWeighting.Weight(0.0, 100.0);
        Assert.Equal(0.35, w, precision: 10);
    }

    [Fact]
    public void Weight_Distanz6_GibtHalbierteAbnahme()
    {
        // Distanz 6 -> min(1, 6/12) = 0.5 -> 1 - 0.5 = 0.5 -> max(0.35, 0.5) = 0.5
        var w = KbMeterWeighting.Weight(0.0, 6.0);
        Assert.Equal(0.5, w, precision: 10);
    }

    [Fact]
    public void Weight_IstSymmetrisch()
    {
        // Math.Abs(a-b) == Math.Abs(b-a) -> Reihenfolge spielt keine Rolle
        var w1 = KbMeterWeighting.Weight(5.0, 15.0);
        var w2 = KbMeterWeighting.Weight(15.0, 5.0);
        Assert.Equal(w1, w2, precision: 10);
    }

    [Fact]
    public void Weight_ErgebnisImmerInGrenzen()
    {
        // Gewicht liegt immer in [0.35, 1.0]
        foreach (var dist in new[] { 0.0, 1.0, 5.0, 11.99, 12.0, 50.0, 1000.0 })
        {
            var w = KbMeterWeighting.Weight(0.0, dist);
            Assert.InRange(w, 0.35, 1.0);
        }
    }
}
