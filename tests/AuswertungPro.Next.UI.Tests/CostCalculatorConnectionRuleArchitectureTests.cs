using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CostCalculatorConnectionRuleArchitectureTests
{
    [Fact]
    public void BestehendeZeilen_und_engine_delegieren_anschlussregel_an_gemeinsame_policy()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "CostCalculatorMeasureBlockVm.cs"));
        var engine = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Costs",
            "MeasurePricingEngine.cs"));
        var policyPath = RepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Costs",
            "ConnectionQuantityPolicy.cs");
        var methodStart = viewModel.IndexOf("private void ApplyConnectionsToLines()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "Die UI-Anschlussmethode muss auffindbar bleiben.");
        var methodEnd = viewModel.IndexOf("private void TryInitializeConnectionsFromLines()", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart, "Der UI-Anschlussabschnitt muss klar begrenzt bleiben.");
        var existingLinesRule = viewModel[methodStart..methodEnd];
        var engineMethodStart = engine.IndexOf(
            "public static void ApplyConnectionsToLines(",
            StringComparison.Ordinal);
        Assert.True(engineMethodStart >= 0, "Die Engine-Anschlussmethode muss auffindbar bleiben.");
        var engineMethodEnd = engine.IndexOf(
            "public static decimal? TryReadConnectionsFromLines(",
            engineMethodStart,
            StringComparison.Ordinal);
        Assert.True(engineMethodEnd > engineMethodStart, "Der Engine-Anschlussabschnitt muss klar begrenzt bleiben.");
        var engineRule = engine[engineMethodStart..engineMethodEnd];

        Assert.True(
            File.Exists(policyPath),
            "Bestehende UI-Zeilen und Domain-Engine brauchen dieselbe reine Entscheidungsquelle.");
        Assert.Contains("ConnectionQuantityPolicy.Evaluate(", existingLinesRule);
        Assert.Contains("ConnectionQuantityPolicy.Evaluate(", engineRule);
        Assert.DoesNotContain("connections.Value <= 0m", existingLinesRule);
        Assert.DoesNotContain("var disable = connections <= 0m", engineRule);
    }
}
