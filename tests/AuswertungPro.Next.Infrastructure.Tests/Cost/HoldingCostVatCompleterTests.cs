using System.Collections.Generic;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Der Schacht-Massnahmen-Dialog hat die MWST-Felder nie gefuellt (Fehler vom
/// 2026-08-20). Bestehende schacht_empfehlungen.json liegen deshalb mit
/// MwstRate/MwstAmount/TotalInclMwst = 0 auf der Platte und erschienen im
/// Druckcenter ohne MWST. Diese Ergaenzung repariert den Altbestand beim Lesen,
/// ohne die Kundendatei zu veraendern.
/// </summary>
public sealed class HoldingCostVatCompleterTests
{
    private static HoldingCost SchachtOhneMwst() => new()
    {
        Holding = "80551",
        Total = 1100m,
        Measures = new List<MeasureCost>
        {
            new()
            {
                MeasureId = "SCHACHT", MeasureName = "Schachtsanierung",
                Total = 1100m,
                Lines = new List<CostLine>
                {
                    new() { Text = "Abdeckung ersetzen", Qty = 1m, UnitPrice = 1100m, Selected = true }
                }
            }
        }
    };

    [Fact]
    public void Ergaenzt_fehlende_Mwst_aus_dem_Projektsatz()
    {
        var ergaenzt = HoldingCostVatCompleter.Complete(SchachtOhneMwst(), 0.081m);

        Assert.Equal(0.081m, ergaenzt!.MwstRate);
        Assert.Equal(89.10m, ergaenzt.MwstAmount);
        Assert.Equal(1189.10m, ergaenzt.TotalInclMwst);
    }

    [Fact]
    public void Laesst_das_Original_unveraendert()
    {
        var original = SchachtOhneMwst();

        HoldingCostVatCompleter.Complete(original, 0.081m);

        Assert.Equal(0m, original.MwstRate);
        Assert.Equal(0m, original.MwstAmount);
        Assert.Equal(0m, original.TotalInclMwst);
    }

    [Fact]
    public void Ruehrt_bereits_gerechnete_Mwst_nicht_an()
    {
        var haltung = new HoldingCost
        {
            Holding = "80585-80707",
            Total = 19256.60m,
            MwstRate = 0.081m,
            MwstAmount = 1559.78m,
            TotalInclMwst = 20816.38m
        };

        var ergebnis = HoldingCostVatCompleter.Complete(haltung, 0.05m);

        Assert.Equal(0.081m, ergebnis!.MwstRate);
        Assert.Equal(1559.78m, ergebnis.MwstAmount);
        Assert.Equal(20816.38m, ergebnis.TotalInclMwst);
    }

    [Fact]
    public void Rechnet_bei_hinterlegtem_Satz_ohne_Betrag_nach()
    {
        var cost = SchachtOhneMwst() with { MwstRate = 0.081m };

        var ergebnis = HoldingCostVatCompleter.Complete(cost, 0.05m);

        // Der am Eintrag hinterlegte Satz gewinnt gegen den Projektsatz.
        Assert.Equal(0.081m, ergebnis!.MwstRate);
        Assert.Equal(89.10m, ergebnis.MwstAmount);
    }

    [Fact]
    public void Laesst_Eintraege_ohne_Nettobetrag_unveraendert()
    {
        var leer = new HoldingCost { Holding = "80999" };

        var ergebnis = HoldingCostVatCompleter.Complete(leer, 0.081m);

        Assert.Equal(0m, ergebnis!.MwstRate);
        Assert.Equal(0m, ergebnis.MwstAmount);
        Assert.Equal(0m, ergebnis.TotalInclMwst);
    }

    [Fact]
    public void Ohne_gueltigen_Projektsatz_wird_nichts_erfunden()
    {
        var ergebnis = HoldingCostVatCompleter.Complete(SchachtOhneMwst(), 0m);

        Assert.Equal(0m, ergebnis!.MwstRate);
        Assert.Equal(0m, ergebnis.MwstAmount);
    }

    [Fact]
    public void Null_bleibt_null()
    {
        Assert.Null(HoldingCostVatCompleter.Complete(null, 0.081m));
    }
}
