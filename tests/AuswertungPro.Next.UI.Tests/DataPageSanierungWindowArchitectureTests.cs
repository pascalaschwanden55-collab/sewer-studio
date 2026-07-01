using System;
using System.IO;

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

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method marker not found: {marker}");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method has no body: {marker}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method body is incomplete: {marker}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
