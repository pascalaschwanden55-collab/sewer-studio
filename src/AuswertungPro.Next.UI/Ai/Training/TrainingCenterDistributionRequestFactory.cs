using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterDistributionRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<string?> SelectPdfPath,
    Func<string?> SelectVideoFolder,
    Func<string, string, string, Task<TrainingCenterImportService.DistributeResult>> DistributeAsync,
    IList<string> RootFolders,
    Action UpdateRootFolderDisplay,
    Action<string> SetLogText,
    Action<string> SetStatusText,
    Action<string> Log);

public sealed record TrainingCenterDistributionDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<string, string, string, Task<TrainingCenterImportService.DistributeResult>> DistributeAsync,
    IList<string> RootFolders,
    Action UpdateRootFolderDisplay,
    Action<string> SetLogText,
    Action<string> SetStatusText,
    Action<string> Log);

public static class TrainingCenterDistributionRequestFactory
{
    public static TrainingCenterDistributionWorkflowRequest Create(
        TrainingCenterDistributionRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SelectPdfPath);
        ArgumentNullException.ThrowIfNull(request.SelectVideoFolder);
        ArgumentNullException.ThrowIfNull(request.DistributeAsync);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.UpdateRootFolderDisplay);
        ArgumentNullException.ThrowIfNull(request.SetLogText);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.Log);

        return new TrainingCenterDistributionWorkflowRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.SelectPdfPath,
            request.SelectVideoFolder,
            request.DistributeAsync,
            request.RootFolders,
            request.UpdateRootFolderDisplay,
            request.SetLogText,
            request.SetStatusText,
            request.Log);
    }

    public static TrainingCenterDistributionWorkflowRequest CreateWithDefaultSelectors(
        TrainingCenterDistributionDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(new TrainingCenterDistributionRequestFactoryRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            TrainingCenterDistributionDialogSelector.SelectPdfPath,
            TrainingCenterDistributionDialogSelector.SelectVideoFolder,
            request.DistributeAsync,
            request.RootFolders,
            request.UpdateRootFolderDisplay,
            request.SetLogText,
            request.SetStatusText,
            request.Log));
    }
}
