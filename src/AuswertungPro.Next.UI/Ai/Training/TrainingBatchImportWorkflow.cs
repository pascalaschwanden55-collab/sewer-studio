using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportWorkflowRequest(
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
    TrainingBatchUiSink BatchUi,
    TrainingBatchImportCaseUiSink CaseUi,
    Action<IReadOnlyList<TrainingSample>> ReplaceSamples,
    Func<Task> RefreshKbStatusAsync,
    Action ClearLivePreview,
    Action ResetSelfTrainingVisuals,
    Func<IDisposable> BeginActivity,
    CancellationToken CancellationToken,
    ITrainingFrameStore? FrameStore = null);

public static class TrainingBatchImportWorkflow
{
    public static async Task RunAsync(TrainingBatchImportWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var _ = request.BeginActivity();
        try
        {
            request.BatchUi.SetBusy(true);
            request.BatchUi.SetLogText("");
            request.BatchUi.SetProgressValue(0);
            request.BatchUi.SetProgressMax(1);
            request.ClearLivePreview();
            request.ResetSelfTrainingVisuals();

            var scanWorkflow = await TrainingBatchImportScanWorkflowController.RunAsync(
                request.RootFolders.Count,
                () => TrainingBatchImportScanController.ScanAsync(
                    request.RootFolders,
                    request.DirectoryExists,
                    request.ScanFolderAsync,
                    request.BatchUi.Log),
                request.Cases,
                request.BatchUi.Log,
                request.BatchUi.SetStatusText).ConfigureAwait(false);
            if (scanWorkflow.ShouldStop)
                return;

            var runtimeSetup = await TrainingBatchImportRuntimeSetupController.PrepareAsync(
                scanWorkflow.CasesWithProtocol,
                request.LoadRuntimeSettings,
                request.LoadSettingsAsync,
                (cfg, settings) =>
                {
                    var meterSvc = TrainingMeterTimelineServiceFactory.Create(cfg, settings.GpuConcurrency);
                    return new TrainingSampleGenerator(
                        cfg,
                        meterSvc,
                        settings,
                        request.CodeCatalog,
                        request.FrameStore ?? FrameStore.Current);
                },
                async () => await request.LoadSamplesAsync().ConfigureAwait(false),
                request.BatchUi.SetProgressMax,
                request.BatchUi.Log).ConfigureAwait(false);

            await TrainingBatchImportCaseLoopController.RunAsync(
                runtimeSetup.CasesToProcess,
                (caseIndex, totalCount, trainingCase) =>
                {
                    request.BatchUi.SetProgressValue(caseIndex + 1);
                    var progressPresentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
                        caseIndex,
                        totalCount,
                        trainingCase);
                    request.BatchUi.SetStatusText(progressPresentation.StatusText);
                    foreach (var line in progressPresentation.LogLines)
                        request.BatchUi.Log(line);
                },
                async (caseIndex, trainingCase, token) =>
                {
                    await TrainingBatchImportCaseWorkflowController.ProcessAsync(
                        trainingCase,
                        runtimeSetup.ExistingSignatures,
                        runtimeSetup.AllSamples,
                        request.GetSelfTrainingResultCount() + 1,
                        caseIndex + 1,
                        runtimeSetup.RunSummary,
                        (currentCase, currentToken) =>
                            request.ExtractPreviewFrameAsync(currentCase, runtimeSetup.Config, currentToken),
                        (input, signatures, currentToken) =>
                            runtimeSetup.Generator.GenerateWithDiagnosticsAsync(input, signatures, framesDir: null, currentToken),
                        request.CaseUi,
                        request.MergeAndSaveSamplesAsync,
                        request.SaveStateAsync,
                        token).ConfigureAwait(false);
                },
                ex =>
                {
                    runtimeSetup.RunSummary.RecordError(ex.Message);
                    request.BatchUi.Log($"  FEHLER: {ex.Message}");
                },
                request.CancellationToken).ConfigureAwait(false);

            var completion = await TrainingBatchImportRunCompletionController.CompleteAsync(
                runtimeSetup.RunSummary,
                runtimeSetup.CasesToProcess.Count,
                async () => await request.LoadSamplesAsync().ConfigureAwait(false),
                request.ReplaceSamples,
                request.RefreshKbStatusAsync,
                request.SaveStateAsync,
                request.BatchUi.Log,
                request.BatchUi.SetStatusText).ConfigureAwait(false);
            if (completion.ShouldStop)
                return;
        }
        catch (OperationCanceledException)
        {
            request.BatchUi.Log("Batch-Import abgebrochen durch Benutzer.");
            request.BatchUi.SetStatusText("Batch-Import abgebrochen.");
        }
        catch (Exception ex)
        {
            request.BatchUi.Log($"FATALER FEHLER: {ex.Message}");
            request.BatchUi.SetStatusText($"Fehler beim Batch-Import: {ex.Message}");
        }
        finally
        {
            request.BatchUi.SetBusy(false);
        }
    }
}
