using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingCenterSampleGenerationRequestFactoryRequest(
    TrainingCase? SelectedCase,
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<CancellationToken> ResetCancellation,
    Func<IDisposable> BeginActivity,
    ICodeCatalogProvider? CodeCatalog,
    Action<IReadOnlyList<TrainingSample>> AppendSamples,
    Action<string> SetStatusText);

public sealed record TrainingCenterSampleGenerationDefaultRequestFactoryRequest(
    TrainingCase? SelectedCase,
    Func<bool> GetIsBusy,
    Action<bool> SetIsBusy,
    Func<CancellationToken> ResetCancellation,
    ICodeCatalogProvider? CodeCatalog,
    Action<IReadOnlyList<TrainingSample>> AppendSamples,
    Action<string> SetStatusText);

public static class TrainingCenterSampleGenerationRequestFactory
{
    public static TrainingCenterSampleGenerationWorkflowRequest CreateWithDefaults(
        TrainingCenterSampleGenerationDefaultRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CreateWithDefaults(new TrainingCenterSampleGenerationRequestFactoryRequest(
            request.SelectedCase,
            request.GetIsBusy,
            request.SetIsBusy,
            request.ResetCancellation,
            BeginActivity: () => AiTrack.Begin("Training Center"),
            request.CodeCatalog,
            request.AppendSamples,
            request.SetStatusText));
    }

    public static TrainingCenterSampleGenerationWorkflowRequest CreateWithDefaults(
        TrainingCenterSampleGenerationRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Create(
            request,
            TrainingSamplesStore.LoadAsync,
            (input, signatures, token) => TrainingCenterSampleGenerationRuntime.GenerateWithDiagnosticsAsync(
                request.CodeCatalog,
                input,
                signatures,
                token),
            TrainingSamplesStore.MergeAndSaveAsync);
    }

    public static TrainingCenterSampleGenerationWorkflowRequest Create(
        TrainingCenterSampleGenerationRequestFactoryRequest request,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync,
        Func<TrainingCaseInput, IReadOnlyCollection<string>, CancellationToken, Task<TrainingSampleGenerationResult>> GenerateWithDiagnosticsAsync,
        Func<List<TrainingSample>, Task> SaveSamplesAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(LoadSamplesAsync);
        ArgumentNullException.ThrowIfNull(GenerateWithDiagnosticsAsync);
        ArgumentNullException.ThrowIfNull(SaveSamplesAsync);

        return new TrainingCenterSampleGenerationWorkflowRequest(
            request.SelectedCase,
            request.GetIsBusy,
            request.SetIsBusy,
            request.ResetCancellation,
            request.BeginActivity,
            LoadSamplesAsync,
            GenerateWithDiagnosticsAsync,
            SaveSamplesAsync,
            request.AppendSamples,
            request.SetStatusText);
    }
}
