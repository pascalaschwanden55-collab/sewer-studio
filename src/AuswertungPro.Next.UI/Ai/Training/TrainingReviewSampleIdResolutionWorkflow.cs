using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingReviewSampleIdResolutionWorkflowRequest(
    InfraSelfImproving.ReviewQueueItem Item,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync);

public static class TrainingReviewSampleIdResolutionWorkflow
{
    public static Task<string?> ResolveAsync(TrainingReviewSampleIdResolutionWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Item);
        ArgumentNullException.ThrowIfNull(request.LoadSamplesAsync);

        return SelfTrainingReviewSampleIdResolver.ResolveAsync(
            request.Item,
            request.LoadSamplesAsync);
    }

    public static Task<string?> ResolveWithDefaultsAsync(InfraSelfImproving.ReviewQueueItem item)
        => ResolveAsync(new TrainingReviewSampleIdResolutionWorkflowRequest(
            item,
            TrainingSamplesStore.LoadAsync));
}
