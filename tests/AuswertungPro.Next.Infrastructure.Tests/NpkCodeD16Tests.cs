using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// NPK-Mittelweg (Offerten-Pruefung 11.07.2026): Jede Katalog-Position traegt neben der
/// Revisions-Nummer (NpkCode, D/V27) optional die D/16-Praxisnummer aus den echten
/// Unternehmer-Offerten (NpkCodeD16). Das LV weist beide aus, damit App-Devis direkt
/// mit heutigen Offerten/Rechnungen vergleichbar sind.
/// </summary>
public sealed class NpkCodeD16Tests
{
    // ── Store: Merge-/Override-Pflege (gleiche Garantien wie NpkCode, Audit W18) ──

    [Fact]
    public void PreserveNpkMetadata_FuelltD16AusDefault_WennOverrideLeer()
    {
        var overrideItem = new CostCatalogItem { Key = "K", NpkCode = "", Chapter = "", NpkCodeD16 = "" };
        var defaultItem = new CostCatalogItem { Key = "K", NpkCode = "933.300", Chapter = "900", NpkCodeD16 = "700.712.107" };

        var merged = CostCatalogStore.PreserveNpkMetadata(overrideItem, defaultItem);

        Assert.Equal("700.712.107", merged.NpkCodeD16);
    }

    [Fact]
    public void BuildUserOverridesForSave_LeertDefaultgleicheD16_DamitDefaultKorrekturenAnkommen()
    {
        var defaults = new CostCatalog
        {
            Items = { new CostCatalogItem { Key = "K", NpkCode = "933.300", NpkCodeD16 = "700.712.107" } }
        };
        var current = new CostCatalog
        {
            Items = { new CostCatalogItem { Key = "K", NpkCode = "933.300", NpkCodeD16 = "700.712.107", Price = 99m } }
        };

        var toSave = CostCatalogStore.BuildUserOverridesForSave(current, defaults);

        var item = Assert.Single(toSave.Items);
        Assert.Equal("", item.NpkCodeD16); // default-gleich -> nicht einfrieren
        Assert.Equal(99m, item.Price);     // Preis-Override bleibt
    }

    // ── Aggregator: D16 wird wie NpkCode via ItemKey nachgeschlagen ──

    [Fact]
    public void Aggregate_ReichtD16NummerDurch()
    {
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["SCHACHT_STEIGEISEN_ERSETZEN"] = new()
            {
                Key = "SCHACHT_STEIGEISEN_ERSETZEN",
                NpkCode = "933.300",
                NpkCodeD16 = "700.712.107",
                Chapter = "900",
                Type = "Fixed"
            }
        };
        var holdings = new[]
        {
            new HoldingCost
            {
                Holding = "H1",
                Measures = new List<MeasureCost>
                {
                    new()
                    {
                        MeasureId = "M",
                        Lines = new List<CostLine>
                        {
                            new() { ItemKey = "SCHACHT_STEIGEISEN_ERSETZEN", Text = "Steigeisen", Unit = "St", Qty = 4m, UnitPrice = 150m, Selected = true }
                        }
                    }
                }
            }
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, catalog);

        var pos = Assert.Single(result);
        Assert.Equal("933.300", pos.NpkCode);
        Assert.Equal("700.712.107", pos.NpkCodeD16);
    }

    // ── CSV-LV: eigene D/16-Spalte, excel-fest wie die NPK-Spalte ──

    [Fact]
    public void BuildCsv_EnthaeltD16SpalteExcelFest()
    {
        var positions = new List<AggregatedPosition>
        {
            new(
                NpkCode: "933.300",
                Chapter: "900",
                ItemKey: "SCHACHT_STEIGEISEN_ERSETZEN",
                Text: "Steigeisen ersetzen",
                Unit: "St",
                Dn: null,
                TotalQty: 4m,
                TotalNet: 600m,
                HoldingCount: 1,
                IsVariablePrice: false,
                UnitPrice: 150m,
                NpkCodeD16: "700.712.107")
        };

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions);

        Assert.Contains("NPK D/16", csv);            // Spaltenkopf
        Assert.Contains("=\"700.712.107\"", csv);    // Wert excel-fest (wie Audit K4)
    }

    [Fact]
    public void BuildWorkbook_EnthaeltD16Spalte()
    {
        var positions = new List<AggregatedPosition>
        {
            new(
                NpkCode: "933.300",
                Chapter: "900",
                ItemKey: "SCHACHT_STEIGEISEN_ERSETZEN",
                Text: "Steigeisen ersetzen",
                Unit: "St",
                Dn: null,
                TotalQty: 4m,
                TotalNet: 600m,
                HoldingCount: 1,
                IsVariablePrice: false,
                UnitPrice: 150m,
                NpkCodeD16: "700.712.107")
        };

        var bytes = NpkLeistungsverzeichnisExcelExporter.BuildWorkbook(positions);

        using var ms = new System.IO.MemoryStream(bytes);
        using var wb = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("NPK D/16", ws.Cell(7, 2).GetString());       // Spaltenkopf (headerRow 7)
        Assert.Equal("700.712.107", ws.Cell(9, 2).GetString());    // Datenzeile (8=Kapitel, 9=Position)
    }
}
