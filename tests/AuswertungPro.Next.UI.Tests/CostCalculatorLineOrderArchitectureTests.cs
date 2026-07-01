using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorLineOrderArchitectureTests
{
    [Fact]
    public void MeasureBlockVm_delegiert_line_ordering_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "CostCalculatorViewModel.cs"));
        var measureBlockSource = source[
            source.IndexOf("public sealed partial class MeasureBlockVm", StringComparison.Ordinal)..
            source.IndexOf("public sealed partial class CostLineVm", StringComparison.Ordinal)];
        var sortLinesSource = ExtractMethodBody(measureBlockSource, "public void SortLines()");

        Assert.Contains("CostCalculatorLineOrderController.OrderTemplateLines(", measureBlockSource, StringComparison.Ordinal);
        Assert.Contains("CostCalculatorLineOrderController.OrderLines(", sortLinesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static readonly string[] GroupOrder", measureBlockSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".OrderBy(x => x.GroupOrder)", sortLinesSource, StringComparison.Ordinal);
    }

}
