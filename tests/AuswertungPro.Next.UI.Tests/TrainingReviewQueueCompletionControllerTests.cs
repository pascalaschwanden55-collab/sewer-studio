using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewQueueCompletionControllerTests
{
    [Fact]
    public void ApplyApproved_removes_item_and_builds_existing_status_and_log_texts()
    {
        var queueService = CreateQueueWithSelfTrainingItem("BAA", "BAA", 12.3);
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<InfraSelfImproving.ReviewQueueItem> { item };

        var result = TrainingReviewQueueCompletionController.ApplyApproved(item, queueService, reviewQueue);

        Assert.Equal(0, queueService.Count);
        Assert.Empty(reviewQueue);
        Assert.Equal(0, result.ReviewQueueCount);
        Assert.Equal("Approved: BAA | 0 verbleibend", result.StatusText);
        Assert.Equal("Review Approved: BAA @ 12.3m (PartialMatch) \u2192 BAA", result.LogText);
    }

    [Fact]
    public void ApplyRejected_removes_item_and_builds_existing_status_and_log_texts()
    {
        var queueService = CreateQueueWithSelfTrainingItem("BAB", "BAB", 4.5);
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<InfraSelfImproving.ReviewQueueItem> { item };

        var result = TrainingReviewQueueCompletionController.ApplyRejected(item, "BAG", queueService, reviewQueue);

        Assert.Equal(0, queueService.Count);
        Assert.Empty(reviewQueue);
        Assert.Equal(0, result.ReviewQueueCount);
        Assert.Equal("Rejected: BAB \u2192 BAG | 0 verbleibend", result.StatusText);
        Assert.Equal("Review Rejected: BAB @ 4.5m (PartialMatch) \u2192 BAB korrigiert zu BAG", result.LogText);
    }

    private static InfraSelfImproving.ReviewQueueService CreateQueueWithSelfTrainingItem(
        string vsaCode,
        string suggestedCode,
        double meter)
    {
        var queueService = new InfraSelfImproving.ReviewQueueService();
        queueService.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: vsaCode,
            suggestedCode: suggestedCode,
            meter: meter,
            framePath: "frame.jpg",
            matchLevel: "PartialMatch");

        return queueService;
    }
}
