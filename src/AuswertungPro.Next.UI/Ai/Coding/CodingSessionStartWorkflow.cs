namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingSessionStartWorkflowRequest(
    bool HasRequiredState,
    double EndMeter);

public sealed record CodingSessionStartWorkflowActions(
    Action ExecuteStartSession,
    Func<bool> HasActiveSession,
    Action<string> ShowSessionStartFailed,
    Action ExitCodingMode,
    Action PauseSession,
    Action<double> SetRangeText,
    Action<double> SetMeterText);

public static class CodingSessionStartWorkflow
{
    public static bool Execute(
        CodingSessionStartWorkflowRequest request,
        CodingSessionStartWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasRequiredState)
            return false;

        try
        {
            actions.ExecuteStartSession();
        }
        catch (Exception ex)
        {
            actions.ShowSessionStartFailed(ex.Message);
            actions.ExitCodingMode();
            return false;
        }

        if (!actions.HasActiveSession())
        {
            actions.ExitCodingMode();
            return false;
        }

        actions.PauseSession();
        actions.SetRangeText(request.EndMeter);
        actions.SetMeterText(0.0);
        return true;
    }
}
