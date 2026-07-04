using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportRunWorkflowRequest(
    IReadOnlyCollection<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    ICollection<TrainingCase> Cases,
    ICodeCatalogProvider? CodeCatalog,
    Func<AiRuntimeSettings> LoadRuntimeSettings,
    Func<Task<TrainingCenterSettings>> LoadSettingsAsync,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Func<List<TrainingSample>, Task> MergeAndSaveSamplesAsync,
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
    Action<IReadOnlyList<TrainingSample>> ReplaceSamples,
    Func<Task> RefreshKbStatusAsync,
    Action ClearLivePreview,
    Action ResetSelfTrainingVisuals,
    Func<IDisposable> BeginActivity,
    Func<TrainingBatchImportWorkflowRequest, Task> RunWorkflowAsync);

public static class TrainingBatchImportRunWorkflow
{
    public static async Task RunAsync(
        TrainingBatchImportRunWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batchUi = new TrainingBatchUiSink(
            request.SetBusy,
            request.SetLogText,
            request.SetProgressValue,
            request.SetProgressMax,
            request.SetStatusText,
            request.Log);
        var caseUi = new TrainingBatchImportCaseUiSink(
            request.UpdateLivePreview,
            request.OnUi,
            request.AddResult,
            request.UpdateCodeDistribution,
            request.SetKbSampleCount,
            request.SetKbCodesCovered,
            request.Log);

        await request.RunWorkflowAsync(
            new TrainingBatchImportWorkflowRequest(
                RootFolders: request.RootFolders,
                DirectoryExists: request.DirectoryExists,
                ScanFolderAsync: request.ScanFolderAsync,
                Cases: request.Cases,
                CodeCatalog: request.CodeCatalog,
                LoadRuntimeSettings: request.LoadRuntimeSettings,
                LoadSettingsAsync: request.LoadSettingsAsync,
                LoadSamplesAsync: request.LoadSamplesAsync,
                MergeAndSaveSamplesAsync: request.MergeAndSaveSamplesAsync,
                SaveStateAsync: request.SaveStateAsync,
                ExtractPreviewFrameAsync: request.ExtractPreviewFrameAsync,
                GetSelfTrainingResultCount: request.GetSelfTrainingResultCount,
                BatchUi: batchUi,
                CaseUi: caseUi,
                ReplaceSamples: request.ReplaceSamples,
                RefreshKbStatusAsync: request.RefreshKbStatusAsync,
                ClearLivePreview: request.ClearLivePreview,
                ResetSelfTrainingVisuals: request.ResetSelfTrainingVisuals,
                BeginActivity: request.BeginActivity,
                CancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
