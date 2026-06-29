using System.IO;
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

    [Fact]
    public void BuilderPageViewModel_enthaelt_keinen_holding_data_line_builder_mehr()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "BuilderPageViewModel.cs"));

        Assert.DoesNotContain("private List<OfferPdfHoldingDataLineModel> BuildHoldingDataLines", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OfferPdfHoldingDataLineModel", source, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "AuswertungPro.Next.UI")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
