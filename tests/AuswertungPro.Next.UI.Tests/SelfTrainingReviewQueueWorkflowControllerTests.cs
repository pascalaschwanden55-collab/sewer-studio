using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingReviewQueueWorkflowControllerTests
{
    [Fact]
    public async Task RunAsync_ohne_queue_service_laedt_keine_samples()
    {
        var loadCalls = 0;

        await SelfTrainingReviewQueueWorkflowController.RunAsync(
            queueService: null,
            Result(partial: 1),
            () =>
            {
                loadCalls++;
                return Task.FromResult(new List<TrainingSample>());
            },
            _ => throw new InvalidOperationException("Reload darf nicht laufen."),
            _ => throw new InvalidOperationException("Log darf nicht laufen."));

        Assert.Equal(0, loadCalls);
    }

    [Fact]
    public async Task RunAsync_ohne_reviewbare_matches_laedt_keine_samples()
    {
        var loadCalls = 0;

        await SelfTrainingReviewQueueWorkflowController.RunAsync(
            new ReviewQueueService(),
            Result(),
            () =>
            {
                loadCalls++;
                return Task.FromResult(new List<TrainingSample>());
            },
            _ => throw new InvalidOperationException("Reload darf nicht laufen."),
            _ => throw new InvalidOperationException("Log darf nicht laufen."));

        Assert.Equal(0, loadCalls);
    }

    [Fact]
    public async Task RunAsync_reiht_kandidaten_ein_reloadet_queue_und_loggt()
    {
        var queue = new ReviewQueueService();
        var reloadCalls = 0;
        var logLines = new List<string>();

        await SelfTrainingReviewQueueWorkflowController.RunAsync(
            queue,
            Result(partial: 1),
            () => Task.FromResult(new List<TrainingSample>
            {
                Sample("s1", MatchLevelNames.PartialMatch),
                Sample("approved", MatchLevelNames.Mismatch, status: TrainingSampleStatus.Approved)
            }),
            reloadedQueue =>
            {
                Assert.Same(queue, reloadedQueue);
                reloadCalls++;
            },
            logLines.Add);

        Assert.Single(queue.GetAll());
        Assert.Equal(1, reloadCalls);
        Assert.Single(
            logLines,
            "1 Samples in Review Queue eingereiht (Partial/Mismatch + zurueckgehaltene ExactMatches)");
    }

    private static TrainingSample Sample(
        string sampleId,
        string matchLevel,
        TrainingSampleStatus status = TrainingSampleStatus.New)
        => new()
        {
            SampleId = sampleId,
            CaseId = "H-001",
            Code = "BAB",
            KiCode = "KI-BAB",
            MeterStart = 12.3,
            FramePath = "frame.jpg",
            MatchLevel = matchLevel,
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
