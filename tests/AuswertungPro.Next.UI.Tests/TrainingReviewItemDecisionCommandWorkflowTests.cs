using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewItemDecisionCommandWorkflowTests
{
    [Fact]
    public async Task RunAsync_baut_workflow_request_und_startet_decision_workflow()
    {
        var item = new ReviewQueueItem("review-1", null, 0.5, DateTime.UnixEpoch);
        var queueService = new ReviewQueueService();
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var box = new BoundingBox(0.1, 0.2, 0.3, 0.4);
        var mask = new TrainingSegmentationMask("rle", 20, 30, 12, 0.8, "damage");
        TrainingReviewItemDecisionWorkflowRequest? captured = null;

        await TrainingReviewItemDecisionCommandWorkflow.RunAsync(
            CreateRequest(item, queueService, reviewQueue) with
            {
                Decision = TrainingReviewItemDecision.Reject,
                CorrectedCode = "BAG",
                CorrectedDescription = "Korrigiert",
                Box = box,
                Mask = mask,
                RunDecisionAsync = request =>
                {
                    captured = request;
                    return Task.CompletedTask;
                }
            });

        Assert.NotNull(captured);
        Assert.Same(item, captured!.Item);
        Assert.Equal(TrainingReviewItemDecision.Reject, captured.Decision);
        Assert.Equal("BAG", captured.CorrectedCode);
        Assert.Equal("Korrigiert", captured.CorrectedDescription);
        Assert.Equal(box, captured.Box);
        Assert.Same(mask, captured.Mask);
        Assert.Same(queueService, captured.QueueService);
        Assert.Same(reviewQueue, captured.ReviewQueue);
        Assert.NotNull(captured.ProcessFeedbackAsync);
        Assert.NotNull(captured.CreateApprovalService());
        Assert.Equal(Environment.UserName, captured.ConfirmedByUser);
    }

    private static TrainingReviewItemDecisionCommandWorkflowRequest CreateRequest(
        ReviewQueueItem item,
        ReviewQueueService queueService,
        ICollection<ReviewQueueItem> reviewQueue)
        => new(
            Item: item,
            Feedback: new FeedbackIngestionService(null!, null!, null),
            QueueService: queueService,
            Decision: TrainingReviewItemDecision.Approve,
            CorrectedCode: "",
            CorrectedDescription: null,
            CancellationToken: CancellationToken.None,
            Box: null,
            Mask: null,
            ReviewQueue: reviewQueue,
            ResolveSampleIdAsync: _ => Task.FromResult<string?>("sample-1"),
            IndexSamplesAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
            DeindexSample: _ => { },
            ReloadSamplesAsync: () => Task.CompletedTask,
            OnUi: action => action(),
            SetReviewQueueCount: _ => { },
            SetReviewStatusText: _ => { },
            Log: _ => { },
            RunDecisionAsync: _ => Task.CompletedTask);

}
