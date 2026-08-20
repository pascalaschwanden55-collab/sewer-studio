using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Mengen der Sanierungsverfahren fuer das Projekt-Cockpit. Zaehlt nach denselben
/// Regeln wie die Kostenzusammenstellung — nur ausgewaehlte Zeilen, gleiche
/// Erkennung ueber <see cref="SpecialStatsClassifier"/>. Damit koennen Cockpit und
/// Angebots-PDF nicht auseinanderlaufen.
/// </summary>
public sealed class RehabilitationQuantityCalculatorTests
{
    private static ProjectCostStore Store(params (string Holding, CostLine[] Lines)[] eintraege)
    {
        var store = new ProjectCostStore();
        foreach (var (holding, lines) in eintraege)
        {
            store.ByHolding[holding] = new HoldingCost
            {
                Holding = holding,
                Measures = new List<MeasureCost>
                {
                    new() { MeasureId = "M", MeasureName = "Massnahme", Lines = lines.ToList() }
                }
            };
        }

        return store;
    }

    private static CostLine Zeile(string key, string text, string unit, decimal qty, decimal price, bool selected = true)
        => new() { ItemKey = key, Text = text, Unit = unit, Qty = qty, UnitPrice = price, Selected = selected };

    [Fact]
    public void Zaehlt_Mengen_und_Betraege_je_Verfahren()
    {
        var store = Store(
            ("H-1", [Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "m", 12.5m, 200m)]),
            ("H-2", [Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "m", 7.5m, 200m)]));

        var gfk = Assert.Single(RehabilitationQuantityCalculator.Calculate(store));

        Assert.Equal(SpecialStatsCategory.InlinerGfk, gfk.Category);
        Assert.Equal("Inliner GFK", gfk.Label);
        Assert.Equal(20m, gfk.Qty);
        Assert.Equal("m", gfk.Unit);
        Assert.Equal(4000m, gfk.Net);
    }

    [Fact]
    public void Zaehlt_Kurzliner_getrennt_von_den_Inlinern()
    {
        var store = Store(("H-1",
        [
            Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "m", 10m, 200m),
            Zeile("KURZLINER_PARTLINER", "Kurzliner (Pointliner, Partliner)", "Stk", 3m, 850m)
        ]));

        var ergebnis = RehabilitationQuantityCalculator.Calculate(store);

        var kurz = Assert.Single(ergebnis, e => e.Category == SpecialStatsCategory.Kurzliner);
        Assert.Equal(3m, kurz.Qty);
        Assert.Equal("stk", kurz.Unit);
        Assert.Equal(2550m, kurz.Net);
    }

    [Fact]
    public void Nicht_ausgewaehlte_Zeilen_zaehlen_nicht()
    {
        var store = Store(("H-1",
        [
            Zeile("MANSCHETTE_EDELSTAHL", "Manschette", "Stk", 2m, 400m),
            Zeile("MANSCHETTE_EDELSTAHL", "Manschette", "Stk", 5m, 400m, selected: false)
        ]));

        var manschetten = Assert.Single(RehabilitationQuantityCalculator.Calculate(store));

        Assert.Equal(2m, manschetten.Qty);
        Assert.Equal(800m, manschetten.Net);
    }

    [Fact]
    public void Verschiedene_Einheiten_werden_als_variabel_ausgewiesen()
    {
        var store = Store(("H-1",
        [
            Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "m", 10m, 200m),
            Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "lfm", 5m, 200m)
        ]));

        var gfk = Assert.Single(RehabilitationQuantityCalculator.Calculate(store));

        Assert.Equal(15m, gfk.Qty);
        Assert.Equal("variabel", gfk.Unit);
    }

    [Fact]
    public void Verfahren_ohne_Menge_erscheinen_nicht()
    {
        var store = Store(("H-1", [Zeile("SPUELEN", "Kanal spuelen", "m", 40m, 8m)]));

        Assert.Empty(RehabilitationQuantityCalculator.Calculate(store));
    }

    [Fact]
    public void Behaelt_die_feste_Ausgabereihenfolge()
    {
        var store = Store(("H-1",
        [
            Zeile("MANSCHETTE_EDELSTAHL", "Manschette", "Stk", 1m, 400m),
            Zeile("KURZLINER_PARTLINER", "Kurzliner", "Stk", 1m, 850m),
            Zeile("SCHLAUCHLINER_GFK", "Schlauchliner GFK", "m", 1m, 200m)
        ]));

        var reihenfolge = RehabilitationQuantityCalculator.Calculate(store)
            .Select(e => e.Category)
            .ToArray();

        Assert.Equal(
            [SpecialStatsCategory.InlinerGfk, SpecialStatsCategory.Kurzliner, SpecialStatsCategory.Manschette],
            reihenfolge);
    }

    [Fact]
    public void Ohne_Kostenspeicher_kommt_eine_leere_Liste()
    {
        Assert.Empty(RehabilitationQuantityCalculator.Calculate(null));
    }
}

/// <summary>
/// Anzeige-Texte der Verfahrenszeilen. Fest auf de-CH, damit der Ausdruck auf jeder
/// Windows-Kultur gleich aussieht (die Kulturfalle hat hier schon Geld gekostet).
/// </summary>
public sealed class RehabilitationQuantityTextTests
{
    private static RehabilitationQuantity Zeile(decimal qty, string unit, decimal net)
        => new(SpecialStatsCategory.InlinerGfk, "Inliner GFK", qty, unit, net);

    [Fact]
    public void Ganze_Mengen_ohne_Nachkommastellen()
    {
        Assert.Equal("24 m", Zeile(24m, "m", 0m).QtyText);
    }

    [Fact]
    public void Gebrochene_Mengen_mit_zwei_Nachkommastellen()
    {
        Assert.Equal("24.50 m", Zeile(24.5m, "m", 0m).QtyText);
    }

    [Fact]
    public void Betrag_mit_Schweizer_Tausendertrennung()
    {
        Assert.Equal("12’500", Zeile(1m, "m", 12500m).NetText);
    }

    [Fact]
    public void Ohne_Einheit_bleibt_nur_die_Menge()
    {
        Assert.Equal("7", Zeile(7m, "", 0m).QtyText);
    }
}
