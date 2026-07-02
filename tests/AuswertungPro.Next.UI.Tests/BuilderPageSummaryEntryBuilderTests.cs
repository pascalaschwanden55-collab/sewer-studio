using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BuilderPageSummaryEntryBuilderTests
{
    [Fact]
    public void Build_uebernimmt_detailkosten_und_baut_fallbackkosten()
    {
        var detailedCost = new HoldingCost { Holding = "H-1", Total = 123m };
        var rows = new[]
        {
            Row("H-1", hasDetailedCost: true, storedCost: detailedCost, netCost: 0m),
            Row("H-2", hasDetailedCost: false, storedCost: null, netCost: 100m),
            Row("H-3", hasDetailedCost: false, storedCost: null, netCost: 0m)
        };

        var entries = BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.081m);

        Assert.Collection(
            entries,
            first =>
            {
                Assert.Equal("H-1", first.Holding);
                Assert.Equal("Gemeinde", first.Owner);
                Assert.Equal("Kanalsanierer", first.ExecutedBy);
                Assert.Same(detailedCost, first.Cost);
            },
            second =>
            {
                Assert.Equal("H-2", second.Holding);
                Assert.Equal(100m, second.Cost.Total);
                Assert.Equal(8.10m, second.Cost.MwstAmount);
                Assert.Equal("PAUSCHALE", Assert.Single(second.Cost.Measures).MeasureId);
            });
    }

    [Fact]
    public void Build_nutzt_fallback_wenn_detailflag_ohne_kostenobjekt_kommt()
    {
        var rows = new[]
        {
            Row("H-1", hasDetailedCost: true, storedCost: null, netCost: 50m)
        };

        var entry = Assert.Single(BuilderPageSummaryEntryBuilder.Build(rows, vatRate: 0.01m));

        Assert.Equal("H-1", entry.Holding);
        Assert.Equal(50m, entry.Cost.Total);
        Assert.Equal(0.50m, entry.Cost.MwstAmount);
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
