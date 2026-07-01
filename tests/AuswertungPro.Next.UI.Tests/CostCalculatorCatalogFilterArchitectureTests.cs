using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorCatalogFilterArchitectureTests
{
    [Fact]
    public void CostCalculatorViewModel_delegiert_katalogfilter_an_controller()
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
        var searchChangedSource = ExtractMethodBody(viewModelSource, "partial void OnCatalogSearchTextChanged(string value)");
        var reloadCatalogSource = ExtractMethodBody(viewModelSource, "private void ReloadCatalog()");

        Assert.Contains("CostCalculatorCatalogFilterController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("AllCatalogItems => _catalogFilter.AllCatalogItems", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("FilteredCatalogItems => _catalogFilter.FilteredCatalogItems", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_catalogFilter.ApplyFilter(value)", searchChangedSource, StringComparison.Ordinal);
        Assert.Contains("_catalogFilter.ReplaceItems(", reloadCatalogSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var keyToGroup = new Dictionary<string, string>", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilteredCatalogItems.Clear();", viewModelSource, StringComparison.Ordinal);
    }

}
