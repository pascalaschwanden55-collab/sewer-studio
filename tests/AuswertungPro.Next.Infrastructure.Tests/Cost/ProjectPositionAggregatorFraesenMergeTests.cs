using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Cost;

public sealed class ProjectPositionAggregatorFraesenMergeTests
{
    // Fraesen wird oberflaechlich in Metern erfasst (m/29), im LV aber als NPK 311 in Stunden
    // ausgewiesen; der Kanalroboter (h/290) bestimmt die Umrechnung. Beide -> eine LV-Zeile.
    private static readonly Dictionary<string, CostCatalogItem> Catalog = new()
    {
        ["VORARBEIT_FRAESEN"] = new CostCatalogItem
        {
            Key = "VORARBEIT_FRAESEN", Name = "Fräsen / Hindernisse entfernen",
            Unit = "m", Type = "Fixed", Price = 29m, NpkCode = "311.100", Chapter = "300"
        },
        ["HAUPTARBEIT_HINDERNISSE_ROBOTER"] = new CostCatalogItem
        {
            Key = "HAUPTARBEIT_HINDERNISSE_ROBOTER", Name = "Ablagerungen / Hindernisse mit Roboter fräsen",
            Unit = "h", Type = "Fixed", Price = 290m, NpkCode = "311.100", Chapter = "300"
        },
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
    public void Fraesen_Meter_wird_im_LV_in_Stunden_umgerechnet_und_mit_Roboter_gemergt()
    {
        var holdings = new[]
        {
            Holding("H1", "VORARBEIT_FRAESEN", 10m, "m", 29m),               // 10 m Fräsen (Häkchen)
            Holding("H2", "HAUPTARBEIT_HINDERNISSE_ROBOTER", 3m, "h", 290m), // 3 h Kanalroboter
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog);

        var npk311 = result.Where(p => p.NpkCode == "311.100").ToList();
        Assert.Single(npk311);                            // EINE Zeile
        Assert.Equal("h", npk311[0].Unit);                // in Stunden
        // 10 m * 29 / 290 = 1 h  +  3 h  = 4 h
        Assert.Equal(4m, npk311[0].TotalQty);
        // Geldbetrag erhalten: 10*29 + 3*290 = 290 + 870 = 1160
        Assert.Equal(1160m, npk311[0].TotalNet);
        Assert.Contains("m", npk311[0].Text);             // Meterzahl als Zusatz behalten
    }

    [Fact]
    public void Verschiedene_Leistungen_bleiben_getrennt()
    {
        var catalog = new Dictionary<string, CostCatalogItem>
        {
            ["A"] = new() { Key = "A", Name = "A", Unit = "m", Type = "Fixed", Price = 10m, NpkCode = "612.111", Chapter = "600" },
            ["B"] = new() { Key = "B", Name = "B", Unit = "m", Type = "Fixed", Price = 10m, NpkCode = "612.113", Chapter = "600" },
        };
        var holdings = new[] { Holding("H1", "A", 1m, "m", 10m), Holding("H2", "B", 1m, "m", 10m) };

        var result = ProjectPositionAggregator.Aggregate(holdings, catalog);
        Assert.Equal(2, result.Count);
    }
}
