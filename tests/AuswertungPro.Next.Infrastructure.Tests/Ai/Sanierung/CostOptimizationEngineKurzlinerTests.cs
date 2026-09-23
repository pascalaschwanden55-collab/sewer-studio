using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Sanierung;

/// <summary>
/// Audit A12 (23.09.2026): Der Teilwort-Abgleich fand «Liner» (180 CHF/m) vor «Kurzliner» (950 CHF/Stk).
/// Jeder Kurzliner mit Zusatztext — auch die eigenen Listeneintraege «Kurzliner / Partliner» und
/// «Pointliner» — wurde dadurch pro Meter gerechnet, bei 40 m etwa siebenmal zu teuer.
/// </summary>
public sealed class CostOptimizationEngineKurzlinerTests
{
    private static decimal Erwartet(string massnahme, double laenge)
        => new CostOptimizationEngine().Calculate(new CostCalcInput
        {
            Measure = massnahme, DiameterMm = 300, LengthMeter = laenge, DepthM = 2.5, Access = AccessDifficulty.Easy,
        }).Expected;

    [Theory]
    [InlineData("Kurzliner DN 300")]
    [InlineData("Kurzliner / Partliner")]
    [InlineData("Pointliner")]
    [InlineData("Partliner")]
    public void Kurzliner_bleibt_ein_stueckpreis_unabhaengig_von_der_laenge(string massnahme)
    {
        Assert.Equal(Erwartet("Kurzliner", 1), Erwartet(massnahme, 1));
        Assert.Equal(Erwartet(massnahme, 1), Erwartet(massnahme, 40));
    }

    [Fact]
    public void Linerendmanschette_ist_eine_manschette_kein_meter_liner()
        => Assert.Equal(Erwartet("Manschette", 1), Erwartet("Linerendmanschette", 40));

    [Fact]
    public void Ein_schlauchliner_bleibt_ein_meterpreis()
        => Assert.Equal(Erwartet("Schlauchliner", 1) * 40, Erwartet("Schlauchliner DN 300", 40));
}
