using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungOptimizationViewModelTests
{
    [Fact]
    public async Task OptimizeCommand_maps_record_and_blocks_parallel_start_until_result_arrives()
    {
        var record = CreateRecord();
        var service = new ControlledOptimizationService();
        var rule = new RuleRecommendationDto
        {
            Measures = ["Inliner", "Roboter"],
            EstimatedCost = 12_500m
        };
        var viewModel = new SanierungOptimizationViewModel(record, service, rule);

        var execution = viewModel.OptimizeCommand.ExecuteAsync(null);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.OptimizeCommand.CanExecute(null));
        Assert.Equal("H-100", service.Request?.HaltungId);
        Assert.Equal(300, service.Request?.Pipe.DiameterMm);
        Assert.Equal("Steinzeug", service.Request?.Pipe.Material);
        Assert.Equal(42.5, service.Request?.Pipe.LengthMeter);
        Assert.Equal("Inliner, Roboter", viewModel.RuleMeasures);
        Assert.Equal("12500.00 CHF", viewModel.RuleEstimatedCost);

        service.Complete(new SanierungOptimizationResult
        {
            RecommendedMeasure = "Inliner DN 300",
            Confidence = 0.91,
            Reasoning = "Strukturell geeignet",
            CostEstimate = new CostBand { Min = 10_000m, Expected = 12_000m, Max = 14_000m },
            RiskFlags = ["Grundwasser pruefen"],
            UsedSignals = "Regel + KI"
        });
        await execution;

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.HasResult);
        Assert.False(viewModel.HasError);
        Assert.True(viewModel.OptimizeCommand.CanExecute(null));
        Assert.True(viewModel.ApplyToSecondaryCommand.CanExecute(null));
        Assert.True(viewModel.TransferToPrimaryCommand.CanExecute(null));
        Assert.Equal("Inliner DN 300", viewModel.AiMeasure);
        Assert.Equal("12000", viewModel.CostExpected);
        Assert.Equal("Grundwasser pruefen", viewModel.RiskText);
    }

    [Fact]
    public async Task OptimizeCommand_turns_service_failure_into_stable_viewmodel_state()
    {
        var service = new ThrowingOptimizationService();
        var viewModel = new SanierungOptimizationViewModel(CreateRecord(), service, null);

        await viewModel.OptimizeCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.HasError);
        Assert.False(viewModel.HasResult);
        Assert.Contains("Programmlog", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dienst nicht erreichbar", viewModel.StatusText, StringComparison.Ordinal);
        Assert.True(viewModel.OptimizeCommand.CanExecute(null));
    }

    [Fact]
    public void CancelCommand_requests_close_without_starting_ai()
    {
        var service = new ControlledOptimizationService();
        var viewModel = new SanierungOptimizationViewModel(CreateRecord(), service, null);
        var closeCount = 0;
        viewModel.CloseRequested += () => closeCount++;

        viewModel.CancelCommand.Execute(null);

        Assert.Equal(1, closeCount);
        Assert.Null(service.Request);
    }

    private static HaltungRecord CreateRecord()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H-100", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("DN_mm", "300", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Rohrmaterial", "Steinzeug", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "42.5", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Grundwasserspiegel", "oberhalb", FieldSource.Manual, userEdited: true);
        return record;
    }

    private sealed class ControlledOptimizationService : IAiSanierungOptimizationService
    {
        private readonly TaskCompletionSource<SanierungOptimizationResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SanierungOptimizationRequest? Request { get; private set; }

        public Task<SanierungOptimizationResult> OptimizeAsync(
            SanierungOptimizationRequest req,
            CancellationToken ct)
        {
            Request = req;
            Started.TrySetResult(true);
            return _completion.Task.WaitAsync(ct);
        }

        public void Complete(SanierungOptimizationResult result)
            => _completion.TrySetResult(result);
    }

    private sealed class ThrowingOptimizationService : IAiSanierungOptimizationService
    {
        public Task<SanierungOptimizationResult> OptimizeAsync(
            SanierungOptimizationRequest req,
            CancellationToken ct)
            => throw new InvalidOperationException("Dienst nicht erreichbar");
    }
}
