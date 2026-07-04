namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterLoadWorkflowRequest(
    Func<Task<TrainingCenterState>> LoadStateAsync,
    IList<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action UpdateRootFolderDisplay,
    Action<string> SetStatusText,
    Func<Task> LoadSamplesAsync,
    Func<Task> RefreshKbStatusAsync,
    Func<Task> LoadLastMatchRateAsync);

public static class TrainingCenterLoadWorkflow
{
    public static async Task RunAsync(TrainingCenterLoadWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = await request.LoadStateAsync().ConfigureAwait(false);
        request.ReplaceCases(state.Cases);

        var restoredRootFolders = TrainingCenterStateController.RestoreExistingRootFolders(
            state,
            request.DirectoryExists);
        if (restoredRootFolders.Count > 0)
        {
            TrainingCenterStateController.ReplaceRootFolders(request.RootFolders, restoredRootFolders);
            request.UpdateRootFolderDisplay();
        }

        request.SetStatusText($"Geladen: {state.Cases.Count} Fälle");

        await request.LoadSamplesAsync().ConfigureAwait(false);
        await request.RefreshKbStatusAsync().ConfigureAwait(false);
        await request.LoadLastMatchRateAsync().ConfigureAwait(false);
    }
}
