using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewItemDecisionRequestFactoryTests
{
    [Fact]
    public void Create_fuellt_request_mit_uebergebenem_kontext()
    {
        var item = new ReviewQueueItem("review-1", null, 0.5, DateTime.UnixEpoch);
        var queueService = new ReviewQueueService();
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var box = new BoundingBox(0.1, 0.2, 0.3, 0.4);
        var mask = new TrainingSegmentationMask("rle", 20, 30, 12, 0.8, "damage");
        var feedback = CreateFeedbackService();

        var request = TrainingReviewItemDecisionRequestFactory.Create(
            item,
            feedback,
            queueService,
            TrainingReviewItemDecision.Reject,
            correctedCode: "BAG",
            correctedDescription: "Beschreibung",
            CancellationToken.None,
            box,
            mask,
            reviewQueue,
            confirmedByUser: "tester",
            ResolveSampleIdAsync,
            () => new ReviewApprovalServiceFake(),
            () => Task.CompletedTask,
            action => action(),
            _ => { },
            _ => { },
            _ => { });

        Assert.Same(item, request.Item);
        Assert.Equal(TrainingReviewItemDecision.Reject, request.Decision);
        Assert.Equal("BAG", request.CorrectedCode);
        Assert.Equal("Beschreibung", request.CorrectedDescription);
        Assert.Equal(box, request.Box);
        Assert.Same(mask, request.Mask);
        Assert.Same(queueService, request.QueueService);
        Assert.Same(reviewQueue, request.ReviewQueue);
        Assert.Equal("tester", request.ConfirmedByUser);
        Assert.Same((Func<ReviewQueueItem, Task<string?>>)ResolveSampleIdAsync, request.ResolveSampleIdAsync);
    }

    [Fact]
    public void Create_setzt_feedback_delegate()
    {
        var item = new ReviewQueueItem("review-1", null, 0.5, DateTime.UnixEpoch);
        var feedback = CreateFeedbackService();
        var request = TrainingReviewItemDecisionRequestFactory.Create(
            item,
            feedback,
            new ReviewQueueService(),
            TrainingReviewItemDecision.Approve,
            correctedCode: "",
            correctedDescription: null,
            CancellationToken.None,
            box: null,
            mask: null,
            new ObservableCollection<ReviewQueueItem>(),
            confirmedByUser: "tester",
            ResolveSampleIdAsync,
            () => new ReviewApprovalServiceFake(),
            () => Task.CompletedTask,
            action => action(),
            _ => { },
            _ => { },
            _ => { });

        Assert.NotNull(request.ProcessFeedbackAsync);
    }

    private static Task<string?> ResolveSampleIdAsync(ReviewQueueItem _)
        => Task.FromResult<string?>("sample-1");

    private static FeedbackIngestionService CreateFeedbackService()
        => new(null!, null!, null);

    private sealed class ReviewApprovalServiceFake : IReviewApprovalService
    {
        public Task<ReviewApplyResult> ApproveSelfTrainingAsync(
            string sampleId,
            BoundingBox? box,
            CancellationToken ct,
            string confirmedByUser,
            TrainingSegmentationMask? mask = null)
            => Task.FromResult(new ReviewApplyResult(true, true, false, null));

        public Task<ReviewApplyResult> RejectSelfTrainingAsync(
            string sampleId,
            string? correctedCode,
            CancellationToken ct,
            string confirmedByUser,
            string? correctedDescription = null)
            => Task.FromResult(new ReviewApplyResult(true, false, true, null));
    }
}
