using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingReviewQueueWorkflowController
{
    public static async Task RunAsync(
        InfraSelfImproving.ReviewQueueService? queueService,
        SelfTrainingResult result,
        Func<Task<List<TrainingSample>>> loadSamplesAsync,
        Action<InfraSelfImproving.ReviewQueueService> reloadQueue,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(reloadQueue);
        ArgumentNullException.ThrowIfNull(log);

        if (queueService is null || !SelfTrainingReviewCandidateSelector.HasReviewableMatches(result))
            return;

        var samples = await loadSamplesAsync();
        var update = SelfTrainingReviewQueueController.EnqueueCandidates(
            queueService,
            samples,
            result);

        if (!update.ShouldReloadQueue)
            return;

        reloadQueue(queueService);
        log(update.LogMessage ?? "");
    }
}
