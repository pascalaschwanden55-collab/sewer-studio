using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingBoundaryClassifierCommandOutcome
{
    Skipped,
    Executed
}

public sealed record CodingBoundaryClassifierCommandRequest(
    SingleFrameResult Result,
    bool HasCodingViewModel,
    bool HasCodingSessionService,
    double CaptureTimestampSeconds,
    double? FrameOsdMeter,
    TimeSpan? CurrentVideoTime,
    TimeSpan FallbackVideoTime,
    double EndMeter,
    int ExistingEventCount,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryClassifierCommandActions(
    Func<double?, double?, double> ResolveMeterForFrame,
    Func<CodingBoundaryClassifierResultWorkflowRequest, CodingBoundaryClassifierResultWorkflowResult> ExecuteResultWorkflow);

public sealed record CodingBoundaryClassifierCommandResult(
    CodingBoundaryClassifierCommandOutcome Outcome,
    CodingBoundaryClassifierResultWorkflowResult? Result,
    double Meter,
    TimeSpan VideoTime)
{
    public bool Handled => Result?.Handled == true;
}

public static class CodingBoundaryClassifierCommandWorkflow
{
    public static CodingBoundaryClassifierCommandResult Execute(
        CodingBoundaryClassifierCommandRequest request,
        CodingBoundaryClassifierCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        if (!CodingBoundaryClassifierResultWorkflow.CanHandle(request.Result)
            || !request.HasCodingViewModel
            || !request.HasCodingSessionService)
        {
            return Build(CodingBoundaryClassifierCommandOutcome.Skipped, null, 0, TimeSpan.Zero);
        }

        var meter = actions.ResolveMeterForFrame(
            request.CaptureTimestampSeconds,
            request.FrameOsdMeter);
        var videoTime = request.CurrentVideoTime ?? request.FallbackVideoTime;
        var result = actions.ExecuteResultWorkflow(
            new CodingBoundaryClassifierResultWorkflowRequest(
                request.Result,
                meter,
                request.EndMeter,
                videoTime,
                request.ExistingEventCount,
                request.AnalyzedFrameBytes));

        return Build(CodingBoundaryClassifierCommandOutcome.Executed, result, meter, videoTime);
    }

    private static CodingBoundaryClassifierCommandResult Build(
        CodingBoundaryClassifierCommandOutcome outcome,
        CodingBoundaryClassifierResultWorkflowResult? result,
        double meter,
        TimeSpan videoTime)
        => new(outcome, result, meter, videoTime);
}
