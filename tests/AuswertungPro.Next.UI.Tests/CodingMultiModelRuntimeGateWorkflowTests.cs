using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelRuntimeGateWorkflowTests
{
    [Fact]
    public void Execute_skips_when_multi_model_service_is_missing()
    {
        using var cts = new CancellationTokenSource();

        var result = CodingMultiModelRuntimeGateWorkflow.Execute(
            new CodingMultiModelRuntimeGateWorkflowRequest<object>(
                MultiModel: null,
                AnalysisCancellation: cts));

        Assert.Equal(CodingMultiModelRuntimeGateWorkflowOutcome.MissingMultiModel, result.Outcome);
        Assert.False(result.Ready);
        Assert.Null(result.MultiModel);
        Assert.Null(result.AnalysisCancellation);
    }

    [Fact]
    public void Execute_skips_when_analysis_cancellation_is_missing()
    {
        var multiModel = new object();

        var result = CodingMultiModelRuntimeGateWorkflow.Execute(
            new CodingMultiModelRuntimeGateWorkflowRequest<object>(
                multiModel,
                AnalysisCancellation: null));

        Assert.Equal(CodingMultiModelRuntimeGateWorkflowOutcome.MissingAnalysisCancellation, result.Outcome);
        Assert.False(result.Ready);
        Assert.Null(result.MultiModel);
        Assert.Null(result.AnalysisCancellation);
    }

    [Fact]
    public void Execute_returns_runtime_when_multi_model_and_analysis_cancellation_exist()
    {
        var multiModel = new object();
        using var cts = new CancellationTokenSource();

        var result = CodingMultiModelRuntimeGateWorkflow.Execute(
            new CodingMultiModelRuntimeGateWorkflowRequest<object>(
                multiModel,
                cts));

        Assert.Equal(CodingMultiModelRuntimeGateWorkflowOutcome.Ready, result.Outcome);
        Assert.True(result.Ready);
        Assert.Same(multiModel, result.MultiModel);
        Assert.Same(cts, result.AnalysisCancellation);
    }
}
