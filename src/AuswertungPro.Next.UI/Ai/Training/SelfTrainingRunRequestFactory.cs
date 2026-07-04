using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Services;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunRequestFactoryRequest(
    TrainingCase SelectedCase,
    Action<bool> SetBusy,
    Action<bool> SetSelfTrainingRunning,
    Action<string> SetLogText,
    Action<string> SetStatusText,
    Action<string> Log,
    Func<HttpClient?> GetKbHttpClient,
    Action<HttpClient> SetKbHttpClient,
    AppSettings? AppSettings,
    ICodeCatalogProvider? CodeCatalog,
    Action<string> SetActiveVisionModel,
    Action<ISelfTrainingOrchestrator?> SetOrchestrator,
    Action<SelfTrainingStep> OnProgress,
    Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexSamplesAsync,
    InfraSelfImproving.ReviewQueueService? ReviewQueueService,
    Action<InfraSelfImproving.ReviewQueueService> ReloadReviewQueue,
    Func<Task> LoadSamplesInternalAsync,
    Func<Task> RefreshKbStatusAsync,
    Action ResetVisuals,
    CancellationToken CancellationToken);

public sealed record SelfTrainingRunRequestFactoryDefaults(
    Func<IDisposable> BeginActivity,
    Func<Func<HttpClient?>, Action<HttpClient>, AppSettings?, ICodeCatalogProvider?, Action<string>, Task<SelfTrainingRuntimeSetup>> PrepareRuntimeAsync,
    Func<SelfTrainingRunSnapshot, Task> AppendHistoryAsync,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Func<IEnumerable<TrainingSample>, Task> MergeOrUpdateSamplesAsync,
    Func<DateTime> UtcNow);

public static class SelfTrainingRunRequestFactory
{
    public static SelfTrainingRunWorkflowRequest CreateWithDefaults(SelfTrainingRunRequestFactoryRequest request)
        => Create(
            request,
            new SelfTrainingRunRequestFactoryDefaults(
                BeginActivity: () => AiActivityTracker.Begin("Selbsttraining"),
                PrepareRuntimeAsync: SelfTrainingRuntimeSetupController.PrepareWithDefaultsAsync,
                AppendHistoryAsync: SelfTrainingHistoryStore.AppendRunAsync,
                LoadSamplesAsync: TrainingSamplesStore.LoadAsync,
                MergeOrUpdateSamplesAsync: TrainingSamplesStore.MergeOrUpdateAsync,
                UtcNow: () => DateTime.UtcNow));

    public static SelfTrainingRunWorkflowRequest Create(
        SelfTrainingRunRequestFactoryRequest request,
        SelfTrainingRunRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(defaults);

        var ui = new SelfTrainingUiSink(
            request.SetBusy,
            request.SetSelfTrainingRunning,
            request.SetLogText,
            request.SetStatusText,
            request.Log);

        return new SelfTrainingRunWorkflowRequest(
            SelectedCase: request.SelectedCase,
            Ui: ui,
            BeginActivity: defaults.BeginActivity,
            PrepareRuntimeAsync: log => defaults.PrepareRuntimeAsync(
                request.GetKbHttpClient,
                request.SetKbHttpClient,
                request.AppSettings,
                request.CodeCatalog,
                log),
            SetActiveVisionModel: request.SetActiveVisionModel,
            SetOrchestrator: request.SetOrchestrator,
            OnProgress: request.OnProgress,
            AppendHistoryAsync: defaults.AppendHistoryAsync,
            UpdateKbAsync: (result, token) => SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(
                result,
                defaults.LoadSamplesAsync,
                defaults.MergeOrUpdateSamplesAsync,
                request.IndexSamplesAsync,
                request.Log,
                token),
            ReviewQueueService: request.ReviewQueueService,
            LoadSamplesAsync: defaults.LoadSamplesAsync,
            ReloadReviewQueue: request.ReloadReviewQueue,
            LoadSamplesInternalAsync: request.LoadSamplesInternalAsync,
            RefreshKbStatusAsync: request.RefreshKbStatusAsync,
            ResetVisuals: request.ResetVisuals,
            UtcNow: defaults.UtcNow,
            CancellationToken: request.CancellationToken);
    }
}
