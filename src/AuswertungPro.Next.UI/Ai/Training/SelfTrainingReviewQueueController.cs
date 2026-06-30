using AuswertungPro.Next.Application.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingReviewQueueUpdate(
    int EnqueuedCount,
    bool ShouldReloadQueue,
    string? LogMessage);

public static class SelfTrainingReviewQueueController
{
    public static SelfTrainingReviewQueueUpdate EnqueueCandidates(
        InfraSelfImproving.ReviewQueueService? queueService,
        IEnumerable<TrainingSample> samples,
        SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(result);

        if (queueService is null || !SelfTrainingReviewCandidateSelector.HasReviewableMatches(result))
            return Empty();

        var reviewCandidates = SelfTrainingReviewCandidateSelector.SelectForRun(samples, result);
        foreach (var sample in reviewCandidates)
        {
            queueService.EnqueueFromSelfTraining(
                sample.CaseId,
                sample.Code,
                sample.KiCode ?? sample.Code,
                sample.MeterStart,
                sample.FramePath!,
                sample.MatchLevel!,
                reason: string.IsNullOrWhiteSpace(sample.Notes) ? null : sample.Notes,
                sampleId: sample.SampleId);
        }

        return reviewCandidates.Count > 0
            ? new SelfTrainingReviewQueueUpdate(
                reviewCandidates.Count,
                ShouldReloadQueue: true,
                $"{reviewCandidates.Count} Samples in Review Queue eingereiht (Partial/Mismatch + zurueckgehaltene ExactMatches)")
            : Empty();
    }

    private static SelfTrainingReviewQueueUpdate Empty()
        => new(EnqueuedCount: 0, ShouldReloadQueue: false, LogMessage: null);
}
