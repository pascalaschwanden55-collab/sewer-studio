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

        // KEIN ConfigureAwait(false): dieser Workflow orchestriert UI-Callbacks
        // (ObservableCollection-Ersatz, RootFolder-Mutation, Statuszeile) und muss
        // deshalb auf dem Aufrufer-Kontext (UI-Thread) bleiben. Sonst wirft die
        // CollectionView der Faelle-Liste (APP-A5BD1B09, Regressionstest:
        // TrainingCenterLoadWorkflowThreadingTests).
        var state = await request.LoadStateAsync();
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

        await request.LoadSamplesAsync();
        await request.RefreshKbStatusAsync();
        await request.LoadLastMatchRateAsync();
    }
}
