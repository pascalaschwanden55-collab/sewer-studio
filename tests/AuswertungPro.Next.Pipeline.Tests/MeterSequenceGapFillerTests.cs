using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Fuellt kurze Luecken zwischen zwei gelesenen Meterstaenden. Der OSD-Leser
/// deckt je nach Anzeigestil 17 bis 95 Prozent der Bilder ab; kurze Luecken sind
/// der Normalfall.
///
/// Drei harte Klammern, alle aus einem echten Fehler geboren: nur zwischen
/// GELESENEN Werten, nur ueber kurze Luecken, und nie ueber einen
/// Richtungswechsel — faellt der Meterstand zwischen zwei Messungen, hat die
/// Kamera gedreht, und ein interpolierter Wert saehe sauber aus und waere falsch.
/// </summary>
public sealed class MeterSequenceGapFillerTests
{
    [Fact]
    public void Eine_kurze_Luecke_zwischen_zwei_Messungen_wird_gefuellt()
    {
        var gefuellt = Fuelle(
            (10.0, 3.0),
            (11.0, null),
            (12.0, null),
            (13.0, 3.3));

        Assert.Equal(new double?[] { 3.0, 3.1, 3.2, 3.3 }, gefuellt.Select(r => Runde(r.Meter)));
        Assert.Equal(new[] { false, true, true, false }, gefuellt.Select(r => r.IsEstimated));
    }

    [Fact]
    public void Eine_lange_Luecke_bleibt_offen()
    {
        // Bei 17 Prozent Abdeckung sind die Luecken Wuesten, keine Luecken.
        var gefuellt = Fuelle(
            (10.0, 3.0),
            (30.0, null),
            (60.0, 9.0));

        Assert.Null(gefuellt[1].Meter);
        Assert.False(gefuellt[1].IsEstimated);
    }

    [Fact]
    public void Ueber_einen_Richtungswechsel_wird_nie_interpoliert()
    {
        // Faellt der Meterstand, ist die Kamera zurueckgefahren. Ein gemittelter
        // Wert dazwischen sieht sauber aus und ist falsch.
        var gefuellt = Fuelle(
            (10.0, 7.4),
            (11.0, null),
            (12.0, 6.9));

        Assert.Null(gefuellt[1].Meter);
        Assert.False(gefuellt[1].IsEstimated);
    }

    [Fact]
    public void Ein_stehender_Meterstand_ist_kein_Richtungswechsel()
    {
        var gefuellt = Fuelle(
            (10.0, 5.0),
            (11.0, null),
            (12.0, 5.0));

        Assert.Equal(5.0, gefuellt[1].Meter);
        Assert.True(gefuellt[1].IsEstimated);
    }

    [Fact]
    public void Ein_geschaetzter_Wert_darf_selbst_nie_Klammer_sein()
    {
        // Sonst wandert eine Schaetzung schrittweise durch das ganze Video.
        var gefuellt = Fuelle(
            (10.0, 3.0),
            (11.0, null),
            (12.0, 3.2),
            (13.0, null),
            (40.0, 8.0));

        Assert.True(gefuellt[1].IsEstimated);
        Assert.Null(gefuellt[3].Meter);
    }

    [Fact]
    public void Vor_der_ersten_und_nach_der_letzten_Messung_wird_nicht_gefuellt()
    {
        var gefuellt = Fuelle(
            (10.0, null),
            (11.0, 3.0),
            (12.0, 3.1),
            (13.0, null));

        Assert.Null(gefuellt[0].Meter);
        Assert.Null(gefuellt[3].Meter);
    }

    [Fact]
    public void Gelesene_Werte_bleiben_unveraendert()
    {
        var gefuellt = Fuelle((10.0, 3.0), (11.0, 3.1));

        Assert.All(gefuellt, r => Assert.False(r.IsEstimated));
        Assert.Equal(new double?[] { 3.0, 3.1 }, gefuellt.Select(r => Runde(r.Meter)));
    }

    [Fact]
    public void Unsortierte_Eingaben_werden_nach_Zeit_geordnet()
    {
        var gefuellt = Fuelle(
            (13.0, 3.3),
            (10.0, 3.0),
            (11.0, null));

        Assert.Equal(new[] { 10.0, 11.0, 13.0 }, gefuellt.Select(r => r.TimeSeconds));
        Assert.True(gefuellt[1].IsEstimated);
    }

    [Fact]
    public void Ohne_Messwerte_bleibt_alles_offen()
    {
        var gefuellt = Fuelle((10.0, null), (11.0, null));

        Assert.All(gefuellt, r => Assert.Null(r.Meter));
        Assert.Empty(MeterSequenceGapFiller.Fill(null, new MeterGapFillOptions()));
    }

    private static double? Runde(double? wert) => wert is null ? null : System.Math.Round(wert.Value, 3);

    private static IReadOnlyList<FilledMeterReading> Fuelle(params (double Zeit, double? Meter)[] werte)
        => MeterSequenceGapFiller.Fill(
            werte.Select(w => new MeterReading(w.Zeit, w.Meter)),
            new MeterGapFillOptions());
}
