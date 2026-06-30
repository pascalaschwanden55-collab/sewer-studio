using System;
using System.IO;

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
}
