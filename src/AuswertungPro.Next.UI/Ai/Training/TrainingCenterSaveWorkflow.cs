namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterSaveWorkflowRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<TrainingCenterState> BuildState,
    Func<TrainingCenterState, Task> SaveStateAsync,
    Action<string> SetStatusText);

public static class TrainingCenterSaveWorkflow
{
    public static async Task RunAsync(TrainingCenterSaveWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GetIsBusy())
            return;

        try
        {
            request.SetIsBusy(true);
            var state = request.BuildState();
            await request.SaveStateAsync(state);
            request.SetStatusText($"Gespeichert: {state.Cases.Count} Fälle, {state.RootFolders.Count} Ordner");
        }
        finally
        {
            request.SetIsBusy(false);
        }
    }
}
