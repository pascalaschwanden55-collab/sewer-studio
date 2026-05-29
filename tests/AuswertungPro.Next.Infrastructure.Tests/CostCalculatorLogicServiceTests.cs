using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CostCalculatorLogicServiceTests
{
    [Fact]
    public void CalculateTotals_ComputesVatAndGrossTotal()
    {
        var totals = CostCalculatorLogicService.CalculateTotals(250.00m, 0.081m);

        Assert.Equal(250.00m, totals.Total);
        Assert.Equal(20.25m, totals.MwstAmount);
        Assert.Equal(270.25m, totals.TotalInclMwst);
    }

    [Fact]
    public void BuildHoldingCost_SumsMeasuresAndSetsVatFields()
    {
        var date = new DateTime(2026, 5, 29);
        var measures = new[]
        {
            new MeasureCost { MeasureId = "A", MeasureName = "A", Total = 100m },
            new MeasureCost { MeasureId = "B", MeasureName = "B", Total = 50m }
        };

        var cost = CostCalculatorLogicService.BuildHoldingCost("H-1", date, measures, 0.081m);

        Assert.Equal("H-1", cost.Holding);
        Assert.Equal(date, cost.Date);
        Assert.Equal(150.00m, cost.Total);
        Assert.Equal(0.081m, cost.MwstRate);
        Assert.Equal(12.15m, cost.MwstAmount);
        Assert.Equal(162.15m, cost.TotalInclMwst);
        Assert.Equal(new[] { "A", "B" }, cost.Measures.Select(m => m.MeasureId).ToArray());
    }

    [Fact]
    public void ResolveMeasureIds_ExactTemplateMatchWinsAndWeakMatchesFallBelowCutoff()
    {
        var templates = new List<MeasureTemplate>
        {
            Template("INLINER", "Inliner", "ITEM_A"),
            Template("KURZLINER", "Kurzliner", "ITEM_B"),
            Template("SCHWACH", "Schwach", "ITEM_C")
        };
        var catalog = Catalog(
            Item("ITEM_A", "Roboterarbeit"),
            Item("ITEM_B", "Inliner vorbereiten"),
            Item("ITEM_C", "Instandsetzung"));

        var ids = CostCalculatorLogicService.ResolveMeasureIds(["Inliner"], templates, catalog);

        Assert.Equal(new[] { "INLINER" }, ids);
    }

    [Fact]
    public void ResolveMeasureIds_UsesCatalogItemKeyNameAndAliasScores()
    {
        var templates = new List<MeasureTemplate>
        {
            Template("A", "Massnahme A", "ROOT_CUT"),
            Template("B", "Massnahme B", "BAG_REPAIR"),
            Template("C", "Massnahme C", "SEAL")
        };
        var catalog = Catalog(
            Item("ROOT_CUT", "Wurzelschneiden"),
            Item("BAG_REPAIR", "Anschluss fraesen", "Hausanschluss"),
            Item("SEAL", "Dichtung"));

        var ids = CostCalculatorLogicService.ResolveMeasureIds(["ROOT_CUT", "Hausanschluss"], templates, catalog);

        Assert.Equal(new[] { "B", "A" }, ids);
    }

    [Fact]
    public void ResolveMeasureIds_IgnoresDisabledTemplatesAndEmptyTokens()
    {
        var templates = new List<MeasureTemplate>
        {
            Template("A", "Inliner", "ITEM_A", disabled: true),
            Template("B", "Kurzliner", "ITEM_B")
        };
        var catalog = Catalog(Item("ITEM_A", "Inliner"), Item("ITEM_B", "Kurzliner"));

        var ids = CostCalculatorLogicService.ResolveMeasureIds(["", "  ", "Inliner"], templates, catalog);

        Assert.Empty(ids);
    }

    private static MeasureTemplate Template(string id, string name, string itemKey, bool disabled = false)
        => new()
        {
            Id = id,
            Name = name,
            Disabled = disabled,
            Lines = new List<MeasureLineTemplate>
            {
                new() { Group = "Hauptarbeit", ItemKey = itemKey, Enabled = true }
            }
        };

    private static CostCatalogItem Item(string key, string name, params string[] aliases)
        => new()
        {
            Key = key,
            Name = name,
            Unit = "Stk",
            Active = true,
            Aliases = aliases.ToList()
        };

    private static IReadOnlyDictionary<string, CostCatalogItem> Catalog(params CostCatalogItem[] items)
        => items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
}
