using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCommandRequestFactoryRequest(
    Func<bool> GetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<CancellationTokenSource> CreateCancellationSource,
    Action<CancellationTokenSource> StoreCancellationSource,
    Func<TrainingBatchImportAutoApproveConfirmationResult> ConfirmAutoApprove,
    Action<string> SetStatusText,
    Func<CancellationToken, Task> RunImportAsync);

public sealed record TrainingBatchImportCommandDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<CancellationTokenSource> CreateCancellationSource,
    Action<CancellationTokenSource> StoreCancellationSource,
    Action<string> SetStatusText,
    Func<CancellationToken, Task> RunImportAsync);

public sealed record TrainingBatchImportCommandRunDefaultRequestFactoryRequest(
    Func<bool> GetIsBusy,
    IReadOnlyCollection<string> RootFolders,
    Func<CancellationTokenSource> CreateCancellationSource,
    Action<CancellationTokenSource> StoreCancellationSource,
    Func<string, Task<List<TrainingCaseInput>>> ScanInputsAsync,
    ICollection<TrainingCase> Cases,
    ICodeCatalogProvider? CodeCatalog,
    Func<Task> SaveStateAsync,
    Func<int> GetSelfTrainingResultCount,
    Action<bool> SetBusy,
    Action<string> SetLogText,
    Action<int> SetProgressValue,
    Action<int> SetProgressMax,
    Action<string> SetStatusText,
    Action<string> Log,
    Action<TrainingBatchImportLivePreview> UpdateLivePreview,
    Action<Action> OnUi,
    Action<SelfTrainingEntryResult> AddResult,
    Action<string, MatchLevel> UpdateCodeDistribution,
    Action<int> SetKbSampleCount,
    Action<int> SetKbCodesCovered,
    ObservableCollection<TrainingSample> Samples,
    Func<Task> RefreshKbStatusAsync,
    Action ClearLivePreview,
    Action ResetSelfTrainingVisuals);

public static class TrainingBatchImportCommandRequestFactory
{
    public static TrainingBatchImportCommandWorkflowRequest Create(TrainingBatchImportCommandRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.CreateCancellationSource);
        ArgumentNullException.ThrowIfNull(request.StoreCancellationSource);
        ArgumentNullException.ThrowIfNull(request.ConfirmAutoApprove);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.RunImportAsync);

        return new TrainingBatchImportCommandWorkflowRequest(
            request.GetIsBusy,
            request.RootFolders,
            request.CreateCancellationSource,
            request.StoreCancellationSource,
            request.ConfirmAutoApprove,
            request.SetStatusText,
            request.RunImportAsync);
    }

    public static TrainingBatchImportCommandWorkflowRequest CreateWithDefaults(
        TrainingBatchImportCommandDefaultRequestFactoryRequest request,
        IDialogService? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.GetIsBusy);
        ArgumentNullException.ThrowIfNull(request.RootFolders);
        ArgumentNullException.ThrowIfNull(request.CreateCancellationSource);
        ArgumentNullException.ThrowIfNull(request.StoreCancellationSource);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.RunImportAsync);

        var dialogService = dialogs ?? DialogHost.Current;

        return Create(new TrainingBatchImportCommandRequestFactoryRequest(
            request.GetIsBusy,
            request.RootFolders,
            request.CreateCancellationSource,
            request.StoreCancellationSource,
            ConfirmAutoApprove: () => TrainingBatchImportAutoApproveConfirmationController.Confirm(dialogService),
            request.SetStatusText,
            request.RunImportAsync));
    }

    public static TrainingBatchImportCommandWorkflowRequest CreateWithDefaults(
        TrainingBatchImportCommandRunDefaultRequestFactoryRequest request,
        IDialogService? dialogs = null,
        Func<TrainingBatchImportRunWorkflowRequest, CancellationToken, Task>? runBatchImportAsync = null,
        ITrainingPreviewFrameExtractor? previewFrameExtractor = null,
        ITrainingCenterSettingsStore? settingsStore = null,
        ITrainingFrameStore? frameStore = null,
        ITrainingSampleStore? trainingSamples = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ScanInputsAsync);

        var runBatchImport = runBatchImportAsync ?? TrainingBatchImportRunWorkflow.RunAsync;

        return CreateWithDefaults(
            new TrainingBatchImportCommandDefaultRequestFactoryRequest(
                GetIsBusy: request.GetIsBusy,
                RootFolders: request.RootFolders,
                CreateCancellationSource: request.CreateCancellationSource,
                StoreCancellationSource: request.StoreCancellationSource,
                SetStatusText: request.SetStatusText,
                RunImportAsync: async ct =>
                {
                    await runBatchImport(
                        TrainingBatchImportRunRequestFactory.CreateWithDefaults(
                            new TrainingBatchImportRunDefaultRequestFactoryRequest(
                                RootFolders: request.RootFolders,
                                ScanInputsAsync: request.ScanInputsAsync,
                                Cases: request.Cases,
                                CodeCatalog: request.CodeCatalog,
                                SaveStateAsync: request.SaveStateAsync,
                                GetSelfTrainingResultCount: request.GetSelfTrainingResultCount,
                                SetBusy: request.SetBusy,
                                SetLogText: request.SetLogText,
                                SetProgressValue: request.SetProgressValue,
                                SetProgressMax: request.SetProgressMax,
                                SetStatusText: request.SetStatusText,
                                Log: request.Log,
                                UpdateLivePreview: request.UpdateLivePreview,
                                OnUi: request.OnUi,
                                AddResult: request.AddResult,
                                UpdateCodeDistribution: request.UpdateCodeDistribution,
                                SetKbSampleCount: request.SetKbSampleCount,
                                SetKbCodesCovered: request.SetKbCodesCovered,
                                Samples: request.Samples,
                                RefreshKbStatusAsync: request.RefreshKbStatusAsync,
                                ClearLivePreview: request.ClearLivePreview,
                                ResetSelfTrainingVisuals: request.ResetSelfTrainingVisuals),
                            previewFrameExtractor,
                            settingsStore,
                            frameStore,
                            trainingSamples),
                        ct).ConfigureAwait(false);
                }),
            dialogs);
    }
}
