using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingLiveAiTimerTickWorkflowOutcome
{
    Skipped,
    Analyzed,
    ErrorLogged
}

public sealed record CodingLiveAiTimerTickWorkflowRequest(
    bool IsClosing,
    bool HasPlayer,
    bool HasLiveDetection,
    CodingSessionState? SessionState,
    bool IsPlayerPlaying);

public sealed record CodingLiveAiTimerTickWorkflowActions(
    Func<Task> RunAnalysisAsync,
    Action<string> TraceError);

public static class CodingLiveAiTimerTickWorkflow
{
    public static async Task<CodingLiveAiTimerTickWorkflowOutcome> ExecuteAsync(
        CodingLiveAiTimerTickWorkflowRequest request,
        CodingLiveAiTimerTickWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            if (!CodingLiveAiTickPolicy.ShouldAnalyze(
                    request.IsClosing,
                    request.HasPlayer,
                    request.HasLiveDetection,
                    request.SessionState,
                    request.IsPlayerPlaying))
            {
                return CodingLiveAiTimerTickWorkflowOutcome.Skipped;
            }

            await actions.RunAnalysisAsync();
            return CodingLiveAiTimerTickWorkflowOutcome.Analyzed;
        }
        catch (Exception ex)
        {
            actions.TraceError(ex.Message);
            return CodingLiveAiTimerTickWorkflowOutcome.ErrorLogged;
        }
    }
}
