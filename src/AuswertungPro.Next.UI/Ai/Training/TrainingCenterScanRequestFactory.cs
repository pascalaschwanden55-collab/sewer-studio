using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterScanRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, ICollection<string>, CancellationToken, Task<List<TrainingCaseInput>>> ScanInputsAsync,
    Func<TrainingCaseInput, TrainingCase> ToTrainingCase,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action<IReadOnlyList<TrainingCase>> AppendCases,
    Action<string> SetStatusText,
    Func<Task> SaveStateAsync,
    Func<CancellationToken> ResetCancellation,
    Action<string> Log);

public sealed record TrainingCenterScanDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<string, ICollection<string>, CancellationToken, Task<List<TrainingCaseInput>>> ScanInputsAsync,
    Action<IReadOnlyList<TrainingCase>> ReplaceCases,
    Action<IReadOnlyList<TrainingCase>> AppendCases,
    Action<string> SetStatusText,
    Func<Task> SaveStateAsync,
    Func<CancellationToken> ResetCancellation,
    Action<string> Log);

public static class TrainingCenterScanRequestFactory
{
    public static TrainingCenterScanWorkflowRequest CreateWithDefaults(
        TrainingCenterScanDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(new TrainingCenterScanRequestFactoryRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.RootFolders,
            System.IO.Directory.Exists,
            request.ScanInputsAsync,
            TrainingCaseInputMapper.ToTrainingCase,
            request.ReplaceCases,
            request.AppendCases,
            request.SetStatusText,
            request.SaveStateAsync,
            request.ResetCancellation,
            request.Log));
    }

    public static TrainingCenterScanWorkflowRequest Create(
        TrainingCenterScanRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.SetIsBusy);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.DirectoryExists);
        ArgumentNullException.ThrowIfNull(request.ScanInputsAsync);
        ArgumentNullException.ThrowIfNull(request.ToTrainingCase);
        ArgumentNullException.ThrowIfNull(request.ReplaceCases);
        ArgumentNullException.ThrowIfNull(request.AppendCases);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.SaveStateAsync);
        ArgumentNullException.ThrowIfNull(request.ResetCancellation);
        ArgumentNullException.ThrowIfNull(request.Log);

        return new TrainingCenterScanWorkflowRequest(
            request.GetIsBusy,
            request.SetIsBusy,
            request.RootFolders,
            request.DirectoryExists,
            async (folder, uebersprungeneOrdner, cancellationToken) =>
            {
                var inputs = await request.ScanInputsAsync(folder, uebersprungeneOrdner, cancellationToken)
                    .ConfigureAwait(false);
                return inputs.Select(request.ToTrainingCase).ToList();
            },
            request.ReplaceCases,
            request.AppendCases,
            request.SetStatusText,
            request.SaveStateAsync,
            request.ResetCancellation,
            request.Log);
    }
}
