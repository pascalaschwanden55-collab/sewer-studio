using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Output.Offers;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorPdfExportModelBuilderTests
{
    [Fact]
    public void Build_erstellt_kostenzusammenstellung_mit_objektdaten_und_eigentuemer()
    {
        var selectedMeasure = new MeasureBlockVm(null, new Dictionary<string, CostCatalogItem>())
        {
            DnText = "300",
            LengthText = "45.30"
        };
        var cost = Cost("06.1-2", selected: true);
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["06.1-2"] = " Gemeinde "
        };

        var result = InvokeBuild(
            "06.1-2",
            new DateTime(2026, 2, 8),
            cost,
            new[] { selectedMeasure },
            owners,
            DateTimeOffset.Parse("2026-02-08T10:00:00Z"));

        Assert.NotNull(result);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("Gemeinde", entry.Owner);

        Assert.Equal("Kostenzusammenstellung", result.Model.DocumentKindLabel);
        Assert.Equal("Abwasser Uri - Kostenzusammenstellung", result.Model.ProjectTitle);
        Assert.Equal("Auswertung (1 Haltung(en))", result.Model.VariantTitle);
        Assert.Equal("Eigentuemer: Alle", result.Model.FilterSummaryText);
        Assert.Equal("100.00 CHF", result.Model.Totals.NetText);
        Assert.Equal("Haltung: 06.1-2\nDN: 300 mm\nLaenge: 45.30 m\nInspektionsdatum: 08.02.2026", result.Model.ObjectBlock);
        Assert.Equal(3, result.Model.TextBlocks.Count);
    }

    [Fact]
    public void Build_liefert_null_ohne_selektierte_kostenlinien()
    {
        var result = InvokeBuild(
            "06.1-2",
            new DateTime(2026, 2, 8),
            Cost("06.1-2", selected: false),
            new[] { new MeasureBlockVm(null, new Dictionary<string, CostCatalogItem>()) },
            new Dictionary<string, string>(),
            DateTimeOffset.Parse("2026-02-08T10:00:00Z"));

        Assert.Null(result);
    }

    private static CostCalculatorPdfExportModelBuildResultView? InvokeBuild(
        string holding,
        DateTime? date,
        HoldingCost cost,
        IReadOnlyList<MeasureBlockVm> selectedMeasures,
        IReadOnlyDictionary<string, string> owners,
        DateTimeOffset now)
    {
        var builderType = typeof(CostCalculatorViewModel).Assembly.GetType(
            "AuswertungPro.Next.UI.ViewModels.Windows.CostCalculatorPdfExportModelBuilder");
        Assert.NotNull(builderType);

        var buildMethod = builderType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(buildMethod);

        var rawResult = buildMethod.Invoke(null, new object?[] { holding, date, cost, selectedMeasures, owners, now });
        if (rawResult is null)
            return null;

        var resultType = rawResult.GetType();
        var entries = (IReadOnlyList<CostSummaryEntry>)resultType.GetProperty("Entries")!.GetValue(rawResult)!;
        var model = (OfferPdfModel)resultType.GetProperty("Model")!.GetValue(rawResult)!;
        return new CostCalculatorPdfExportModelBuildResultView(entries, model);
    }

    private static HoldingCost Cost(string holding, bool selected)
        => new()
        {
            Holding = holding,
            Total = selected ? 100m : 0m,
            MwstRate = 0.081m,
            MwstAmount = selected ? 8.10m : 0m,
            TotalInclMwst = selected ? 108.10m : 0m,
            Measures = new List<MeasureCost>
            {
                new()
                {
                    MeasureId = "M1",
                    MeasureName = "Kurzliner",
                    Lines = new List<CostLine>
                    {
                        new()
                        {
                            Group = "Hauptarbeit",
                            ItemKey = "KURZLINER",
                            Text = "Kurzliner",
                            Unit = "m",
                            Qty = 2m,
                            UnitPrice = 50m,
                            Selected = selected
                        }
                    }
                }
            }
        };

    private sealed record CostCalculatorPdfExportModelBuildResultView(
        IReadOnlyList<CostSummaryEntry> Entries,
        OfferPdfModel Model);
}
