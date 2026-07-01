using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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

}
