using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorImportDefaultsArchitectureTests
{
    [Fact]
    public void CostCalculatorViewModel_delegiert_import_defaults_an_controller()
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
        var tryAddMeasureSource = ExtractMethodBody(viewModelSource, "private bool TryAddMeasure(string id, bool applyPrices)");

        Assert.Contains("CostCalculatorImportDefaultsController", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_importDefaults.InitializeFromHaltungRecord(haltungRecord, SelectedMeasures)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("_importDefaults.ApplyTo(block)", tryAddMeasureSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultDn", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultLength", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DefaultConnections", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MeasureImportDefaultsResolver.Resolve", viewModelSource, StringComparison.Ordinal);
    }

}
