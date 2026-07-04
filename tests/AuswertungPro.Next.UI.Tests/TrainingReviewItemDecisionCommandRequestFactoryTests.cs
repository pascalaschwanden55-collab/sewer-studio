using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewItemDecisionCommandRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_review_item_decision_command_request()
    {
        var calls = new List<string>();
        var item = new ReviewQueueItem("review-1", null, 0.5, DateTime.UnixEpoch);
        var feedback = new FeedbackIngestionService(null!, null!, null);
        var queueService = new ReviewQueueService();
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var box = new BoundingBox(0.1, 0.2, 0.3, 0.4);
        var mask = new TrainingSegmentationMask("rle", 20, 30, 12, 0.8, "damage");
        var outcome = new KbIndexOutcome([], []);

        var request = TrainingReviewItemDecisionCommandRequestFactory.Create(
            new TrainingReviewItemDecisionCommandRequestFactoryRequest(
                Item: item,
                Feedback: feedback,
                QueueService: queueService,
                Decision: TrainingReviewItemDecision.Reject,
                CorrectedCode: "BAG",
                CorrectedDescription: "Korrigiert",
                CancellationToken: CancellationToken.None,
                Box: box,
                Mask: mask,
                ReviewQueue: reviewQueue,
                ResolveSampleIdAsync: actualItem =>
                {
                    calls.Add("resolve:" + actualItem.Id);
                    return Task.FromResult<string?>("sample-1");
                },
                IndexSamplesAsync: (samples, _) =>
                {
                    calls.Add("index:" + samples.Count);
                    return Task.FromResult(outcome);
                },
                DeindexSample: sampleId => calls.Add("deindex:" + sampleId),
                ReloadSamplesAsync: () =>
                {
                    calls.Add("reload");
                    return Task.CompletedTask;
                },
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                },
                SetReviewQueueCount: value => calls.Add("count:" + value),
                SetReviewStatusText: value => calls.Add("status:" + value),
                Log: value => calls.Add("log:" + value)));

        Assert.Same(item, request.Item);
        Assert.Same(feedback, request.Feedback);
        Assert.Same(queueService, request.QueueService);
        Assert.Equal(TrainingReviewItemDecision.Reject, request.Decision);
        Assert.Equal("BAG", request.CorrectedCode);
        Assert.Equal("Korrigiert", request.CorrectedDescription);
        Assert.Equal(box, request.Box);
        Assert.Same(mask, request.Mask);
        Assert.Same(reviewQueue, request.ReviewQueue);
        Assert.Equal("sample-1", await request.ResolveSampleIdAsync(item));
        Assert.Same(outcome, await request.IndexSamplesAsync([new TrainingSample()], CancellationToken.None));
        request.DeindexSample("sample-1");
        await request.ReloadSamplesAsync();
        request.OnUi(() => request.SetReviewQueueCount(1));
        request.SetReviewStatusText("ok");
        request.Log("fertig");

        Assert.Equal(
            [
                "resolve:review-1",
                "index:1",
                "deindex:sample-1",
                "reload",
                "on-ui",
                "count:1",
                "status:ok",
                "log:fertig"
            ],
            calls);
    }
}
