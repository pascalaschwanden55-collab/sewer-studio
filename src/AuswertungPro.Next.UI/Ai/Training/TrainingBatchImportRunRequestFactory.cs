using System.Collections.ObjectModel;
using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportRunRequestFactoryRequest(
    IReadOnlyCollection<string> RootFolders,
    Func<string, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    ICollection<TrainingCase> Cases,
    ICodeCatalogProvider? CodeCatalog,
    Func<Task> SaveStateAsync,
    Func<TrainingCase, AiRuntimeSettings, CancellationToken, Task<string?>> ExtractPreviewFrameAsync,
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

public sealed record TrainingBatchImportRunDefaultRequestFactoryRequest(
    IReadOnlyCollection<string> RootFolders,
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

public sealed record TrainingBatchImportRunRequestFactoryDefaults(
    Func<string, bool> DirectoryExists,
    Func<AiRuntimeSettings> LoadRuntimeSettings,
    Func<Task<TrainingCenterSettings>> LoadSettingsAsync,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Func<List<TrainingSample>, Task> MergeAndSaveSamplesAsync,
    Func<TrainingBatchImportWorkflowRequest, Task> RunWorkflowAsync,
    Func<IDisposable> BeginActivity);

public static class TrainingBatchImportRunRequestFactory
{
    public static TrainingBatchImportRunWorkflowRequest CreateWithDefaults(
        TrainingBatchImportRunDefaultRequestFactoryRequest request,
        ITrainingPreviewFrameExtractor? previewFrameExtractor = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ScanInputsAsync);

        return CreateWithDefaults(new TrainingBatchImportRunRequestFactoryRequest(
            RootFolders: request.RootFolders,
            ScanFolderAsync: async folder =>
            {
                var inputs = await request.ScanInputsAsync(folder).ConfigureAwait(false);
                return inputs.Select(TrainingCaseInputMapper.ToTrainingCase).ToList();
            },
            Cases: request.Cases,
            CodeCatalog: request.CodeCatalog,
            SaveStateAsync: request.SaveStateAsync,
            ExtractPreviewFrameAsync: (previewFrameExtractor ?? TrainingPreviewFrameExtractor.Current)
                .ExtractPreviewFrameAsync,
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
            ResetSelfTrainingVisuals: request.ResetSelfTrainingVisuals));
    }

    public static TrainingBatchImportRunWorkflowRequest CreateWithDefaults(
        TrainingBatchImportRunRequestFactoryRequest request)
        => Create(
            request,
            new TrainingBatchImportRunRequestFactoryDefaults(
                DirectoryExists: Directory.Exists,
                LoadRuntimeSettings: () => PlayerAiSettingsLoader.LoadRuntimeSettings(),
                LoadSettingsAsync: TrainingCenterSettingsStore.LoadAsync,
                LoadSamplesAsync: TrainingSamplesStore.LoadAsync,
                MergeAndSaveSamplesAsync: TrainingSamplesStore.MergeAndSaveAsync,
                RunWorkflowAsync: TrainingBatchImportWorkflow.RunAsync,
                BeginActivity: () => AiTrack.Begin("Training Center")));

    public static TrainingBatchImportRunWorkflowRequest Create(
        TrainingBatchImportRunRequestFactoryRequest request,
        TrainingBatchImportRunRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(defaults);

        return new TrainingBatchImportRunWorkflowRequest(
            RootFolders: request.RootFolders,
            DirectoryExists: defaults.DirectoryExists,
            ScanFolderAsync: request.ScanFolderAsync,
            Cases: request.Cases,
            CodeCatalog: request.CodeCatalog,
            LoadRuntimeSettings: defaults.LoadRuntimeSettings,
            LoadSettingsAsync: defaults.LoadSettingsAsync,
            LoadSamplesAsync: defaults.LoadSamplesAsync,
            MergeAndSaveSamplesAsync: defaults.MergeAndSaveSamplesAsync,
            SaveStateAsync: request.SaveStateAsync,
            ExtractPreviewFrameAsync: request.ExtractPreviewFrameAsync,
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
            ReplaceSamples: items => TrainingSampleCollectionController.ReplaceWith(request.Samples, items),
            RefreshKbStatusAsync: request.RefreshKbStatusAsync,
            ClearLivePreview: request.ClearLivePreview,
            ResetSelfTrainingVisuals: request.ResetSelfTrainingVisuals,
            BeginActivity: defaults.BeginActivity,
            RunWorkflowAsync: defaults.RunWorkflowAsync);
    }
}
