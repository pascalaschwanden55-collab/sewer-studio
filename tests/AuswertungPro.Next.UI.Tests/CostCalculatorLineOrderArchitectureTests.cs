using System;
using System.IO;

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
