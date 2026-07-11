using System;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Restzeit-Rechner fuer lange Laeufe (Pipeline, Batch): gleitende Rate (EMA),
/// Warmup bevor die erste Schaetzung erscheint, deterministisch (Zeit wird hereingereicht).
/// </summary>
public sealed class EtaCalculatorTests
{
    [Fact]
    public void Warmup_liefert_noch_keine_schaetzung()
    {
        var eta = new EtaCalculator();
        for (var i = 1; i <= 4; i++)
        {
            var ergebnis = eta.MeldeFortschritt(i * 10, 100, TimeSpan.FromSeconds(i));
            Assert.Null(ergebnis.Restzeit);
        }
    }

    [Fact]
    public void Konstante_rate_ergibt_exakte_restzeit()
    {
        var eta = new EtaCalculator();
        EtaErgebnis? letztes = null;
        // 10 Einheiten pro Sekunde, 100 gesamt.
        for (var i = 1; i <= 5; i++)
            letztes = eta.MeldeFortschritt(i * 10, 100, TimeSpan.FromSeconds(i));

        Assert.NotNull(letztes!.Restzeit);
        Assert.Equal(5d, letztes.Restzeit!.Value.TotalSeconds, 1);
        Assert.Equal(10d, letztes.RateProSekunde!.Value, 1);
    }

    [Fact]
    public void Ratenwechsel_konvergiert_ueber_ema()
    {
        var eta = new EtaCalculator();
        var t = 0d;
        var erledigt = 0L;
        // Erst 10/s...
        for (var i = 0; i < 5; i++)
        {
            t += 1; erledigt += 10;
            eta.MeldeFortschritt(erledigt, 1000, TimeSpan.FromSeconds(t));
        }
        // ...dann 20/s.
        EtaErgebnis? letztes = null;
        for (var i = 0; i < 15; i++)
        {
            t += 1; erledigt += 20;
            letztes = eta.MeldeFortschritt(erledigt, 1000, TimeSpan.FromSeconds(t));
        }

        Assert.True(letztes!.RateProSekunde > 15d, $"Rate {letztes.RateProSekunde} sollte Richtung 20 konvergieren");
    }

    [Fact]
    public void Stillstand_liefert_keine_restzeit_mehr()
    {
        var eta = new EtaCalculator();
        var t = 0d;
        for (var i = 1; i <= 5; i++)
        {
            t += 1;
            eta.MeldeFortschritt(50, 100, TimeSpan.FromSeconds(t)); // erledigt bewegt sich nicht
        }
        for (var i = 0; i < 30; i++)
        {
            t += 1;
            var ergebnis = eta.MeldeFortschritt(50, 100, TimeSpan.FromSeconds(t));
            if (i > 20)
                Assert.Null(ergebnis.Restzeit); // Rate gegen 0 -> keine serioese Schaetzung
        }
    }

    [Fact]
    public void Gesamt_null_liefert_nichts()
    {
        var eta = new EtaCalculator();
        for (var i = 1; i <= 6; i++)
        {
            var ergebnis = eta.MeldeFortschritt(i, 0, TimeSpan.FromSeconds(i));
            Assert.Null(ergebnis.Restzeit);
        }
    }

    [Fact]
    public void Fertig_oder_ueber_gesamt_ergibt_rest_null_sekunden()
    {
        var eta = new EtaCalculator();
        EtaErgebnis? letztes = null;
        for (var i = 1; i <= 6; i++)
            letztes = eta.MeldeFortschritt(i * 25, 100, TimeSpan.FromSeconds(i));

        Assert.Equal(TimeSpan.Zero, letztes!.Restzeit);
    }
}
