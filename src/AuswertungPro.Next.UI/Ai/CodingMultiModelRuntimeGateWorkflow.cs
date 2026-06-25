using System.Threading;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingMultiModelRuntimeGateWorkflowOutcome
{
    MissingMultiModel,
    MissingAnalysisCancellation,
    Ready
}

public sealed record CodingMultiModelRuntimeGateWorkflowRequest<TMultiModel>(
    TMultiModel? MultiModel,
    CancellationTokenSource? AnalysisCancellation)
    where TMultiModel : class;

public sealed record CodingMultiModelRuntimeGateWorkflowResult<TMultiModel>(
    CodingMultiModelRuntimeGateWorkflowOutcome Outcome,
    TMultiModel? MultiModel,
    CancellationTokenSource? AnalysisCancellation)
    where TMultiModel : class
{
    public bool Ready => Outcome == CodingMultiModelRuntimeGateWorkflowOutcome.Ready;
}

public static class CodingMultiModelRuntimeGateWorkflow
{
    public static CodingMultiModelRuntimeGateWorkflowResult<TMultiModel> Execute<TMultiModel>(
        CodingMultiModelRuntimeGateWorkflowRequest<TMultiModel> request)
        where TMultiModel : class
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MultiModel is null)
            return Result<TMultiModel>(CodingMultiModelRuntimeGateWorkflowOutcome.MissingMultiModel);

        if (request.AnalysisCancellation is null)
            return Result<TMultiModel>(CodingMultiModelRuntimeGateWorkflowOutcome.MissingAnalysisCancellation);

        return new CodingMultiModelRuntimeGateWorkflowResult<TMultiModel>(
            CodingMultiModelRuntimeGateWorkflowOutcome.Ready,
            request.MultiModel,
            request.AnalysisCancellation);
    }

    private static CodingMultiModelRuntimeGateWorkflowResult<TMultiModel> Result<TMultiModel>(
        CodingMultiModelRuntimeGateWorkflowOutcome outcome)
        where TMultiModel : class
        => new(outcome, MultiModel: null, AnalysisCancellation: null);
}
