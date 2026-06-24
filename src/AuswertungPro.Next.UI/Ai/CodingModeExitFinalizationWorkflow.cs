using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingModeExitFinalizationWorkflowRequest(
    IReadOnlyCollection<CodingEvent>? Events,
    double? LastOsdMeter,
    double EndMeter,
    TimeSpan EndTime,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingModeExitFinalizationWorkflowActions(
    Action<double> CloseTrackedStreckenschaeden,
    Func<double, bool> CloseOpenStreckenschaeden,
    Action<double, TimeSpan, byte[]?> EnsureRohrendeExists);

public sealed record CodingModeExitFinalizationWorkflowResult(
    bool CanExit);

public static class CodingModeExitFinalizationWorkflow
{
    public static CodingModeExitFinalizationWorkflowResult Execute(
        CodingModeExitFinalizationWorkflowRequest request,
        CodingModeExitFinalizationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.Events is not { Count: > 0 })
            return new CodingModeExitFinalizationWorkflowResult(CanExit: true);

        var closeMeter = request.LastOsdMeter ?? request.EndMeter;
        actions.CloseTrackedStreckenschaeden(closeMeter);
        if (!actions.CloseOpenStreckenschaeden(closeMeter))
            return new CodingModeExitFinalizationWorkflowResult(CanExit: false);

        if (!CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode(request.Events))
        {
            actions.EnsureRohrendeExists(
                request.EndMeter,
                request.EndTime,
                request.AnalyzedFrameBytes);
        }

        return new CodingModeExitFinalizationWorkflowResult(CanExit: true);
    }
}
