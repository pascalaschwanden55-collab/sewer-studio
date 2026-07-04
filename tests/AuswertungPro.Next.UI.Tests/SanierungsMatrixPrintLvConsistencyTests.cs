using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixPrintLvConsistencyTests
{
    [Fact]
    public void Matrix_Druckcenter_und_Lv_weisen_Pauschalen_konsistent_aus()
    {
        var detailed = new HoldingCost
        {
            Holding = "H-1",
            Total = 100m,
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "SCHLAUCHLINER_NADELFILZ",
                    Dn = 200,
                    Lines =
                    [
                        new CostLine
                        {
                            ItemKey = "SCHLAUCHLINER_NADELFILZ",
                            Text = "Nadelfilz",
                            Unit = "m",
                            Qty = 10m,
                            UnitPrice = 10m,
                            Selected = true
                        }
                    ],
                    Total = 100m
                }
            ]
        };

        var rows = new[]
        {
            Row("H-1", hasDetailedCost: true, detailed, netCost: 100m),
            Row("H-2", hasDetailedCost: false, storedCost: null, netCost: 50m)
        };

        var printEntries = BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.081m);
        var printNet = printEntries.Sum(e => e.Cost.Total);
        var matrixDetailTotal = detailed.Total;
        var matrixPauschalen = TablePauschaleCostHelper.SummarizeRows(
            rows.Select(r => (r.StoredCost, r.HasDetailedCost ? 0m : r.NetCost)));

        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["SCHLAUCHLINER_NADELFILZ"] = new()
            {
                Key = "SCHLAUCHLINER_NADELFILZ",
                Name = "Nadelfilz",
                Unit = "m",
                Type = "ByDN",
                NpkCode = "612.110",
                Chapter = "600"
            }
        };

        var lvHoldings = printEntries
            .Where(e => !TablePauschaleCostHelper.IsFallbackPauschale(e.Cost))
            .Select(e => e.Cost)
            .ToList();
        var positions = ProjectPositionAggregator.Aggregate(lvHoldings, catalog);
        var lvTotal = positions.Sum(p => Math.Round(p.TotalNet, 2, MidpointRounding.AwayFromZero));
        var excludedPauschalen = printEntries
            .Where(e => TablePauschaleCostHelper.IsFallbackPauschale(e.Cost))
            .Sum(e => e.Cost.Total);

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(
            positions,
            "CHF",
            excludedPauschalen,
            excludedPauschaleHoldingCount: 1);

        Assert.Equal(printNet, matrixDetailTotal + matrixPauschalen.Total);
        Assert.Equal(printNet, lvTotal + excludedPauschalen);
        Assert.Contains("Nicht enthaltene Pauschalkosten (1 Haltung(en));;;;;50.00;", csv);
    }

    private static DruckcenterRowVm Row(
        string holding,
        bool hasDetailedCost,
        HoldingCost? storedCost,
        decimal netCost)
        => new()
        {
            Holding = holding,
            Owner = "Gemeinde",
            ExecutedBy = "Kanalsanierer",
            HasDetailedCost = hasDetailedCost,
            StoredCost = storedCost,
            NetCost = netCost
        };
}
