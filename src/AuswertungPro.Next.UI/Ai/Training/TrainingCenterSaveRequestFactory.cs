namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterSaveRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<TrainingCenterState> BuildState,
    Func<TrainingCenterState, Task> SaveStateAsync,
    Action<string> SetStatusText);

public sealed record TrainingCenterSaveDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IEnumerable<TrainingCase> Cases,
    IEnumerable<string> RootFolders,
    Func<TrainingCenterState, Task> SaveStateAsync,
    Action<string> SetStatusText);

public static class TrainingCenterSaveRequestFactory
{
    public static TrainingCenterSaveWorkflowRequest CreateWithDefaults(
        TrainingCenterSaveDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SetIsBusy);
        ArgumentNullException.ThrowIfNull(request.Cases);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.SaveStateAsync);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);

        return Create(new TrainingCenterSaveRequestFactoryRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            () => BuildStateWithDefaults(request.Cases, request.RootFolders),
            request.SaveStateAsync,
            request.SetStatusText));
    }

    public static TrainingCenterState BuildStateWithDefaults(
        IEnumerable<TrainingCase> cases,
        IEnumerable<string> rootFolders)
        => TrainingCenterStateController.BuildState(cases, rootFolders, DateTime.UtcNow);

    public static TrainingCenterSaveWorkflowRequest Create(
        TrainingCenterSaveRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SetIsBusy);
        ArgumentNullException.ThrowIfNull(request.BuildState);
        ArgumentNullException.ThrowIfNull(request.SaveStateAsync);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);

        return new TrainingCenterSaveWorkflowRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.BuildState,
            request.SaveStateAsync,
            request.SetStatusText);
    }
}
