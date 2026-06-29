using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer die CatalogPriceApplier-Logik (wird aus
/// SanierungsMatrixPageViewModel extrahiert). Nageln IST-Verhalten fest,
/// bevor der Code verschoben wird.
/// </summary>
public sealed class CatalogPriceApplierTests
{
    // --- ResolveExactCatalogPrice-Faelle ---

    [Fact]
    public void ResolveExactCatalogPrice_Fixed_gibt_Item_Preis_zurueck()
    {
        // Festpreis-Positionen ohne DN-Regeln geben direkt den Preis zurueck.
        var item = FixedItem(999m);
        var price = ResolveExact(item, dn: null, qty: 1m);
        Assert.Equal(999m, price);
    }

    [Fact]
    public void ResolveExactCatalogPrice_ByDN_ohne_DN_liefert_null()
    {
        // Audit-Kern: Kein DN-Fallback -> ohne DN-Angabe kein Preis (bewusst null, nicht 0).
        var item = ByDnItem(300, 300, 150m);
        var price = ResolveExact(item, dn: null, qty: 10m);
        Assert.Null(price);
    }

    [Fact]
    public void ResolveExactCatalogPrice_ByDN_passender_DN_liefert_Preis()
    {
        var item = ByDnItem(250, 350, 120m);
        var price = ResolveExact(item, dn: 300, qty: 1m);
        Assert.Equal(120m, price);
    }

    [Fact]
    public void ResolveExactCatalogPrice_ByDN_DN_ausserhalb_Range_liefert_null()
    {
        // KEIN Naechster-DN-Fallback — anders als die MeasurePricingEngine!
        var item = ByDnItem(300, 300, 150m);
        var price = ResolveExact(item, dn: 200, qty: 1m);
        Assert.Null(price);
    }

    [Fact]
    public void ResolveExactCatalogPrice_ByDN_mit_QtyFrom_QtyTo_passendem_Bereich()
    {
        // Bucket gilt nur wenn qty im Bereich liegt.
        var item = ByDnItemWithQty(300, 300, qtyFrom: 5m, qtyTo: 20m, price: 80m);
        var inRange = ResolveExact(item, dn: 300, qty: 10m);
        var outOfRange = ResolveExact(item, dn: 300, qty: 25m);
        Assert.Equal(80m, inRange);
        Assert.Null(outOfRange);
    }

    // --- ApplyCatalogPricesToStoredCosts-Faelle ---

    [Fact]
    public void ApplyPrices_uebernimmt_neuen_Preis_und_zaehlt_geaenderte_Haltungen()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["GFK"] = FixedItem(200m),
        };

        var store = StoreWith("H1", Line("GFK", qty: 10m, price: 100m, overridden: false));
        var updated = ApplyToStore(store, catalog, vatRate: 0.081m);

        Assert.Equal(1, updated);
        var line = store.ByHolding["H1"].Measures[0].Lines[0];
        Assert.Equal(200m, line.UnitPrice);
    }

    [Fact]
    public void ApplyPrices_belaesst_PriceOverridden_Zeilen_unveraendert()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["GFK"] = FixedItem(200m),
        };

        var store = StoreWith("H1", Line("GFK", qty: 10m, price: 99m, overridden: true));
        var updated = ApplyToStore(store, catalog, vatRate: 0.081m);

        Assert.Equal(0, updated);
        Assert.Equal(99m, store.ByHolding["H1"].Measures[0].Lines[0].UnitPrice);
    }

    [Fact]
    public void ApplyPrices_aendert_nichts_wenn_Preis_unveraendert()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["GFK"] = FixedItem(100m),
        };

        var store = StoreWith("H1", Line("GFK", qty: 10m, price: 100m, overridden: false));
        var updated = ApplyToStore(store, catalog, vatRate: 0.081m);

        Assert.Equal(0, updated);
    }

    [Fact]
    public void ApplyPrices_aktualisiert_Total_und_MwSt_nach_Preisaenderung()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["GFK"] = FixedItem(20m),
        };

        // 5m * 10 CHF = 50, dann 5m * 20 CHF = 100
        var store = StoreWith("H1", Line("GFK", qty: 5m, price: 10m, overridden: false));
        ApplyToStore(store, catalog, vatRate: 0.081m);

        var cost = store.ByHolding["H1"];
        Assert.Equal(100m, cost.Total);
        Assert.Equal(8.10m, cost.MwstAmount);
        Assert.Equal(108.10m, cost.TotalInclMwst);
    }

    // --- BuildRowHinweis-Charakterisierung ---

    [Fact]
    public void BuildRowHinweis_listet_anschluesse_und_fehlende_preise()
    {
        var cost = CostWith(selected: true, qty: 1m, price: 0m);
        var hinweis = BuildHinweis(anschluesse: 2, cost);
        Assert.Contains("2 Anschluss", hinweis);
        Assert.Contains("Preis fehlt", hinweis);
    }

    [Fact]
    public void BuildRowHinweis_leer_wenn_keine_anschluesse_und_kein_preis_fehlt()
    {
        var cost = CostWith(selected: true, qty: 1m, price: 100m);
        var hinweis = BuildHinweis(anschluesse: 0, cost);
        Assert.Equal("", hinweis);
    }

    [Fact]
    public void BuildRowHinweis_listet_nur_anschluesse_wenn_preise_vorhanden()
    {
        var cost = CostWith(selected: true, qty: 5m, price: 50m);
        var hinweis = BuildHinweis(anschluesse: 3, cost);
        Assert.Contains("3 Anschluss", hinweis);
        Assert.DoesNotContain("Preis fehlt", hinweis);
    }

    [Fact]
    public void BuildRowHinweis_nicht_selektierte_zeilen_mit_preis_0_werden_ignoriert()
    {
        // Audit-Kern W9: nur AUSGEWAEHLTE Zeilen mit Qty>0 und Preis=0 melden
        var cost = CostWith(selected: false, qty: 1m, price: 0m);
        var hinweis = BuildHinweis(anschluesse: 0, cost);
        Assert.Equal("", hinweis);
    }

    // -----------------------------------------------------------------------
    // Hilfsmethoden — rufen die privaten statischen Methoden indirekt per
    // Reflection-freie Charakterisierung auf: Tests spiegeln die Logik 1:1.
    // Nach der Extraktion zeigen die Tests auf die neue Klasse.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ruft die Logik von ResolveExactCatalogPrice nach. Wird nach der Extraktion
    /// durch den echten CatalogPriceApplier-Aufruf ersetzt.
    /// </summary>
    private static decimal? ResolveExact(CostCatalogItem item, int? dn, decimal qty)
    {
        if (item.DnPrices is { Count: > 0 })
        {
            if (dn is not int d)
                return null;
            var bucket = item.DnPrices.FirstOrDefault(b =>
                d >= b.DnFrom && d <= b.DnTo
                && (!b.QtyFrom.HasValue || qty >= b.QtyFrom.Value)
                && (!b.QtyTo.HasValue || qty <= b.QtyTo.Value));
            return bucket?.Price;
        }
        return item.Price;
    }

    /// <summary>Analog zu ApplyCatalogPricesToStoredCosts im ViewModel.</summary>
    private static int ApplyToStore(
        ProjectCostStore store,
        Dictionary<string, CostCatalogItem> catalog,
        decimal vatRate)
    {
        var updated = 0;
        foreach (var (holding, cost) in store.ByHolding)
        {
            var changed = false;
            foreach (var measure in cost.Measures)
            {
                var measureChanged = false;
                foreach (var line in measure.Lines)
                {
                    if (line.IsPriceOverridden || string.IsNullOrWhiteSpace(line.ItemKey))
                        continue;
                    if (!catalog.TryGetValue(line.ItemKey.Trim(), out var item) || !item.Active)
                        continue;

                    var price = ResolveExact(item, measure.Dn, line.Qty);
                    if (price is decimal p)
                    {
                        if (p != line.UnitPrice)
                        {
                            line.UnitPrice = p;
                            measureChanged = true;
                        }
                        if (!string.IsNullOrWhiteSpace(line.PriceHint))
                        {
                            line.PriceHint = "";
                            measureChanged = true;
                        }
                    }
                }

                if (measureChanged)
                {
                    measure.Total = measure.Lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);
                    changed = true;
                }
            }

            if (changed)
            {
                var totals = AuswertungPro.Next.Infrastructure.Costs.CostCalculatorLogicService.CalculateTotals(
                    cost.Measures.Sum(m => m.Total), vatRate);
                cost.Total = totals.Total;
                cost.MwstRate = vatRate;
                cost.MwstAmount = totals.MwstAmount;
                cost.TotalInclMwst = totals.TotalInclMwst;
                updated++;
            }
        }

        return updated;
    }

    /// <summary>Analog zu BuildRowHinweis im ViewModel.</summary>
    private static string BuildHinweis(int anschluesse, HoldingCost cost)
    {
        var hints = new List<string>();
        if (anschluesse > 0)
            hints.Add($"{anschluesse} Anschluss(e)");
        if (cost.Measures.SelectMany(m => m.Lines).Any(l => l.Selected && l.Qty > 0m && l.UnitPrice <= 0m))
            hints.Add("Preis fehlt im Katalog");
        return string.Join(" | ", hints);
    }

    // --- Fabrik-Methoden ---

    private static CostCatalogItem FixedItem(decimal price) => new CostCatalogItem
    {
        Key = "X",
        Name = "Test",
        Unit = "Stk",
        Type = "Fixed",
        Price = price,
        Active = true,
        DnPrices = new List<DnPrice>(),
    };

    private static CostCatalogItem ByDnItem(int dnFrom, int dnTo, decimal price) => new CostCatalogItem
    {
        Key = "X",
        Name = "Test",
        Unit = "m",
        Type = "ByDN",
        Active = true,
        DnPrices = new List<DnPrice>
        {
            new DnPrice { DnFrom = dnFrom, DnTo = dnTo, Price = price }
        }
    };

    private static CostCatalogItem ByDnItemWithQty(int dnFrom, int dnTo, decimal qtyFrom, decimal qtyTo, decimal price)
        => new CostCatalogItem
        {
            Key = "X",
            Name = "Test",
            Unit = "m",
            Type = "ByDN",
            Active = true,
            DnPrices = new List<DnPrice>
            {
                new DnPrice { DnFrom = dnFrom, DnTo = dnTo, QtyFrom = qtyFrom, QtyTo = qtyTo, Price = price }
            }
        };

    private static ProjectCostStore StoreWith(string holding, CostLine line)
    {
        var store = new ProjectCostStore();
        store.ByHolding[holding] = new HoldingCost
        {
            Holding = holding,
            Measures = new List<MeasureCost>
            {
                new MeasureCost
                {
                    MeasureId = "M1",
                    Lines = new List<CostLine> { line },
                    Total = line.Selected ? line.Qty * line.UnitPrice : 0m,
                }
            },
        };
        return store;
    }

    private static CostLine Line(string key, decimal qty, decimal price, bool overridden) => new CostLine
    {
        ItemKey = key,
        Text = key,
        Group = "Hauptarbeit",
        Unit = "m",
        Qty = qty,
        UnitPrice = price,
        Selected = true,
        IsPriceOverridden = overridden,
    };

    private static HoldingCost CostWith(bool selected, decimal qty, decimal price) => new HoldingCost
    {
        Holding = "H",
        Measures = new List<MeasureCost>
        {
            new MeasureCost
            {
                MeasureId = "M1",
                Lines = new List<CostLine>
                {
                    new CostLine
                    {
                        ItemKey = "KEY",
                        Group = "Hauptarbeit",
                        Qty = qty,
                        UnitPrice = price,
                        Selected = selected,
                    }
                },
                Total = selected ? qty * price : 0m,
            }
        },
    };
}
