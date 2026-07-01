using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageSanierungWindowArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_sanierungsmassnahmen_window_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("private readonly DataPageSanierungWindowController _sanierungWindowController;", source, StringComparison.Ordinal);
        AssertDelegates(source, "private void OpenSanierungsmassnahmenWindow(HaltungRecord? record, InitialFocusMode focus)", "_sanierungWindowController.Open(record, focus);");

        var method = ExtractMethod(source, "private void OpenSanierungsmassnahmenWindow(HaltungRecord? record, InitialFocusMode focus)");
        Assert.DoesNotContain("new CostCalculatorViewModel", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new SanierungOptimizationViewModel", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new SanierungsmassnahmenWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("AppSettingsAiSettingsProvider", method, StringComparison.Ordinal);
        Assert.DoesNotContain("RuleRecommendationDto", method, StringComparison.Ordinal);
        Assert.DoesNotContain("TransferredToPrimary", method, StringComparison.Ordinal);
    }

    private static void AssertDelegates(string source, string marker, string call)
    {
        var method = ExtractMethod(source, marker);
        Assert.Contains(call, method, StringComparison.Ordinal);
    }

}
