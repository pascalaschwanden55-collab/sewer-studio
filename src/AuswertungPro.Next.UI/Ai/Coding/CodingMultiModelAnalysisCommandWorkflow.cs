using System.Threading;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiModelAnalysisCommandOutcome
{
    MissingMultiModel,
    MissingAnalysisCancellation,
    StartNotReady,
    InferenceCompleted
}

public sealed record CodingMultiModelAnalysisCommandRequest<TMultiModel>(
    TMultiModel? MultiModel,
    CancellationTokenSource? AnalysisCancellation)
    where TMultiModel : class;

public sealed record CodingMultiModelAnalysisCommandActions<TMultiModel>(
    Func<CancellationToken, Task<CodingMultiModelAnalysisStartWorkflowResult>> StartAnalysisAsync,
    Func<double?> ResolveEndMeter,
    Func<TMultiModel, CodingMultiModelAnalysisStartWorkflowResult, double?, CancellationToken, Task> RunInferenceAsync)
    where TMultiModel : class;

public sealed record CodingMultiModelAnalysisCommandResult(
    CodingMultiModelAnalysisCommandOutcome Outcome,
    CodingMultiModelAnalysisStartWorkflowOutcome? StartOutcome);

public static class CodingMultiModelAnalysisCommandWorkflow
{
    public static async Task<CodingMultiModelAnalysisCommandResult> ExecuteAsync<TMultiModel>(
        CodingMultiModelAnalysisCommandRequest<TMultiModel> request,
        CodingMultiModelAnalysisCommandActions<TMultiModel> actions)
        where TMultiModel : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var runtimeGate = CodingMultiModelRuntimeGateWorkflow.Execute(
            new CodingMultiModelRuntimeGateWorkflowRequest<TMultiModel>(
                request.MultiModel,
                request.AnalysisCancellation));

        if (runtimeGate.Outcome == CodingMultiModelRuntimeGateWorkflowOutcome.MissingMultiModel)
            return Result(CodingMultiModelAnalysisCommandOutcome.MissingMultiModel, startOutcome: null);

        if (runtimeGate.Outcome == CodingMultiModelRuntimeGateWorkflowOutcome.MissingAnalysisCancellation)
            return Result(CodingMultiModelAnalysisCommandOutcome.MissingAnalysisCancellation, startOutcome: null);

        var cancellationToken = runtimeGate.AnalysisCancellation!.Token;
        var start = await actions.StartAnalysisAsync(cancellationToken);
        if (start.Outcome != CodingMultiModelAnalysisStartWorkflowOutcome.Ready)
            return Result(CodingMultiModelAnalysisCommandOutcome.StartNotReady, start.Outcome);

        var endMeter = actions.ResolveEndMeter();
        await actions.RunInferenceAsync(
            runtimeGate.MultiModel!,
            start,
            endMeter,
            cancellationToken);

        return Result(CodingMultiModelAnalysisCommandOutcome.InferenceCompleted, start.Outcome);
    }

    private static CodingMultiModelAnalysisCommandResult Result(
        CodingMultiModelAnalysisCommandOutcome outcome,
        CodingMultiModelAnalysisStartWorkflowOutcome? startOutcome)
        => new(outcome, startOutcome);
}
