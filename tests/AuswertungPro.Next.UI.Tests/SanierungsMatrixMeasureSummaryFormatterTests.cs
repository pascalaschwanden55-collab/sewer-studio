using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixMeasureSummaryFormatterTests
{
    [Fact]
    public void FormatSummary_zeigt_keine_massnahme_fuer_leeren_store()
    {
        Assert.Equal("- keine -", SanierungsMatrixMeasureSummaryFormatter.FormatSummary(null));
        Assert.Equal("- keine -", SanierungsMatrixMeasureSummaryFormatter.FormatSummary(new HoldingCost()));
    }

    [Fact]
    public void FormatSummary_fasst_mehrere_massnahmen_kompakt_zusammen()
    {
        var cost = new HoldingCost
        {
            Measures =
            {
                Measure("GFK", "GFK"),
                Measure("LEM", "LEM"),
                Measure("ANSCHLUSS", "Anschluss abdichten"),
            },
        };

        Assert.Equal("GFK + LEM + 1 weitere", SanierungsMatrixMeasureSummaryFormatter.FormatSummary(cost));
    }

    [Fact]
    public void BuildDetailMeasures_liefert_nur_ausgewaehlte_positionen()
    {
        var cost = new HoldingCost
        {
            Total = 300m,
            Measures =
            {
                new MeasureCost
                {
                    MeasureId = "GFK",
                    MeasureName = "GFK",
                    Total = 300m,
                    Lines =
                    {
                        new CostLine
                        {
                            Group = "Hauptarbeit",
                            ItemKey = "GFK",
                            Text = "GFK-Liner",
                            Unit = "m",
                            Qty = 12m,
                            UnitPrice = 25m,
                            Selected = true,
                        },
                        new CostLine
                        {
                            Group = "Option",
                            ItemKey = "NICHT",
                            Text = "Nicht gewaehlt",
                            Unit = "Stk",
                            Qty = 1m,
                            UnitPrice = 99m,
                            Selected = false,
                        },
                    },
                },
            },
        };

        var detail = SanierungsMatrixMeasureSummaryFormatter.BuildDetailMeasures(cost);

        var measure = Assert.Single(detail);
        Assert.Equal("GFK", measure.MeasureName);
        Assert.Equal(300m, measure.Total);
        var line = Assert.Single(measure.Lines);
        Assert.Equal("Hauptarbeit", line.Group);
        Assert.Equal("GFK-Liner", line.Text);
        Assert.Equal(12m, line.Qty);
        Assert.Equal(25m, line.UnitPrice);
        Assert.Equal(300m, line.LineTotal);
    }

    private static MeasureCost Measure(string id, string name)
    {
        return new MeasureCost
        {
            MeasureId = id,
            MeasureName = name,
            Total = 10m,
            Lines =
            {
                new CostLine
                {
                    Group = "Hauptarbeit",
                    ItemKey = id,
                    Text = name,
                    Unit = "m",
                    Qty = 1m,
                    UnitPrice = 10m,
                    Selected = true,
                },
            },
        };
    }
}
