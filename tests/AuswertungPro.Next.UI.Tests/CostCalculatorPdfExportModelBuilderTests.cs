using System.IO;
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

    [Fact]
    public void CostCalculatorViewModel_delegiert_pdf_modellbau_an_builder()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "CostCalculatorViewModel.cs"));
        var viewModelSource = source[
            source.IndexOf("public sealed partial class CostCalculatorViewModel", StringComparison.Ordinal)..
            source.IndexOf("public sealed partial class MeasureBlockVm", StringComparison.Ordinal)];
        var exportPdfSource = ExtractMethodBody(viewModelSource, "private async Task ExportPdfAsync(Window? owner)");

        Assert.Contains("CostCalculatorPdfExportModelBuilder.Build(", exportPdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new OfferPdfContext", exportPdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OfferPdfModelFactory.CreateCostSummary", exportPdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCostSummaryEntries", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("int? dn", exportPdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("decimal? lengthM", exportPdfSource, StringComparison.Ordinal);
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

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var braceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceIndex..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenrumpf nicht abgeschlossen: {signature}");
    }

    private sealed record CostCalculatorPdfExportModelBuildResultView(
        IReadOnlyList<CostSummaryEntry> Entries,
        OfferPdfModel Model);
}
