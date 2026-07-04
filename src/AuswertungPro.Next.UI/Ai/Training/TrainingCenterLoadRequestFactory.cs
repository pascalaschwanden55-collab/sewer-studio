namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterLoadRequestFactoryRequest(
    Func<Task<TrainingCenterState>> LoadStateAsync,
    IList<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action UpdateRootFolderDisplay,
    Action<string> SetStatusText,
    Func<Task> LoadSamplesAsync,
    Func<Task> RefreshKbStatusAsync,
    Func<Task> LoadLastMatchRateAsync);

public sealed record TrainingCenterLoadDefaultRequestFactoryRequest(
    Func<Task<TrainingCenterState>> LoadStateAsync,
    IList<string> RootFolders,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action UpdateRootFolderDisplay,
    Action<string> SetStatusText,
    Func<Task> LoadSamplesAsync,
    Func<Task> RefreshKbStatusAsync,
    Func<Task> LoadLastMatchRateAsync);

public static class TrainingCenterLoadRequestFactory
{
    public static TrainingCenterLoadWorkflowRequest CreateWithDefaults(
        TrainingCenterLoadDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(new TrainingCenterLoadRequestFactoryRequest(
            request.LoadStateAsync,
            request.RootFolders,
            System.IO.Directory.Exists,
            request.ReplaceCases,
            request.UpdateRootFolderDisplay,
            request.SetStatusText,
            request.LoadSamplesAsync,
            request.RefreshKbStatusAsync,
            request.LoadLastMatchRateAsync));
    }

    public static TrainingCenterLoadWorkflowRequest Create(
        TrainingCenterLoadRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoadStateAsync);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.DirectoryExists);
        ArgumentNullException.ThrowIfNull(request.ReplaceCases);
        ArgumentNullException.ThrowIfNull(request.UpdateRootFolderDisplay);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.LoadSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.RefreshKbStatusAsync);
        ArgumentNullException.ThrowIfNull(request.LoadLastMatchRateAsync);

        return new TrainingCenterLoadWorkflowRequest(
            request.LoadStateAsync,
            request.RootFolders,
            request.DirectoryExists,
            request.ReplaceCases,
            request.UpdateRootFolderDisplay,
            request.SetStatusText,
            request.LoadSamplesAsync,
            request.RefreshKbStatusAsync,
            request.LoadLastMatchRateAsync);
    }
}
