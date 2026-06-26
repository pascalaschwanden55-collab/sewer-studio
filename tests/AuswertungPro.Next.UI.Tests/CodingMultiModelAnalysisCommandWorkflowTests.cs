using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelAnalysisCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_without_multi_model_runtime()
    {
        using var cts = new CancellationTokenSource();

        var result = await CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<object>(
                MultiModel: null,
                AnalysisCancellation: cts),
            NoActions<object>());

        Assert.Equal(CodingMultiModelAnalysisCommandOutcome.MissingMultiModel, result.Outcome);
        Assert.Null(result.StartOutcome);
    }

    [Fact]
    public async Task ExecuteAsync_skips_without_analysis_cancellation()
    {
        var result = await CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<object>(
                MultiModel: new object(),
                AnalysisCancellation: null),
            NoActions<object>());

        Assert.Equal(CodingMultiModelAnalysisCommandOutcome.MissingAnalysisCancellation, result.Outcome);
        Assert.Null(result.StartOutcome);
    }

    [Theory]
    [InlineData(CodingMultiModelAnalysisStartWorkflowOutcome.NoSnapshot)]
    [InlineData(CodingMultiModelAnalysisStartWorkflowOutcome.FrameNotReady)]
    public async Task ExecuteAsync_stops_when_start_is_not_ready(
        CodingMultiModelAnalysisStartWorkflowOutcome startOutcome)
    {
        using var cts = new CancellationTokenSource();
        var calls = new List<string>();

        var result = await CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<object>(
                MultiModel: new object(),
                AnalysisCancellation: cts),
            Actions<object>(
                calls,
                startResult: new CodingMultiModelAnalysisStartWorkflowResult(
                    startOutcome,
                    FrameBytes: [1, 2, 3],
                    FrameOsdMeter: 7.8)));

        Assert.Equal(CodingMultiModelAnalysisCommandOutcome.StartNotReady, result.Outcome);
        Assert.Equal(startOutcome, result.StartOutcome);
        Assert.Equal(["start"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_resolves_end_meter_and_runs_inference_after_ready_start()
    {
        using var cts = new CancellationTokenSource();
        var calls = new List<string>();
        var multiModel = new object();

        var result = await CodingMultiModelAnalysisCommandWorkflow.ExecuteAsync(
            new CodingMultiModelAnalysisCommandRequest<object>(
                multiModel,
                cts),
            Actions<object>(calls));

        Assert.Equal(CodingMultiModelAnalysisCommandOutcome.InferenceCompleted, result.Outcome);
        Assert.Equal(CodingMultiModelAnalysisStartWorkflowOutcome.Ready, result.StartOutcome);
        Assert.Equal(
            [
                "start",
                "end-meter",
                "inference:True:3:7.8:42.5:True"
            ],
            calls);
    }

    private static CodingMultiModelAnalysisCommandActions<TMultiModel> Actions<TMultiModel>(
        List<string> calls,
        CodingMultiModelAnalysisStartWorkflowResult? startResult = null)
        where TMultiModel : class
        => new(
            StartAnalysisAsync: _ =>
            {
                calls.Add("start");
                return Task.FromResult(startResult ?? ReadyStart());
            },
            ResolveEndMeter: () =>
            {
                calls.Add("end-meter");
                return 42.5;
            },
            RunInferenceAsync: (multiModel, start, endMeter, token) =>
            {
                calls.Add(
                    $"inference:{multiModel is not null}:{start.FrameBytes!.Length}:{start.FrameOsdMeter:F1}:{endMeter:F1}:{token.CanBeCanceled}");
                return Task.CompletedTask;
            });

    private static CodingMultiModelAnalysisCommandActions<TMultiModel> NoActions<TMultiModel>()
        where TMultiModel : class
        => new(
            StartAnalysisAsync: _ => throw new InvalidOperationException("Start should not run."),
            ResolveEndMeter: () => throw new InvalidOperationException("End meter should not be resolved."),
            RunInferenceAsync: (_, _, _, _) => throw new InvalidOperationException("Inference should not run."));

    private static CodingMultiModelAnalysisStartWorkflowResult ReadyStart()
        => new(
            CodingMultiModelAnalysisStartWorkflowOutcome.Ready,
            FrameBytes: [1, 2, 3],
            FrameOsdMeter: 7.8);
}
