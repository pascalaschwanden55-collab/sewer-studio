using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingStructuralClassifierCommandOutcome
{
    Skipped,
    Executed
}

public sealed record CodingStructuralClassifierCommandRequest(
    SingleFrameResult Result,
    double CaptureTimestampSeconds,
    double? FrameOsdMeter,
    TimeSpan? CurrentVideoTime,
    TimeSpan FallbackVideoTime,
    IReadOnlyList<CodingEvent>? ViewEvents,
    ICodingSessionService? CodingSessionService,
    bool MeterFromOsd);

public sealed record CodingStructuralClassifierCommandActions(
    Func<double?, double?, double> ResolveMeterForFrame,
    Func<CodingStructuralClassifierResultWorkflowRequest, CodingStructuralClassifierResultWorkflowResult> ExecuteResultWorkflow);

public sealed record CodingStructuralClassifierCommandResult(
    CodingStructuralClassifierCommandOutcome Outcome,
    CodingStructuralClassifierResultWorkflowResult? Result,
    double Meter,
    TimeSpan VideoTime)
{
    public bool Handled => Result?.Handled == true;
}

public static class CodingStructuralClassifierCommandWorkflow
{
    public static CodingStructuralClassifierCommandResult Execute(
        CodingStructuralClassifierCommandRequest request,
        CodingStructuralClassifierCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.ViewEvents is null || request.CodingSessionService is null)
            return Result(CodingStructuralClassifierCommandOutcome.Skipped, null, 0, TimeSpan.Zero);

        var meter = actions.ResolveMeterForFrame(
            request.CaptureTimestampSeconds,
            request.FrameOsdMeter);
        var videoTime = request.CurrentVideoTime ?? request.FallbackVideoTime;
        var workflowResult = actions.ExecuteResultWorkflow(
            new CodingStructuralClassifierResultWorkflowRequest(
                request.Result,
                meter,
                videoTime,
                request.ViewEvents,
                request.CodingSessionService,
                request.MeterFromOsd));

        return Result(CodingStructuralClassifierCommandOutcome.Executed, workflowResult, meter, videoTime);
    }

    private static CodingStructuralClassifierCommandResult Result(
        CodingStructuralClassifierCommandOutcome outcome,
        CodingStructuralClassifierResultWorkflowResult? result,
        double meter,
        TimeSpan videoTime)
        => new(outcome, result, meter, videoTime);
}
