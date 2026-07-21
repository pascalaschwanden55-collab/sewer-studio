using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer MeasurePricingEngine.
/// Fangen das IST-Verhalten der extrahierten Preis-/Mengen-Engine ein.
/// </summary>
public sealed class MeasurePricingEngineTests
{
    // -------------------------------------------------------------------------
    // ApplyCatalogPrices – Festpreis (Fixed)
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyCatalogPrices_Fixed_SetsUnitPriceFromCatalog()
    {
        var lines = Lines(Line("ITEM_A", qty: 1m));
        var catalog = Catalog(FixedItem("ITEM_A", price: 150m));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: null, onlyQtyBased: false);

        Assert.Equal(150m, lines[0].UnitPrice);
    }

    [Fact]
    public void ApplyCatalogPrices_Fixed_SkipsWhenOnlyQtyBasedTrue()
    {
        var lines = Lines(Line("ITEM_A", qty: 1m, unitPrice: 99m));
        var catalog = Catalog(FixedItem("ITEM_A", price: 150m));

        // Festpreis-Artikel hat keine Mengenregeln => soll uebersprungen werden
        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: null, onlyQtyBased: true);

        Assert.Equal(99m, lines[0].UnitPrice); // unveraendert
    }

    [Fact]
    public void ApplyCatalogPrices_Fixed_SkipsWhenPriceOverridden()
    {
        var lines = Lines(Line("ITEM_A", qty: 1m, unitPrice: 99m, priceOverridden: true));
        var catalog = Catalog(FixedItem("ITEM_A", price: 150m));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: null, onlyQtyBased: false);

        Assert.Equal(99m, lines[0].UnitPrice); // manuell eingetragen, nicht ueberschreiben
    }

    // -------------------------------------------------------------------------
    // ApplyCatalogPrices – ByDN exakter Treffer
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyCatalogPrices_ByDn_ExactMatch_SetsPrice()
    {
        var lines = Lines(Line("ITEM_B", qty: 1m));
        var catalog = Catalog(ByDnItem("ITEM_B",
            new DnPrice { DnFrom = 200, DnTo = 300, Price = 85m }));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: 250, onlyQtyBased: false);

        Assert.Equal(85m, lines[0].UnitPrice);
        Assert.Equal("", lines[0].PriceHint);
    }

    [Fact]
    public void ApplyCatalogPrices_ByDn_NoMatch_SetsZeroAndEmptyHint()
    {
        var lines = Lines(Line("ITEM_B", qty: 1m, unitPrice: 55m));
        var catalog = Catalog(ByDnItem("ITEM_B",
            new DnPrice { DnFrom = 400, DnTo = 500, Price = 200m }));

        // DN 100 passt nicht und kein Fallback moeglich? Nein — FindNearestDnCandidates
        // wuerde DN 400-500 als naechste liefern.
        // Hier testen wir: leere DnPrices-Liste -> kein Treffer -> Preis 0.
        var catalogEmpty = Catalog(new CostCatalogItem
        {
            Key = "ITEM_B", Name = "Test", Unit = "m",
            Type = "ByDN", Active = true,
            DnPrices = new System.Collections.Generic.List<DnPrice>()
        });
        MeasurePricingEngine.ApplyCatalogPrices(lines, catalogEmpty, dn: 100, onlyQtyBased: false);

        Assert.Equal(0m, lines[0].UnitPrice);
        Assert.Equal("", lines[0].PriceHint);
    }

    [Fact]
    public void ApplyCatalogPrices_ByDn_NearestFallback_SetsPriceAndHint()
    {
        var lines = Lines(Line("ITEM_B", qty: 1m));
        var catalog = Catalog(ByDnItem("ITEM_B",
            new DnPrice { DnFrom = 300, DnTo = 400, Price = 120m }));

        // DN 250 liegt ausserhalb des Buckets -> Fallback auf naechsten Bucket
        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: 250, onlyQtyBased: false);

        Assert.Equal(120m, lines[0].UnitPrice);
        Assert.Contains("DN", lines[0].PriceHint); // Hinweis enthaelt "Preis von DN..."
    }

    // -------------------------------------------------------------------------
    // ApplyCatalogPrices – Mengenregeln (QtyFrom/QtyTo)
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyCatalogPrices_ByDn_QtyRule_SelectsMatchingTier()
    {
        // Zwei Preisstufen: 1-5 Stk -> 100 CHF, 6-99 Stk -> 80 CHF
        var lines = Lines(Line("ITEM_C", qty: 8m));
        var catalog = Catalog(ByDnItem("ITEM_C",
            new DnPrice { DnFrom = 100, DnTo = 500, QtyFrom = 1m, QtyTo = 5m, Price = 100m },
            new DnPrice { DnFrom = 100, DnTo = 500, QtyFrom = 6m, QtyTo = 99m, Price = 80m }));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: 200, onlyQtyBased: false);

        Assert.Equal(80m, lines[0].UnitPrice);
    }

    [Fact]
    public void ApplyCatalogPrices_ByDn_QtyRule_OnlyQtyBased_UpdatesQtyItems()
    {
        var lines = Lines(Line("ITEM_C", qty: 8m, unitPrice: 50m));
        var catalog = Catalog(ByDnItem("ITEM_C",
            new DnPrice { DnFrom = 100, DnTo = 500, QtyFrom = 1m, QtyTo = 99m, Price = 80m }));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: 200, onlyQtyBased: true);

        Assert.Equal(80m, lines[0].UnitPrice); // hat QtyFrom -> wird aktualisiert
    }

    [Fact]
    public void ApplyCatalogPrices_ByDn_NoDn_ZeroPriceSet()
    {
        var lines = Lines(Line("ITEM_B", qty: 1m, unitPrice: 77m));
        var catalog = Catalog(ByDnItem("ITEM_B",
            new DnPrice { DnFrom = 200, DnTo = 300, Price = 85m }));

        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, dn: null, onlyQtyBased: false);

        Assert.Equal(0m, lines[0].UnitPrice);
    }

    // -------------------------------------------------------------------------
    // ApplyLengthToLines
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyLengthToLines_SetsQtyOnMeterLines()
    {
        var lines = Lines(
            Line("SCHLAUCHLINER_A", qty: 1m, unit: "m"),
            Line("INSTALL_B", qty: 1m, unit: "Stk"));

        MeasurePricingEngine.ApplyLengthToLines(lines, 45.30m);

        Assert.Equal(45.30m, lines[0].Qty);
        Assert.Equal(1m, lines[1].Qty); // Nicht-Meter-Einheit unveraendert
    }

    [Fact]
    public void ApplyLengthToLines_SkipsQtyOverriddenLines()
    {
        var lines = Lines(Line("SCHLAUCHLINER_A", qty: 20m, unit: "m", qtyOverridden: true));

        MeasurePricingEngine.ApplyLengthToLines(lines, 45.30m);

        Assert.Equal(20m, lines[0].Qty); // manuell eingetragen, nicht ueberschreiben
    }

    // -------------------------------------------------------------------------
    // ApplyConnectionsToLines
    // -------------------------------------------------------------------------

    [Fact]
    public void ApplyConnectionsToLines_SetsQtyOnConnectionLines()
    {
        var lines = Lines(Line("ROBOTER_ANSCHLUSS_FRAESEN", qty: 0m, unit: "Stk", selected: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 3m);

        Assert.Equal(3m, lines[0].Qty);
        Assert.True(lines[0].Selected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void ApplyConnectionsToLines_NonPositiveConnections_DisablesLines(int connections)
    {
        var lines = Lines(Line(
            "ANSCHLUSS_A",
            qty: 2m,
            unit: "Stk",
            selected: true,
            transferMarked: true,
            qtyOverridden: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, connections);

        Assert.Equal(0m, lines[0].Qty);
        Assert.False(lines[0].Selected);
        Assert.False(lines[0].TransferMarked);
        Assert.False(lines[0].IsQtyOverridden);
    }

    [Fact]
    public void ApplyConnectionsToLines_ReenablesLineDisabledByZeroCount()
    {
        // Zeile war durch "0 Anschluesse" deaktiviert (Selected=false, Qty=0)
        var lines = Lines(Line("ANSCHLUSS_A", qty: 0m, unit: "Stk", selected: false));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 2m);

        Assert.True(lines[0].Selected);
        Assert.Equal(2m, lines[0].Qty);
    }

    [Fact]
    public void ApplyConnectionsToLines_SkipsQtyOverriddenLines()
    {
        var lines = Lines(Line("ANSCHLUSS_A", qty: 5m, unit: "Stk", selected: true, qtyOverridden: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 2m);

        Assert.Equal(5m, lines[0].Qty); // manuell eingetragen, nicht ueberschreiben
    }

    [Fact]
    public void ApplyConnectionsToLines_ReactivatesZeroLineWithoutOverwritingManualQty()
    {
        var lines = Lines(Line(
            "ANSCHLUSS_A",
            qty: 0m,
            unit: "Stk",
            selected: false,
            qtyOverridden: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 2m);

        Assert.True(lines[0].Selected);
        Assert.Equal(0m, lines[0].Qty);
        Assert.True(lines[0].IsQtyOverridden);
    }

    [Fact]
    public void ApplyConnectionsToLines_DoesNotReactivateDeselectedPositiveLine()
    {
        var lines = Lines(Line(
            "ANSCHLUSS_A",
            qty: 5m,
            unit: "Stk",
            selected: false,
            transferMarked: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 2m);

        Assert.False(lines[0].Selected);
        Assert.Equal(2m, lines[0].Qty);
        Assert.True(lines[0].TransferMarked);
        Assert.False(lines[0].IsQtyOverridden);
    }

    [Fact]
    public void ApplyConnectionsToLines_LeavesNonConnectionLineUnchanged()
    {
        var lines = Lines(Line(
            "POSITION",
            qty: 5m,
            unit: "Stk",
            selected: true,
            transferMarked: true,
            qtyOverridden: true));

        MeasurePricingEngine.ApplyConnectionsToLines(lines, 0m);

        Assert.Equal(5m, lines[0].Qty);
        Assert.True(lines[0].Selected);
        Assert.True(lines[0].TransferMarked);
        Assert.True(lines[0].IsQtyOverridden);
    }

    // -------------------------------------------------------------------------
    // TryReadConnectionsFromLines
    // -------------------------------------------------------------------------

    [Fact]
    public void TryReadConnectionsFromLines_ReturnsMaxQty()
    {
        var lines = new List<CostLine>
        {
            new() { ItemKey = "ANSCHLUSS_A", Qty = 3m, Selected = true },
            new() { ItemKey = "ANSCHLUSS_B", Qty = 5m, Selected = true },
            new() { ItemKey = "SCHLAUCHLINER", Qty = 10m, Selected = true }
        };

        var result = MeasurePricingEngine.TryReadConnectionsFromLines(lines);

        Assert.Equal(5m, result);
    }

    [Fact]
    public void TryReadConnectionsFromLines_ReturnsNullWhenNoConnectionLines()
    {
        var lines = new List<CostLine>
        {
            new() { ItemKey = "SCHLAUCHLINER", Qty = 10m, Selected = true }
        };

        var result = MeasurePricingEngine.TryReadConnectionsFromLines(lines);

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // Hilfsfunktionen
    // -------------------------------------------------------------------------

    private static List<CostLine> Lines(params CostLine[] lines)
        => new(lines);

    private static CostLine Line(
        string itemKey,
        decimal qty = 1m,
        decimal unitPrice = 0m,
        string unit = "Stk",
        bool selected = true,
        bool priceOverridden = false,
        bool qtyOverridden = false,
        bool transferMarked = false)
        => new()
        {
            ItemKey = itemKey,
            Text = itemKey,
            Group = "Hauptarbeit",
            Qty = qty,
            UnitPrice = unitPrice,
            Unit = unit,
            Selected = selected,
            TransferMarked = transferMarked,
            IsPriceOverridden = priceOverridden,
            IsQtyOverridden = qtyOverridden
        };

    private static CostCatalogItem FixedItem(string key, decimal price)
        => new()
        {
            Key = key, Name = key, Unit = "Stk",
            Type = "Fixed", Price = price, Active = true
        };

    private static CostCatalogItem ByDnItem(string key, params DnPrice[] prices)
        => new()
        {
            Key = key, Name = key, Unit = "m",
            Type = "ByDN", Active = true,
            DnPrices = new List<DnPrice>(prices)
        };

    private static IReadOnlyDictionary<string, CostCatalogItem> Catalog(
        params CostCatalogItem[] items)
        => items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
}
