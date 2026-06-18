using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class MetrierungProximityEvaluatorTests
{
    private static readonly MetrierungProximityThresholds T = MetrierungProximityThresholds.Default;

    // Fluchtpunkt = Bildmitte, Rohrradius = halber Frame, quadratisches Bild.
    private static MetrierungProximityInput Box(double x1, double y1, double x2, double y2)
        => new(x1, y1, x2, y2, 0.5, 0.5, 1.0, 0.5);

    [Fact]
    public void TunnelFehlmaske_gross_zentral_ohne_Wandnaehe_ist_Voraus()
    {
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.20, 0.20, 0.80, 0.80), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void NaheMuffe_gross_mit_Bildrandkontakt_ist_Codierbar()
    {
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.03, 0.03, 0.97, 0.97), T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision);
    }

    [Fact]
    public void WandschadenNah_klein_aussen_ist_Codierbar()
    {
        // Kleine Box weit aussen oben (nahe Rohrwand/Bildrand), weit weg vom Fluchtpunkt.
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.46, 0.02, 0.54, 0.12), T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision);
    }

    [Fact]
    public void KleinerZentralerFund_weit_voraus_ist_Voraus()
    {
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.46, 0.46, 0.54, 0.54), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void Konservativ_unklarer_mittlerer_Fund_ist_Voraus()
    {
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.30, 0.30, 0.50, 0.50), T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void MittlereTiefe_seitlich_aber_im_DN_Kreis_ist_Voraus()
    {
        // Fachregel (User 2026-06-16): Ein Befund bei halber Rohrtiefe, der seitlich
        // versetzt ist aber NOCH GANZ IM DN-Kreis liegt (outerR < 1.0), ist zu weit voraus
        // -> nur merken, nicht codieren. (Frueher faelschlich "Codierbar" ueber distToVanish.)
        var wide = new MetrierungProximityInput(0.70, 0.48, 0.78, 0.52, 0.5, 0.5, 1.78, 0.5);
        var r = MetrierungProximityEvaluator.Evaluate(wide, T);
        Assert.Equal(MetrierungProximity.Voraus, r.Decision);
    }

    [Fact]
    public void Ueberschreitet_DN_Kreis_nach_aussen_ist_Codierbar()
    {
        // Befund reicht vom mittleren Bereich bis nahe an den Bildrand/die Rohrwand:
        // die aeussere Ecke ueberschreitet den DN-Kreis (outerR >= 1.0) -> Nahbereich,
        // jetzt codieren (Distanz stimmt). Genau die Geometrie "zwischen DN-Kreis und Rand".
        var r = MetrierungProximityEvaluator.Evaluate(Box(0.55, 0.45, 0.97, 0.55), T);
        Assert.Equal(MetrierungProximity.Codierbar, r.Decision);
    }

    [Fact]
    public void DistToVanish_ist_in_Rohrradien_geeicht_unabhaengig_vom_Aspect()
    {
        // Rohr fuellt die Breite (pipeR=0.5). Box-Zentrum ~ an der rechten Rohrwand.
        // Einheiten-Konsistenz: DistToVanish muss ~1.0 sein (an der Wand), NICHT ~1.78 (aspect-verfaelscht).
        var atWall = new MetrierungProximityInput(0.96, 0.48, 1.00, 0.52, 0.5, 0.5, 1.78, 0.5);
        var r = MetrierungProximityEvaluator.Evaluate(atWall, T);
        Assert.True(r.DistToVanish > 0.85 && r.DistToVanish < 1.10,
            $"DistToVanish={r.DistToVanish:F2} sollte ~1.0 sein (Rohrradius-Eichung, aspect-unabhaengig)");
    }
}
