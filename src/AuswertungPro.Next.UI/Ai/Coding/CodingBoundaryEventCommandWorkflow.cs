using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingBoundaryEventCommandOutcome
{
    Skipped,
    Executed
}

public sealed record CodingBoundaryStartCommandRequest(
    double CurrentMeter,
    bool HasCodingViewModel,
    IReadOnlyList<CodingEvent>? ViewEvents,
    IReadOnlyList<CodingEvent> SessionEvents,
    IReadOnlyList<CodingEvent> ImportEvents,
    ICodingSessionService? CodingSessionService,
    double? FirstCleanFrameSeconds,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryEndCommandRequest(
    bool HasCodingViewModel,
    IReadOnlyList<CodingEvent>? ViewEvents,
    IReadOnlyList<CodingEvent> ImportEvents,
    ICodingSessionService? CodingSessionService,
    double? OsdMeter,
    double FallbackEndMeter,
    double ViewModelEndMeter,
    TimeSpan FallbackVideoTime,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryStartCommandActions(
    Func<CodingBoundaryStartEventWorkflowRequest, Task<CodingBoundaryEventWorkflowResult>> EnsureStartAsync);

public sealed record CodingBoundaryEndCommandActions(
    Func<CodingBoundaryEndEventWorkflowRequest, CodingBoundaryEventWorkflowResult> EnsureEnd);

public sealed record CodingBoundaryEventCommandResult(
    CodingBoundaryEventCommandOutcome Outcome,
    CodingBoundaryEventWorkflowResult? Result)
{
    public bool Added => Result?.Added == true;
}

public static class CodingBoundaryEventCommandWorkflow
{
    public static async Task<CodingBoundaryEventCommandResult> EnsureStartAsync(
        CodingBoundaryStartCommandRequest request,
        CodingBoundaryStartCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel
            || request.CodingSessionService == null
            || request.ViewEvents == null)
        {
            return Result(CodingBoundaryEventCommandOutcome.Skipped, null);
        }

        var workflowResult = await actions.EnsureStartAsync(
            new CodingBoundaryStartEventWorkflowRequest(
                request.CurrentMeter,
                request.ViewEvents,
                request.SessionEvents,
                request.ImportEvents,
                request.CodingSessionService,
                request.FirstCleanFrameSeconds,
                request.AnalyzedFrameBytes));

        return Result(CodingBoundaryEventCommandOutcome.Executed, workflowResult);
    }

    public static CodingBoundaryEventCommandResult EnsureEnd(
        CodingBoundaryEndCommandRequest request,
        CodingBoundaryEndCommandActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel
            || request.CodingSessionService == null
            || request.ViewEvents == null)
        {
            return Result(CodingBoundaryEventCommandOutcome.Skipped, null);
        }

        var workflowResult = actions.EnsureEnd(
            new CodingBoundaryEndEventWorkflowRequest(
                request.ViewEvents,
                request.ImportEvents,
                request.CodingSessionService,
                request.OsdMeter,
                request.FallbackEndMeter,
                request.ViewModelEndMeter,
                request.FallbackVideoTime,
                request.AnalyzedFrameBytes));

        return Result(CodingBoundaryEventCommandOutcome.Executed, workflowResult);
    }

    private static CodingBoundaryEventCommandResult Result(
        CodingBoundaryEventCommandOutcome outcome,
        CodingBoundaryEventWorkflowResult? result)
        => new(outcome, result);
}
