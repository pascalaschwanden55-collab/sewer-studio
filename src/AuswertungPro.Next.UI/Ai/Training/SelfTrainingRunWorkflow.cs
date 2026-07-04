using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunWorkflowRequest(
    TrainingCase SelectedCase,
    SelfTrainingUiSink Ui,
    Func<IDisposable> BeginActivity,
    Func<Action<string>, Task<SelfTrainingRuntimeSetup>> PrepareRuntimeAsync,
    Action<string> SetActiveVisionModel,
    Action<ISelfTrainingOrchestrator?> SetOrchestrator,
    Action<SelfTrainingStep> OnProgress,
    Func<SelfTrainingRunSnapshot, Task> AppendHistoryAsync,
    Func<SelfTrainingResult, CancellationToken, Task> UpdateKbAsync,
    InfraSelfImproving.ReviewQueueService? ReviewQueueService,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Action<InfraSelfImproving.ReviewQueueService> ReloadReviewQueue,
    Func<Task> LoadSamplesInternalAsync,
    Func<Task> RefreshKbStatusAsync,
    Action ResetVisuals,
    Func<DateTime> UtcNow,
    CancellationToken CancellationToken);

public static class SelfTrainingRunWorkflow
{
    public static async Task RunAsync(SelfTrainingRunWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var _ = request.BeginActivity();
        try
        {
            request.Ui.SetBusy(true);
            request.Ui.SetSelfTrainingRunning(true);
            request.ResetVisuals();
            request.Ui.SetLogText("");

            var startPresentation = SelfTrainingRunPresentationBuilder.BuildStart(request.SelectedCase);
            request.Ui.SetStatusText(startPresentation.StatusText);
            foreach (var line in startPresentation.LogLines)
                request.Ui.Log(line);

            using var selfTrainingSetup = await request.PrepareRuntimeAsync(request.Ui.Log).ConfigureAwait(false);
            request.SetActiveVisionModel(selfTrainingSetup.Session.ActiveVisionModel);
            request.SetOrchestrator(selfTrainingSetup.Session.Orchestrator);

            var progress = new Progress<SelfTrainingStep>(request.OnProgress);

            request.Ui.Log(SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog());
            var result = await selfTrainingSetup.Session.Orchestrator.RunAsync(
                ToTrainingCaseInput(request.SelectedCase),
                progress,
                request.CancellationToken).ConfigureAwait(false);

            if (SelfTrainingHistorySnapshotBuilder.Build(result, request.UtcNow()) is { } snapshot)
                await request.AppendHistoryAsync(snapshot).ConfigureAwait(false);

            var completionPresentation = SelfTrainingRunPresentationBuilder.BuildCompletion(result);
            foreach (var line in completionPresentation.LogLines)
                request.Ui.Log(line);

            request.Ui.SetStatusText(completionPresentation.StatusText);

            if (SelfTrainingRunPresentationBuilder.BuildFewShotExportHint(result) is { } fewShotHint)
                request.Ui.Log(fewShotHint);

            await request.UpdateKbAsync(result, request.CancellationToken).ConfigureAwait(false);

            if (request.ReviewQueueService is not null && SelfTrainingReviewCandidateSelector.HasReviewableMatches(result))
            {
                var reviewSamples = await request.LoadSamplesAsync().ConfigureAwait(false);
                var reviewQueueUpdate = SelfTrainingReviewQueueController.EnqueueCandidates(
                    request.ReviewQueueService,
                    reviewSamples,
                    result);

                if (reviewQueueUpdate.ShouldReloadQueue)
                {
                    request.ReloadReviewQueue(request.ReviewQueueService);
                    request.Ui.Log(reviewQueueUpdate.LogMessage ?? "");
                }
            }

            await request.LoadSamplesInternalAsync().ConfigureAwait(false);
            await request.RefreshKbStatusAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            request.Ui.Log("Selbsttraining abgebrochen.");
            request.Ui.SetStatusText("Selbsttraining abgebrochen.");
        }
        catch (Exception ex)
        {
            request.Ui.Log($"FEHLER: {ex.GetType().Name}: {ex.Message}");
            request.Ui.SetStatusText($"Fehler: {ex.Message}");
        }
        finally
        {
            request.Ui.SetBusy(false);
            request.Ui.SetSelfTrainingRunning(false);
            request.SetOrchestrator(null);
        }
    }

    private static TrainingCaseInput ToTrainingCaseInput(TrainingCase trainingCase)
        => new(
            trainingCase.CaseId,
            trainingCase.FolderPath,
            trainingCase.VideoPath,
            trainingCase.ProtocolPath,
            trainingCase.InspectionDate);
}
