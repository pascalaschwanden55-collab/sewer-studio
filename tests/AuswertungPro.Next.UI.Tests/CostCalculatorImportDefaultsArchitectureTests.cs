using System;
using System.IO;

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
