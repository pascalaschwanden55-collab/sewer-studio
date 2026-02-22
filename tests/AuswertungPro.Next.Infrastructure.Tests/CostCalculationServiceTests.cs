using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CostCalculationServiceTests
{
    [Fact]
    public void CalculateCombinedOffer_AppliesRabattSkontoAndMwstToNetTotals()
    {
        var service = new CostCalculationService(AppContext.BaseDirectory);

        var templates = new List<MeasureTemplate>
        {
            new()
            {
                Id = "TPL_A",
                Name = "Massnahme A",
                Lines = new List<TemplateLine>
                {
                    new() { Group = "Hauptarbeit", ItemRef = "ITEM_A", Qty = NumberElement(2m) }
                }
            },
            new()
            {
                Id = "TPL_B",
                Name = "Massnahme B",
                Lines = new List<TemplateLine>
                {
                    new() { Group = "Hauptarbeit", ItemRef = "ITEM_B", Qty = NumberElement(1m) }
                }
            }
        };

        var catalog = new PriceCatalog
        {
            Currency = "CHF",
            Items = new List<PriceItem>
            {
                new() { Id = "ITEM_A", Label = "A", Unit = "m", UnitPrice = 100m },
                new() { Id = "ITEM_B", Label = "B", Unit = "Stk", UnitPrice = 50m }
            }
        };

        var inputs = new List<MeasureInputs>
        {
            new() { Dn = 300, RabattPct = 10m, SkontoPct = 5m },
            new() { Dn = 300, RabattPct = 10m, SkontoPct = 5m }
        };

        var offer = service.CalculateCombinedOffer(templates, catalog, inputs, mwstPct: 8.1m);

        Assert.Equal(250.00m, offer.Totals.SubTotal);
        Assert.Equal(10.0m, offer.Totals.RabattPct);
        Assert.Equal(25.00m, offer.Totals.Rabatt);
        Assert.Equal(5.0m, offer.Totals.SkontoPct);
        Assert.Equal(11.25m, offer.Totals.Skonto);
        Assert.Equal(213.75m, offer.Totals.NetExclMwst);
        Assert.Equal(8.1m, offer.Totals.MwstPct);
        Assert.Equal(17.31m, offer.Totals.Mwst);
        Assert.Equal(231.06m, offer.Totals.TotalInclMwst);
    }

    [Fact]
    public void CalculateCombinedOffer_DoesNotThrow_WhenInputRowsMissing()
    {
        var service = new CostCalculationService(AppContext.BaseDirectory);

        var templates = new List<MeasureTemplate>
        {
            new()
            {
                Id = "TPL_A",
                Name = "Massnahme A",
                Lines = new List<TemplateLine>
                {
                    new() { Group = "Hauptarbeit", ItemRef = "ITEM_A", Qty = NumberElement(1m) }
                }
            },
            new()
            {
                Id = "TPL_B",
                Name = "Massnahme B",
                Lines = new List<TemplateLine>
                {
                    new() { Group = "Hauptarbeit", ItemRef = "ITEM_B", Qty = NumberElement(1m) }
                }
            }
        };

        var catalog = new PriceCatalog
        {
            Currency = "CHF",
            Items = new List<PriceItem>
            {
                new() { Id = "ITEM_A", Label = "A", Unit = "m", UnitPrice = 10m },
                new() { Id = "ITEM_B", Label = "B", Unit = "Stk", UnitPrice = 20m }
            }
        };

        var inputs = new List<MeasureInputs>
        {
            new() { Dn = 300, RabattPct = 3m, SkontoPct = 2m }
        };

        var offer = service.CalculateCombinedOffer(templates, catalog, inputs, mwstPct: 8.1m);

        Assert.Equal(30.00m, offer.Totals.SubTotal);
        Assert.Equal(3m, offer.Totals.RabattPct);
        Assert.Equal(2m, offer.Totals.SkontoPct);
        Assert.True(offer.Totals.TotalInclMwst > 0m);
    }

    private static JsonElement NumberElement(decimal value)
    {
        using var doc = JsonDocument.Parse(value.ToString(CultureInfo.InvariantCulture));
        return doc.RootElement.Clone();
    }
}
