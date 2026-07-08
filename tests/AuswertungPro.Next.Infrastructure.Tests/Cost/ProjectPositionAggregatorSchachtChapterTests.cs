using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

/// <summary>
/// Schacht-Kosten (NPK Kap. 700) und Haltungs-Kosten (Kap. 600) landen im selben Aggregator und
/// werden korrekt getrennt und sortiert (700 hinter 600).
/// </summary>
public sealed class ProjectPositionAggregatorSchachtChapterTests
{
    private static readonly Dictionary<string, CostCatalogItem> Catalog = new()
    {
        ["LINER"] = new() { Key = "LINER", Name = "Liner liefern", Unit = "m", Type = "ByDN", Price = 100m, NpkCode = "612.111", Chapter = "600" },
        ["SCHACHT_SANIERUNG_PAUSCHAL"] = new() { Key = "SCHACHT_SANIERUNG_PAUSCHAL", Name = "Schachtsanierung pauschal", Unit = "St", Type = "Fixed", Price = 1500m, NpkCode = "700.001", Chapter = "700" },
    };

    private static HoldingCost Holding(string name, string itemKey, decimal qty, string unit, decimal price) => new()
    {
        Holding = name,
        Measures = new List<MeasureCost>
        {
            new()
            {
                MeasureId = "M", MeasureName = "M",
                Lines = new List<CostLine>
                {
                    new() { ItemKey = itemKey, Text = "x", Unit = unit, Qty = qty, UnitPrice = price, Selected = true }
                }
            }
        }
    };

    [Fact]
    public void Haltung_600_und_Schacht_700_ergeben_beide_Kapitel_700_nach_600()
    {
        var holdings = new[]
        {
            Holding("H1", "LINER", 10m, "m", 100m),                    // Kap. 600
            Holding("KS 60191", "SCHACHT_SANIERUNG_PAUSCHAL", 1m, "St", 1500m), // Kap. 700
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog);

        Assert.Contains(result, p => p.Chapter == "600");
        Assert.Contains(result, p => p.Chapter == "700");

        var idx600 = result.ToList().FindIndex(p => p.Chapter == "600");
        var idx700 = result.ToList().FindIndex(p => p.Chapter == "700");
        Assert.True(idx600 < idx700, "Kapitel 700 muss nach 600 sortiert sein.");

        var schacht = result.Single(p => p.NpkCode == "700.001");
        Assert.Equal(1m, schacht.TotalQty);
        Assert.Equal(1500m, schacht.TotalNet);
    }
}
