namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingOverlayInputInteractionWorkflowActions(
    Action Suspend,
    Action Resume);

public static class CodingOverlayInputInteractionWorkflow
{
    public static T Run<T>(
        CodingOverlayInputInteractionWorkflowActions actions,
        Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(callback);

        actions.Suspend();
        try
        {
            return callback();
        }
        finally
        {
            actions.Resume();
        }
    }

    public static async Task RunAsync(
        CodingOverlayInputInteractionWorkflowActions actions,
        Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(callback);

        actions.Suspend();
        try
        {
            await callback();
        }
        finally
        {
            actions.Resume();
        }
    }
}
