using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureSelectionArchitectureTests
{
    [Fact]
    public void CostCalculatorViewModel_delegiert_measure_selection_und_ordering_an_controller()
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
        var sortMeasuresSource = ExtractMethodBody(viewModelSource, "private void SortMeasures()");

        Assert.Contains("CostCalculatorMeasureSelectionController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains(".SetSelectedMeasures(measures)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains(".OrderMeasures(SelectedMeasures)", sortMeasuresSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_measureOrderById", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectedMeasureIds", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".OrderBy(x => x.Order)", sortMeasuresSource, StringComparison.Ordinal);
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
