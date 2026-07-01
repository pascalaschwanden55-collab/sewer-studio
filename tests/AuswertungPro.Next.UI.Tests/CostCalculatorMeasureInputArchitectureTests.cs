using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorMeasureInputArchitectureTests
{
    [Fact]
    public void MeasureBlockVm_delegiert_text_input_suppression_an_controller()
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

        Assert.Contains("private readonly CostCalculatorMeasureInputStateController _inputState = new();", measureBlockSource, StringComparison.Ordinal);
        Assert.Contains("_inputState.ApplyDnText(", measureBlockSource, StringComparison.Ordinal);
        Assert.Contains("_inputState.ApplyLengthText(", measureBlockSource, StringComparison.Ordinal);
        Assert.Contains("_inputState.ApplyConnectionsText(", measureBlockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_suppressDnUpdate", measureBlockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_suppressLengthUpdate", measureBlockSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_suppressConnectionsUpdate", measureBlockSource, StringComparison.Ordinal);
    }

}
