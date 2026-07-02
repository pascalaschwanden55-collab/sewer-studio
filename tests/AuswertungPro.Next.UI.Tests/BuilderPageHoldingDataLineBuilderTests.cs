using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageHoldingDataLineBuilderTests
{
    [Fact]
    public void Build_sortiert_zeilen_und_mappt_druckcenter_felder()
    {
        var rows = new[]
        {
            Row("H-3", owner: "Zeta", executedBy: ""),
            Row("H-2", owner: "Beta", executedBy: "Baumeister", netCost: 120.5m),
            Row("H-1", owner: "Alpha", executedBy: "Baumeister", street: "Dorfstrasse")
        };

        var lines = BuilderPageHoldingDataLineBuilder.Build(rows);

        Assert.Collection(
            lines,
            first =>
            {
                Assert.Equal("H-1", first.Holding);
                Assert.Equal("Dorfstrasse", first.Street);
                Assert.Equal("Alpha", first.Owner);
                Assert.Equal("Baumeister", first.ExecutedBy);
            },
            second =>
            {
                Assert.Equal("H-2", second.Holding);
                Assert.Equal("120.50 CHF", second.NetText);
                Assert.Equal("Quelle", second.DetailText);
                Assert.Equal("Manschette", second.MeasuresText);
            },
            third =>
            {
                Assert.Equal("H-3", third.Holding);
                Assert.Equal("", third.ExecutedBy);
            });
    }

    private static DruckcenterRowVm Row(
        string holding,
        string owner,
        string executedBy,
        string street = "",
        decimal netCost = 0m)
        => new()
        {
            Holding = holding,
            Street = street,
            Owner = owner,
            ExecutedBy = executedBy,
            Sanieren = "Ja",
            Material = "Beton",
            Zustand = "ZK1",
            NetCost = netCost,
            CostSource = "Quelle",
            MeasuresPreview = "Manschette"
        };

}
