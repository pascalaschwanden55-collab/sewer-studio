using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingReviewQueueControllerTests
{
    [Fact]
    public void EnqueueCandidates_ohne_queue_service_macht_nichts()
    {
        var result = Result(exact: 1);

        var update = SelfTrainingReviewQueueController.EnqueueCandidates(
            queueService: null,
            samples: new[] { Sample("s1", MatchLevelNames.ExactMatch) },
            result);

        Assert.Equal(0, update.EnqueuedCount);
        Assert.False(update.ShouldReloadQueue);
        Assert.Null(update.LogMessage);
    }

    [Fact]
    public void EnqueueCandidates_reiht_reviewbare_samples_ein()
    {
        var queue = new ReviewQueueService();
        var result = Result(exact: 1, partial: 1);
        var samples = new[]
        {
            Sample("s1", MatchLevelNames.ExactMatch, kiCode: null, notes: "HumanReviewRequired"),
            Sample("s2", MatchLevelNames.PartialMatch, kiCode: "KI-BAB", notes: " "),
            Sample("approved", MatchLevelNames.Mismatch, status: TrainingSampleStatus.Approved)
        };

        var update = SelfTrainingReviewQueueController.EnqueueCandidates(queue, samples, result);

        Assert.Equal(2, update.EnqueuedCount);
        Assert.True(update.ShouldReloadQueue);
        Assert.Equal(
            "2 Samples in Review Queue eingereiht (Partial/Mismatch + zurueckgehaltene ExactMatches)",
            update.LogMessage);

        var queued = queue.GetAll();
        Assert.Equal(2, queued.Count);
        Assert.Contains(queued, item =>
            item.SelfTrainingSampleId == "s1"
            && item.SelfTrainingSuggestedCode == "BAB"
            && item.SelfTrainingReason == "HumanReviewRequired");
        Assert.Contains(queued, item =>
            item.SelfTrainingSampleId == "s2"
            && item.SelfTrainingSuggestedCode == "KI-BAB"
            && item.SelfTrainingReason is null);
    }

    private static TrainingSample Sample(
        string sampleId,
        string matchLevel,
        string? kiCode = null,
        string? notes = null,
        TrainingSampleStatus status = TrainingSampleStatus.New)
        => new()
        {
            SampleId = sampleId,
            CaseId = "H-001",
            Code = "BAB",
            KiCode = kiCode,
            MeterStart = 12.3,
            FramePath = "frame.jpg",
            MatchLevel = matchLevel,
            Notes = notes ?? string.Empty,
            Status = status
        };

    private static SelfTrainingResult Result(
        int exact = 0,
        int partial = 0,
        int mismatch = 0,
        int noFindings = 0)
        => new(
            "H-001",
            TotalEntries: exact + partial + mismatch + noFindings,
            ExactMatches: exact,
            PartialMatches: partial,
            Mismatches: mismatch,
            NoFindings: noFindings,
            OverallTechnique: null,
            Duration: TimeSpan.Zero,
            SamplesGenerated: 0);
}
