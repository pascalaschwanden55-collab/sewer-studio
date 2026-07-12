using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageLvPreparationServiceTests
{
    [Fact]
    public void SelectAwuHoldings_schliesst_Private_aus_und_erkennt_Pauschalen()
    {
        var rows = new[]
        {
            Row("H-AWU", "AWU", DetailedCost("H-AWU", 2m)),
            Row("H-PRIVAT", "Privat", DetailedCost("H-PRIVAT", 9m)),
            new DruckcenterRowVm
            {
                Holding = "H-PAUSCHALE",
                Owner = "Abwasser Uri",
                NetCost = 50m,
                HasDetailedCost = false
            }
        };

        var selection = BuilderPageLvPreparationService.SelectAwuHoldings(rows, 0.081m);

        Assert.Equal(["H-AWU", "H-PAUSCHALE"], selection.Holdings.Select(cost => cost.Holding));
        Assert.Equal("H-PAUSCHALE", Assert.Single(selection.FallbackHoldings).Holding);
    }

    [Fact]
    public void Build_buendelt_Awu_Haltungen_und_Awu_Schaechte_und_weist_Pauschalen_separat_aus()
    {
        var selection = BuilderPageLvPreparationService.SelectAwuHoldings(
            [
                Row("H-AWU", "AWU", DetailedCost("H-AWU", 2m)),
                new DruckcenterRowVm
                {
                    Holding = "H-PAUSCHALE",
                    Owner = "AWU",
                    NetCost = 50m,
                    HasDetailedCost = false
                }
            ],
            0.081m);
        var shaftCosts = new[]
        {
            DetailedCost("KS 1", 3m),
            DetailedCost("KS 2", 8m)
        };
        var catalog = new Dictionary<string, CostCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["POSITION"] = new()
            {
                Key = "POSITION",
                Name = "Position",
                Unit = "m",
                Type = "Fixed",
                NpkCode = "500.100",
                Chapter = "500"
            }
        };

        var result = BuilderPageLvPreparationService.Build(
            selection,
            includeFallbackHoldings: false,
            shaftCosts,
            new HashSet<string>(StringComparer.Ordinal) { "KS1" },
            catalog);

        var position = Assert.Single(result.Positions);
        Assert.Equal("POSITION", position.ItemKey);
        Assert.Equal(5m, position.TotalQty);
        Assert.Equal(2, position.HoldingCount);
        Assert.Equal(50m, result.ExcludedTotal);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Equal(2, result.HoldingCount);
    }

    [Fact]
    public void Build_nimmt_Pauschale_nur_auf_wenn_ausdruecklich_gewuenscht()
    {
        var selection = BuilderPageLvPreparationService.SelectAwuHoldings(
            [new DruckcenterRowVm { Holding = "H-1", Owner = "AWU", NetCost = 40m }],
            0.081m);

        var result = BuilderPageLvPreparationService.Build(
            selection,
            includeFallbackHoldings: true,
            Array.Empty<HoldingCost>(),
            new HashSet<string>(),
            new Dictionary<string, CostCatalogItem>());

        Assert.Equal(0m, result.ExcludedTotal);
        Assert.Equal(0, result.ExcludedCount);
        Assert.Equal(1, result.HoldingCount);
        Assert.Equal("PAUSCHALE", Assert.Single(result.Positions).ItemKey);
    }

    private static DruckcenterRowVm Row(string holding, string owner, HoldingCost cost)
        => new()
        {
            Holding = holding,
            Owner = owner,
            HasDetailedCost = true,
            StoredCost = cost,
            NetCost = cost.Total
        };

    private static HoldingCost DetailedCost(string holding, decimal quantity)
        => new()
        {
            Holding = holding,
            Total = quantity * 10m,
            Measures =
            [
                new MeasureCost
                {
                    MeasureId = "M",
                    Lines =
                    [
                        new CostLine
                        {
                            ItemKey = "POSITION",
                            Text = "Position",
                            Unit = "m",
                            Qty = quantity,
                            UnitPrice = 10m,
                            Selected = true
                        }
                    ]
                }
            ]
        };
}
