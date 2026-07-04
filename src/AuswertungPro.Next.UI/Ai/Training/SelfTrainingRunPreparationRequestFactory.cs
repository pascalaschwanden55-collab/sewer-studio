using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunPreparationRequestFactoryRequest(
    bool IsBusy,
    bool IsSelfTrainingRunning,
    IList<TrainingCase> Cases,
    IReadOnlyList<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    TrainingCase? SelectedCase,
    Action<TrainingCase> SetSelectedCase,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText);

public sealed record SelfTrainingRunPreparationDefaultRequestFactoryRequest(
    bool IsBusy,
    bool IsSelfTrainingRunning,
    IList<TrainingCase> Cases,
    IReadOnlyList<string> RootFolders,
    Func<string, Task<List<TrainingCaseInput>>> ScanInputsAsync,
    TrainingCase? SelectedCase,
    Action<TrainingCase> SetSelectedCase,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText);

public static class SelfTrainingRunPreparationRequestFactory
{
    public static SelfTrainingRunPreparationWorkflowRequest CreateWithDefaults(
        SelfTrainingRunPreparationDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(
            new SelfTrainingRunPreparationRequestFactoryRequest(
                request.IsBusy,
                request.IsSelfTrainingRunning,
                request.Cases,
                request.RootFolders,
                System.IO.Directory.Exists,
                async folder =>
                {
                    var inputs = await request.ScanInputsAsync(folder).ConfigureAwait(false);
                    return inputs.Select(TrainingCaseInputMapper.ToTrainingCase).ToList();
                },
                request.SelectedCase,
                request.SetSelectedCase,
                request.ResetCancellation,
                request.SetStatusText),
            TrainingSamplesStore.LoadAsync);
    }

    public static SelfTrainingRunPreparationWorkflowRequest CreateWithDefaults(
        SelfTrainingRunPreparationRequestFactoryRequest request)
        => Create(request, TrainingSamplesStore.LoadAsync);

    public static SelfTrainingRunPreparationWorkflowRequest Create(
        SelfTrainingRunPreparationRequestFactoryRequest request,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(LoadSamplesAsync);

        return new SelfTrainingRunPreparationWorkflowRequest(
            request.IsBusy,
            request.IsSelfTrainingRunning,
            request.Cases,
            request.RootFolders,
            request.DirectoryExists,
            request.ScanFolderAsync,
            request.SelectedCase,
            LoadSamplesAsync,
            request.SetSelectedCase,
            request.ResetCancellation,
            request.SetStatusText);
    }
}
