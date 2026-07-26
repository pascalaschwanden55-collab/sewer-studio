using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingLiveFindingEventCommandOutcome
{
    Skipped,
    Executed
}

public sealed record CodingLiveFindingEventCommandRequest(
    bool HasCodingViewModel,
    LiveDetection Result,
    IReadOnlyList<LiveFrameFinding> ValidFindings,
    ICodingSessionService? CodingSessionService,
    IEnumerable<CodingEvent> ViewEvents,
    QualityGateService? QualityGate,
    TimeSpan? CurrentVideoTime,
    TimeSpan FallbackVideoTime);

public sealed record CodingLiveFindingEventCommandActions(
    Func<double?, double?, double> ResolveMeterForFrame,
    Func<CodingLiveFindingEventWorkflowRequest, CodingLiveFindingEventWorkflowResult> ExecuteFindingWorkflow);

public sealed record CodingLiveFindingEventCommandResult(
    CodingLiveFindingEventCommandOutcome Outcome,
    CodingLiveFindingEventWorkflowResult? EventResult,
    double Meter,
    TimeSpan VideoTime);

public static class CodingLiveFindingEventCommandWorkflow
{
    public static CodingLiveFindingEventCommandResult Execute(
        CodingLiveFindingEventCommandRequest request,
        CodingLiveFindingEventCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel || request.CodingSessionService is null)
            return Result(CodingLiveFindingEventCommandOutcome.Skipped, null, 0, TimeSpan.Zero);

        var meter = actions.ResolveMeterForFrame(
            request.Result.TimestampSeconds,
            request.Result.MeterReading);
        var videoTime = request.CurrentVideoTime ?? request.FallbackVideoTime;
        var eventResult = actions.ExecuteFindingWorkflow(
            new CodingLiveFindingEventWorkflowRequest(
                request.ValidFindings,
                meter,
                videoTime,
                request.CodingSessionService,
                request.ViewEvents,
                request.QualityGate));

        return Result(CodingLiveFindingEventCommandOutcome.Executed, eventResult, meter, videoTime);
    }

    private static CodingLiveFindingEventCommandResult Result(
        CodingLiveFindingEventCommandOutcome outcome,
        CodingLiveFindingEventWorkflowResult? eventResult,
        double meter,
        TimeSpan videoTime)
        => new(outcome, eventResult, meter, videoTime);
}
