using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunCommandDefaultRequestFactoryRequest(
    bool IsBusy,
    bool IsSelfTrainingRunning,
    IList<TrainingCase> Cases,
    IReadOnlyList<string> RootFolders,
    Func<string, Task<List<TrainingCaseInput>>> ScanInputsAsync,
    TrainingCase? SelectedCase,
    Action<TrainingCase> SetSelectedCase,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText,
    Action<bool> SetBusy,
    Action<bool> SetSelfTrainingRunning,
    Action<string> SetLogText,
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
    Action ResetVisuals);

public static class SelfTrainingRunCommandRequestFactory
{
    public static SelfTrainingRunCommandWorkflowRequest CreateWithDefaults(
        SelfTrainingRunCommandDefaultRequestFactoryRequest request,
        Func<SelfTrainingRunPreparationWorkflowRequest, Task<SelfTrainingRunPreparationWorkflowResult>>? prepareAsync = null,
        Func<SelfTrainingRunWorkflowRequest, Task>? runAsync = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prepare = prepareAsync ?? (workflowRequest => SelfTrainingRunPreparationWorkflow.RunAsync(workflowRequest));
        var run = runAsync ?? (workflowRequest => SelfTrainingRunWorkflow.RunAsync(workflowRequest));

        return new SelfTrainingRunCommandWorkflowRequest(
            PrepareAsync: () => prepare(
                SelfTrainingRunPreparationRequestFactory.CreateWithDefaults(
                    new SelfTrainingRunPreparationDefaultRequestFactoryRequest(
                        request.IsBusy,
                        request.IsSelfTrainingRunning,
                        request.Cases,
                        request.RootFolders,
                        request.ScanInputsAsync,
                        request.SelectedCase,
                        request.SetSelectedCase,
                        request.ResetCancellation,
                        request.SetStatusText))),
            CreateRunRequest: (selectedCase, cancellationToken) =>
                SelfTrainingRunRequestFactory.CreateWithDefaults(
                    new SelfTrainingRunRequestFactoryRequest(
                        SelectedCase: selectedCase,
                        SetBusy: request.SetBusy,
                        SetSelfTrainingRunning: request.SetSelfTrainingRunning,
                        SetLogText: request.SetLogText,
                        SetStatusText: request.SetStatusText,
                        Log: request.Log,
                        GetKbHttpClient: request.GetKbHttpClient,
                        SetKbHttpClient: request.SetKbHttpClient,
                        AppSettings: request.AppSettings,
                        CodeCatalog: request.CodeCatalog,
                        SetActiveVisionModel: request.SetActiveVisionModel,
                        SetOrchestrator: request.SetOrchestrator,
                        OnProgress: request.OnProgress,
                        IndexSamplesAsync: request.IndexSamplesAsync,
                        ReviewQueueService: request.ReviewQueueService,
                        ReloadReviewQueue: request.ReloadReviewQueue,
                        LoadSamplesInternalAsync: request.LoadSamplesInternalAsync,
                        RefreshKbStatusAsync: request.RefreshKbStatusAsync,
                        ResetVisuals: request.ResetVisuals,
                        CancellationToken: cancellationToken)),
            RunAsync: run);
    }
}
