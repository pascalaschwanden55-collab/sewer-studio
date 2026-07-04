using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSelectedReviewCommandRequestFactoryTests
{
    [Fact]
    public async Task CreateApprove_verdrahtet_pending_geometry_runtime_und_ui_delegates()
    {
        var item = Item();
        var queue = new ReviewQueueService();
        var box = new BoundingBox(0.5, 0.6, 0.2, 0.3);
        var mask = new TrainingSegmentationMask("rle", 100, 80, 250, 0.91, "damage");
        var cts = new CancellationTokenSource();
        var calls = new List<string>();
        var settings = new AppSettings();

        var request = TrainingSelectedReviewCommandRequestFactory.CreateApprove(
            new TrainingSelectedReviewApproveFactoryRequest(
                item,
                queue,
                () => box,
                () => mask,
                () => calls.Add("clear"),
                settings,
                (_, _, _, _, _, _) =>
                {
                    calls.Add("approve-handler");
                    return Task.CompletedTask;
                },
                cts.Token,
                value => calls.Add("log:" + value),
                action =>
                {
                    calls.Add("ui");
                    action();
                },
                value => calls.Add("status:" + value)),
            new TrainingSelectedReviewCommandRequestFactoryDefaults(
                ApproveWithDefaultsAsync: (actualItem, actualQueue, actualToken, actualBox, actualMask, actualSettings, _) =>
                {
                    Assert.Same(item, actualItem);
                    Assert.Same(queue, actualQueue);
                    Assert.Equal(cts.Token, actualToken);
                    Assert.Equal(box, actualBox);
                    Assert.Same(mask, actualMask);
                    Assert.Same(settings, actualSettings);
                    calls.Add("approve-runtime");
                    return Task.CompletedTask;
                },
                RejectWithDefaultsAsync: (_, _, _, _, _) => throw new InvalidOperationException("reject unexpected"),
                CorrectWithDefaultsAsync: (_, _, _, _, _, _, _) => throw new InvalidOperationException("correct unexpected")));

        await request.ApproveAsync(item, queue, cts.Token, box, mask);
        request.ClearPendingReviewGeometry();
        request.Log("x");
        request.OnUi(() => request.SetReviewStatusText("ok"));

        Assert.Same(item, request.Item);
        Assert.Same(queue, request.QueueService);
        Assert.Equal(box, request.GetPendingBox());
        Assert.Same(mask, request.GetPendingMask());
        Assert.Equal(cts.Token, request.CancellationToken);
        Assert.Equal(["approve-runtime", "clear", "log:x", "ui", "status:ok"], calls);
    }

    [Fact]
    public async Task CreateReject_und_CreateCorrection_verdrahten_runtime_defaults()
    {
        var item = Item();
        var queue = new ReviewQueueService();
        var cts = new CancellationTokenSource();
        var settings = new AppSettings();
        var calls = new List<string>();

        var defaults = new TrainingSelectedReviewCommandRequestFactoryDefaults(
            ApproveWithDefaultsAsync: (_, _, _, _, _, _, _) => throw new InvalidOperationException("approve unexpected"),
            RejectWithDefaultsAsync: (actualItem, actualQueue, actualToken, actualSettings, _) =>
            {
                Assert.Same(item, actualItem);
                Assert.Same(queue, actualQueue);
                Assert.Equal(cts.Token, actualToken);
                Assert.Same(settings, actualSettings);
                calls.Add("reject-runtime");
                return Task.CompletedTask;
            },
            CorrectWithDefaultsAsync: (actualItem, code, description, actualQueue, actualToken, actualSettings, _) =>
            {
                Assert.Same(item, actualItem);
                Assert.Equal("BAG", code);
                Assert.Equal("Beschreibung", description);
                Assert.Same(queue, actualQueue);
                Assert.Equal(cts.Token, actualToken);
                Assert.Same(settings, actualSettings);
                calls.Add("correct-runtime");
                return Task.CompletedTask;
            });

        var rejectRequest = TrainingSelectedReviewCommandRequestFactory.CreateReject(
            new TrainingSelectedReviewRejectFactoryRequest(
                item,
                queue,
                settings,
                (_, _, _, _, _, _) => Task.CompletedTask,
                cts.Token,
                value => calls.Add("log:" + value),
                action => action(),
                value => calls.Add("status:" + value)),
            defaults);
        var correctionRequest = TrainingSelectedReviewCommandRequestFactory.CreateCorrection(
            new TrainingSelectedReviewCorrectionFactoryRequest(
                item,
                queue,
                "BAG",
                "Beschreibung",
                settings,
                (_, _, _, _, _, _) => Task.CompletedTask,
                cts.Token,
                value => calls.Add("log:" + value),
                action => action(),
                value => calls.Add("status:" + value)),
            defaults);

        await rejectRequest.RejectAsync(item, queue, cts.Token);
        await correctionRequest.ApplyCorrectionAsync(item, "BAG", "Beschreibung", cts.Token);

        Assert.Same(item, rejectRequest.Item);
        Assert.Same(queue, rejectRequest.QueueService);
        Assert.Same(item, correctionRequest.Item);
        Assert.Same(queue, correctionRequest.QueueService);
        Assert.Equal("BAG", correctionRequest.CorrectedCode);
        Assert.Equal("Beschreibung", correctionRequest.CorrectedDescription);
        Assert.Equal(["reject-runtime", "correct-runtime"], calls);
    }

    private static ReviewQueueItem Item()
        => new("review-1", null, 0.5, DateTime.UnixEpoch);
}
