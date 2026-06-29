using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests für LegacyOfferTotalsCalculator.
/// Alle erwarteten Werte wurden aus der bisherigen Kaskadenlogik in
/// CostCalculationService.CalculateOffer / CalculateCombinedOffer abgeleitet.
/// </summary>
public sealed class LegacyOfferTotalsCalculatorTests
{
    // ---------------------------------------------------------------------------
    // Hilfsmethode: Positionsliste mit einer einzigen Betrag-Zeile aufbauen
    // ---------------------------------------------------------------------------
    private static List<OfferLine> Lines(decimal amount)
        => new()
        {
            new OfferLine
            {
                Label = "Testposition",
                Unit  = "m",
                Qty   = 1m,
                UnitPrice = amount,
                Amount = amount
            }
        };

    // ---------------------------------------------------------------------------
    // Keine Abzüge, keine MwSt
    // ---------------------------------------------------------------------------
    [Fact]
    public void BuildTotals_NullPercents_SubTotalEqualsTotal()
    {
        var result = LegacyOfferTotalsCalculator.BuildTotals(Lines(100m), 0m, 0m, 0m, "CHF");

        Assert.Equal(100.00m, result.SubTotal);
        Assert.Equal(0.00m,   result.Rabatt);
        Assert.Equal(0.00m,   result.Skonto);
        Assert.Equal(100.00m, result.NetExclMwst);
        Assert.Equal(0.00m,   result.Mwst);
        Assert.Equal(100.00m, result.TotalInclMwst);
        Assert.Equal("CHF",   result.Currency);
    }

    // ---------------------------------------------------------------------------
    // Repräsentativer Fall aus CostCalculationServiceTests (250 CHF, 10%/5%/8.1%)
    // Erwartete Werte identisch zu CalculateCombinedOffer_AppliesRabattSkontoAndMwstToNetTotals
    // ---------------------------------------------------------------------------
    [Fact]
    public void BuildTotals_FullKaskade_MatchesCalculateCombinedOfferExpectedValues()
    {
        // 2 × 100 + 1 × 50 = 250
        var lines = new List<OfferLine>
        {
            new() { Amount = 200m, Label = "A", Unit = "m", Qty = 2m, UnitPrice = 100m },
            new() { Amount =  50m, Label = "B", Unit = "Stk", Qty = 1m, UnitPrice = 50m }
        };

        var result = LegacyOfferTotalsCalculator.BuildTotals(lines, 10m, 5m, 8.1m, "CHF");

        Assert.Equal(250.00m,  result.SubTotal);
        Assert.Equal(10m,      result.RabattPct);
        Assert.Equal(25.00m,   result.Rabatt);
        Assert.Equal(5m,       result.SkontoPct);
        Assert.Equal(11.25m,   result.Skonto);
        Assert.Equal(213.75m,  result.NetExclMwst);
        Assert.Equal(8.1m,     result.MwstPct);
        Assert.Equal(17.31m,   result.Mwst);
        Assert.Equal(231.06m,  result.TotalInclMwst);
        Assert.Equal("CHF",    result.Currency);
    }

    // ---------------------------------------------------------------------------
    // Zeilen ohne Amount (Warnung/Preis fehlt) werden nicht summiert
    // ---------------------------------------------------------------------------
    [Fact]
    public void BuildTotals_LinesWithNullAmount_AreExcludedFromSubTotal()
    {
        var lines = new List<OfferLine>
        {
            new() { Amount = 80m,  Label = "Vorhanden",  Unit = "m",   Qty = 1m },
            new() { Amount = null, Label = "Preis fehlt", Unit = "Stk", Qty = 1m }
        };

        var result = LegacyOfferTotalsCalculator.BuildTotals(lines, 0m, 0m, 0m, "EUR");

        Assert.Equal(80.00m, result.SubTotal);
        Assert.Equal(80.00m, result.TotalInclMwst);
        Assert.Equal("EUR",  result.Currency);
    }

    // ---------------------------------------------------------------------------
    // Leere Positionsliste -> alles 0
    // ---------------------------------------------------------------------------
    [Fact]
    public void BuildTotals_EmptyLines_AllZero()
    {
        var result = LegacyOfferTotalsCalculator.BuildTotals(new List<OfferLine>(), 5m, 2m, 8.1m, "CHF");

        Assert.Equal(0m, result.SubTotal);
        Assert.Equal(0m, result.Rabatt);
        Assert.Equal(0m, result.Skonto);
        Assert.Equal(0m, result.NetExclMwst);
        Assert.Equal(0m, result.Mwst);
        Assert.Equal(0m, result.TotalInclMwst);
    }

    // ---------------------------------------------------------------------------
    // Dezimal-Rundung: Skonto auf 2 Stellen (nicht erst am Ende)
    // SubTotal = 100, Rabatt 0%, SubNachRabatt = 100
    // Skonto 3.333...% = round(100 * 3.333.../100, 2) = round(3.333..., 2) = 3.33
    // NetExcl = round(100 - 3.33, 2) = 96.67
    // Mwst = round(96.67 * 7.7/100, 2) = round(7.44359, 2) = 7.44
    // Total = round(96.67 + 7.44, 2) = 104.11
    // ---------------------------------------------------------------------------
    [Fact]
    public void BuildTotals_RoundingAtEachStep_IsConsistentWithCascade()
    {
        var result = LegacyOfferTotalsCalculator.BuildTotals(
            Lines(100m),
            rabattPct: 0m,
            skontoPct: 3.333333333m,
            mwstPct: 7.7m,
            currency: "CHF");

        // round(100 * 3.333333333 / 100, 2) = round(3.333333333, 2) = 3.33
        Assert.Equal(3.33m,   result.Skonto);
        Assert.Equal(96.67m,  result.NetExclMwst);
        // round(96.67 * 7.7 / 100, 2) = round(7.44359, 2) = 7.44
        Assert.Equal(7.44m,   result.Mwst);
        Assert.Equal(104.11m, result.TotalInclMwst);
    }
}
