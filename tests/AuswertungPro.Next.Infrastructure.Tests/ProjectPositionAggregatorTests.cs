using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjectPositionAggregatorTests
{
    private static Dictionary<string, CostCatalogItem> Catalog() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["VORARBEIT_REINIGUNG"] = new() { Key = "VORARBEIT_REINIGUNG", NpkCode = "211.110", Chapter = "200", Type = "Fixed" },
            ["SCHLAUCHLINER_NADELFILZ"] = new() { Key = "SCHLAUCHLINER_NADELFILZ", NpkCode = "612.110", Chapter = "600", Type = "ByDN" },
            // GFK teilt sich die NPK-Nummer 612.110 mit Nadelfilz, ist aber eine andere Leistung.
            ["SCHLAUCHLINER_GFK"] = new() { Key = "SCHLAUCHLINER_GFK", NpkCode = "612.110", Chapter = "600", Type = "ByDN" },
        };

    private static CostLine Line(string itemKey, string text, string unit, decimal qty, decimal price, bool selected = true, string priceHint = "") =>
        new() { ItemKey = itemKey, Text = text, Unit = unit, Qty = qty, UnitPrice = price, Selected = selected, PriceHint = priceHint };

    private static HoldingCost Holding(string name, int? dn, params CostLine[] lines) =>
        new()
        {
            Holding = name,
            Measures = new List<MeasureCost> { new() { MeasureId = "M", Dn = dn, Lines = lines.ToList() } }
        };

    [Fact]
    public void Aggregate_SumsSameFixedPosition_AcrossHoldingsAndDn_AsOneRow()
    {
        var holdings = new[]
        {
            Holding("H1", 200, Line("VORARBEIT_REINIGUNG", "Reinigung", "m", 50m, 5m)),
            Holding("H2", 300, Line("VORARBEIT_REINIGUNG", "Reinigung", "m", 80m, 5m)),
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog());

        var reinigung = Assert.Single(result, p => p.NpkCode == "211.110");
        Assert.Equal(130m, reinigung.TotalQty);   // 50 + 80
        Assert.Equal(650m, reinigung.TotalNet);   // 130 * 5
        Assert.Equal(2, reinigung.HoldingCount);
        Assert.Null(reinigung.Dn);                // Fixed -> kein DN-Split
        Assert.Equal(5m, reinigung.UnitPrice);
        Assert.False(reinigung.IsVariablePrice);
    }

    [Fact]
    public void Aggregate_SplitsByDnPosition_PerDn()
    {
        var holdings = new[]
        {
            Holding("H1", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 270m)),
            Holding("H2", 300, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 30m, 320m)),
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog());

        var liner = result.Where(p => p.NpkCode == "612.110").ToList();
        Assert.Equal(2, liner.Count);
        Assert.Contains(liner, p => p.Dn == 200 && p.TotalQty == 40m && p.UnitPrice == 270m);
        Assert.Contains(liner, p => p.Dn == 300 && p.TotalQty == 30m && p.UnitPrice == 320m);
    }

    [Fact]
    public void Aggregate_SameDn_SumsAndKeepsFixedPrice()
    {
        var holdings = new[]
        {
            Holding("H1", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 270m)),
            Holding("H2", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 25m, 270m)),
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog());

        var liner = Assert.Single(result.Where(p => p.NpkCode == "612.110"));
        Assert.Equal(65m, liner.TotalQty);
        Assert.Equal(200, liner.Dn);
        Assert.Equal(270m, liner.UnitPrice);
        Assert.False(liner.IsVariablePrice);
    }

    [Fact]
    public void Aggregate_DifferentPricesSameDn_MarksVariable()
    {
        var holdings = new[]
        {
            Holding("H1", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 270m)),
            Holding("H2", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 25m, 290m)),
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog());

        var liner = Assert.Single(result.Where(p => p.NpkCode == "612.110"));
        Assert.True(liner.IsVariablePrice);
        Assert.Null(liner.UnitPrice);
        Assert.Equal(65m, liner.TotalQty);
    }

    [Fact]
    public void Aggregate_SkipsUnselectedAndZeroQtyLines()
    {
        var holding = new HoldingCost
        {
            Holding = "H1",
            Measures = new List<MeasureCost>
            {
                new()
                {
                    Dn = 200,
                    Lines = new List<CostLine>
                    {
                        Line("VORARBEIT_REINIGUNG", "R", "m", 50m, 5m, selected: false),
                        Line("VORARBEIT_REINIGUNG", "R", "m", 0m, 5m, selected: true),
                    }
                }
            }
        };

        var result = ProjectPositionAggregator.Aggregate(new[] { holding }, Catalog());

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_SameNpkCode_DifferentItemKey_StaysSeparate()
    {
        // Nadelfilz und GFK haben dieselbe NPK 612.110 und denselben DN, sind aber
        // verschiedene Leistungen -> duerfen NICHT in eine LV-Zeile verschmelzen.
        var holdings = new[]
        {
            Holding("H1", 250, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 300m)),
            Holding("H2", 250, Line("SCHLAUCHLINER_GFK", "GFK", "m", 30m, 200m)),
        };

        var result = ProjectPositionAggregator.Aggregate(holdings, Catalog());

        var liner = result.Where(p => p.NpkCode == "612.110").ToList();
        Assert.Equal(2, liner.Count);
        Assert.Contains(liner, p => p.ItemKey == "SCHLAUCHLINER_NADELFILZ" && p.TotalQty == 40m);
        Assert.Contains(liner, p => p.ItemKey == "SCHLAUCHLINER_GFK" && p.TotalQty == 30m);
    }

    [Fact]
    public void BuildCsv_ContainsChapterTitlesAndTotal()
    {
        var holdings = new[]
        {
            Holding("H1", 200, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 270m)),
            Holding("H2", 200, Line("VORARBEIT_REINIGUNG", "Reinigung", "m", 50m, 5m)),
        };

        var positions = ProjectPositionAggregator.Aggregate(holdings, Catalog());
        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions, "CHF");

        Assert.Contains("NPK 600 — Renovierung", csv);
        Assert.Contains("NPK 200 — Reinigung", csv);
        Assert.Contains("612.110", csv);
        Assert.Contains("TOTAL", csv);
    }

    [Fact]
    public void BuildCsv_AppendsPriceHintToPositionText()
    {
        var holdings = new[]
        {
            Holding("H1", 350, Line("SCHLAUCHLINER_NADELFILZ", "Nadelfilz", "m", 40m, 320m,
                priceHint: "Preis von DN 300 uebernommen")),
        };

        var positions = ProjectPositionAggregator.Aggregate(holdings, Catalog());
        var liner = Assert.Single(positions);

        Assert.Equal("Preis von DN 300 uebernommen", liner.PriceHint);

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions, "CHF");

        Assert.Contains("Nadelfilz (Preis von DN 300 uebernommen)", csv);
    }

    [Fact]
    public void ChapterTitle_112_IsPruefung()
    {
        var title = NpkLeistungsverzeichnisExporter.ChapterTitle("112");
        Assert.Contains("112", title);
        Assert.Contains("Prüfung", title);
    }
}
