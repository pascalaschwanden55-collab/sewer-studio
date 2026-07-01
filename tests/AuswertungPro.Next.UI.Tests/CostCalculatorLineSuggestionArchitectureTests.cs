using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorLineSuggestionArchitectureTests
{
    [Fact]
    public void CostLineVm_delegiert_suggested_vs_manual_state_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "CostCalculatorViewModel.cs"));
        var costLineSource = source[source.IndexOf("public sealed partial class CostLineVm", StringComparison.Ordinal)..];

        Assert.Contains("private readonly CostCalculatorLineSuggestionStateController _suggestionState = new();", costLineSource, StringComparison.Ordinal);
        Assert.Contains("_suggestionState.ApplySuggestedPrice(", costLineSource, StringComparison.Ordinal);
        Assert.Contains("_suggestionState.ApplySuggestedQty(", costLineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _suppressOverride", costLineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _suppressQtyOverride", costLineSource, StringComparison.Ordinal);
    }

}
