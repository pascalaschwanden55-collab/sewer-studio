using System.Linq;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class HaltungsgrafikScaleCalculatorTests
{
    [Fact]
    public void BuildTicks_erzeugt_gleichmaessige_schritte_inkl_endpunkt()
    {
        var ticks = HaltungsgrafikScaleCalculator.BuildTicks(10, 2);
        Assert.Equal(new[] { 0d, 2, 4, 6, 8, 10 }, ticks);
    }

    [Fact]
    public void BuildTicks_haengt_endpunkt_an_wenn_step_nicht_aufgeht()
    {
        var ticks = HaltungsgrafikScaleCalculator.BuildTicks(9, 2);
        Assert.Equal(new[] { 0d, 2, 4, 6, 8, 9 }, ticks);
    }

    [Fact]
    public void BuildTicks_leer_bei_nicht_positiver_eingabe()
    {
        Assert.Empty(HaltungsgrafikScaleCalculator.BuildTicks(0, 2));
        Assert.Empty(HaltungsgrafikScaleCalculator.BuildTicks(10, 0));
    }

    [Theory]
    [InlineData(10d, 2d)]    // 10/2 = 5  (in 4..8)
    [InlineData(40d, 5d)]    // 40/5 = 8  (in 4..8)
    [InlineData(1d, 0.2d)]   // 1/0.2 = 5 (in 4..8)
    public void ChooseTickStep_waehlt_schritt_fuer_4_bis_8_ticks(double length, double expectedStep)
        => Assert.Equal(expectedStep, HaltungsgrafikScaleCalculator.ChooseTickStep(length));

    [Fact]
    public void ChooseTickStep_fallback_groesster_schritt_bei_sehr_langer_haltung()
        => Assert.Equal(50d, HaltungsgrafikScaleCalculator.ChooseTickStep(1000));

    [Fact]
    public void ChooseTickStep_default_eins_bei_nicht_positiver_laenge()
        => Assert.Equal(1d, HaltungsgrafikScaleCalculator.ChooseTickStep(0));

    [Fact]
    public void ComputeScaleRatio_null_bei_nicht_positiver_laenge_oder_hoehe()
    {
        Assert.Null(HaltungsgrafikScaleCalculator.ComputeScaleRatio(0, 500));
        Assert.Null(HaltungsgrafikScaleCalculator.ComputeScaleRatio(10, 0));
    }

    [Fact]
    public void ComputeScaleRatio_rechnet_punkte_in_cm_und_rundet_auf_1_zu_100()
    {
        // 72 pt = 2.54 cm; Laenge 2.54 m -> 1 m/cm -> Massstab 1:100
        Assert.Equal(100, HaltungsgrafikScaleCalculator.ComputeScaleRatio(2.54, 72));
    }
}
