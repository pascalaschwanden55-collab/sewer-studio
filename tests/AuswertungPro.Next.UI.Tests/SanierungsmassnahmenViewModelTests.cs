using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsmassnahmenViewModelTests
{
    [Fact]
    public void Constructor_maps_haltung_context_and_close_command_requests_close()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("DN_mm", "400", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "55.2", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Zustandsklasse", "3", FieldSource.Manual, userEdited: true);
        var calculator = new CostCalculatorViewModel(
            holding: "H-200",
            date: null,
            recommendedTokens: [],
            projectPath: null,
            catalogStore: new CostCatalogStore(),
            templateStore: new MeasureTemplateStore(),
            costRepo: new ProjectCostStoreRepository());
        var viewModel = new SanierungsmassnahmenViewModel(
            calculator,
            optimizationVm: null,
            record,
            InitialFocusMode.CostCalculator);
        var closeCount = 0;
        viewModel.CloseRequested += () => closeCount++;

        Assert.Equal("H-200", viewModel.HaltungName);
        Assert.Equal("400", viewModel.Dn);
        Assert.Equal("Beton", viewModel.Material);
        Assert.Equal("55.2", viewModel.Laenge);
        Assert.Equal("3", viewModel.Zustandsklasse);
        Assert.Contains("H-200", viewModel.WindowTitle, StringComparison.Ordinal);
        Assert.False(viewModel.HasAi);
        Assert.False(viewModel.ApplyRuleToCalcCommand.CanExecute(null));
        Assert.False(viewModel.ApplyAiToCalcCommand.CanExecute(null));

        viewModel.CloseCommand.Execute(null);

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void Rule_command_resolves_catalog_measure_and_adds_it_to_calculation()
    {
        var record = CreateRecord("H-201");
        var calculator = new CostCalculatorViewModel(
            holding: "H-201",
            date: null,
            recommendedTokens: [],
            projectPath: null,
            catalogStore: new CostCatalogStore(),
            templateStore: new MeasureTemplateStore(),
            costRepo: new ProjectCostStoreRepository());
        var expected = calculator.Measures.First(measure => !measure.Disabled);
        var optimization = new SanierungOptimizationViewModel(
            record,
            new NeverCalledOptimizationService(),
            new RuleRecommendationDto { Measures = [expected.Name] });
        var viewModel = new SanierungsmassnahmenViewModel(
            calculator,
            optimization,
            record,
            InitialFocusMode.CostCalculator);

        Assert.True(viewModel.ApplyRuleToCalcCommand.CanExecute(null));

        viewModel.ApplyRuleToCalcCommand.Execute(null);

        Assert.Contains(calculator.SelectedMeasures, measure => measure.MeasureId == expected.Id);
    }

    private static HaltungRecord CreateRecord(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        return record;
    }

    private sealed class NeverCalledOptimizationService : IAiSanierungOptimizationService
    {
        public Task<SanierungOptimizationResult> OptimizeAsync(
            SanierungOptimizationRequest req,
            CancellationToken ct)
            => throw new InvalidOperationException("Der KI-Dienst darf in diesem Test nicht starten.");
    }
}
