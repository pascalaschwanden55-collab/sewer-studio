using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.Application.Costs;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageSpecialStatsCalculatorTests
{
    [Fact]
    public void Compute_summiert_ausgewaehlte_spezialpositionen_und_positions_buckets()
    {
        var rows = new[]
        {
            Row("H-1", Cost(
                Line("SCHLAUCHLINER_GFK", "Einbau", "", 12m),
                Line("", "Manschette DN 300", "", 2m),
                Line("SCHLAUCHLINER_GFK", "nicht selektiert", "", 100m, selected: false))),
            Row("H-2", Cost(
                Line("SCHLAUCHLINER_GFK", "Einbau", "", 3m))),
            Row("H-3", cost: null)
        };

        var result = BuilderPageSpecialStatsCalculator.Compute(rows);

        Assert.Equal(15m, result.InlinerGfk);
        Assert.Equal(0m, result.InlinerNadelfilz);
        Assert.Equal(2m, result.Manschetten);
        Assert.Equal(0m, result.Linerendmanschetten);

        Assert.Collection(
            result.PositionStats,
            first =>
            {
                Assert.Equal("Inliner GFK", first.Category);
                Assert.Equal("SCHLAUCHLINER_GFK - Einbau", first.Position);
                Assert.Equal(15m, first.Qty);
                Assert.Equal("m", first.Unit);
                Assert.Equal(2, first.HoldingCount);
            },
            second =>
            {
                Assert.Equal("Manschetten", second.Category);
                Assert.Equal("Manschette DN 300", second.Position);
                Assert.Equal(2m, second.Qty);
                Assert.Equal("stk", second.Unit);
                Assert.Equal(1, second.HoldingCount);
            });
    }

    [Fact]
    public void Compute_nutzt_text_als_position_wenn_er_den_key_enthaelt()
    {
        var rows = new[]
        {
            Row("H-1", Cost(Line("LEM", "LEM Linerendmanschette", "", 4m)))
        };

        var result = BuilderPageSpecialStatsCalculator.Compute(rows);

        var stat = Assert.Single(result.PositionStats);
        Assert.Equal("Linerendmanschetten (LEM)", stat.Category);
        Assert.Equal("LEM Linerendmanschette", stat.Position);
        Assert.Equal("stk", stat.Unit);
        Assert.Equal(4m, result.Linerendmanschetten);
    }

    [Fact]
    public void Compute_zaehlt_Element_Nicht_Als_Lem_Und_Stimmt_Mit_PdfClassifier_Ueberein()
    {
        var line = Line("", "Element reparieren", "", 5m);
        var rows = new[] { Row("H-1", Cost(line)) };

        var result = BuilderPageSpecialStatsCalculator.Compute(rows);
        var pdfClassified = SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out _);

        Assert.False(pdfClassified);
        Assert.Equal(0m, result.Linerendmanschetten);
        Assert.Empty(result.PositionStats);
    }

    private static DruckcenterRowVm Row(string holding, HoldingCost? cost)
        => new()
        {
            Holding = holding,
            StoredCost = cost
        };

    private static HoldingCost Cost(params CostLine[] lines)
        => new()
        {
            Measures =
            [
                new MeasureCost
                {
                    Lines = lines.ToList()
                }
            ]
        };

    private static CostLine Line(
        string key,
        string text,
        string unit,
        decimal qty,
        bool selected = true)
        => new()
        {
            ItemKey = key,
            Text = text,
            Unit = unit,
            Qty = qty,
            Selected = selected
        };

}
