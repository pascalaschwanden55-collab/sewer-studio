using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewQueueLoadControllerTests
{
    [Fact]
    public void Load_reads_queue_items_in_service_order_and_builds_existing_status_text()
    {
        var queueService = new InfraSelfImproving.ReviewQueueService();
        queueService.EnqueueFromSelfTraining(
            caseId: "case-low",
            vsaCode: "BAA",
            suggestedCode: "BAA",
            meter: 1.2,
            framePath: "low.jpg",
            matchLevel: "NoFindings");
        queueService.EnqueueFromSelfTraining(
            caseId: "case-high",
            vsaCode: "BAB",
            suggestedCode: "BAB",
            meter: 3.4,
            framePath: "high.jpg",
            matchLevel: "PartialMatch");

        var result = TrainingReviewQueueLoadController.Load(queueService);

        Assert.Equal(2, result.ReviewQueueCount);
        Assert.Equal("2 Einträge zur Prüfung", result.StatusText);
        Assert.Equal(["BAB", "BAA"], result.Items.Select(i => i.SelfTrainingVsaCode));
    }

    [Fact]
    public void Load_handles_empty_queue_with_existing_status_text()
    {
        var result = TrainingReviewQueueLoadController.Load(new InfraSelfImproving.ReviewQueueService());

        Assert.Equal(0, result.ReviewQueueCount);
        Assert.Equal("0 Einträge zur Prüfung", result.StatusText);
        Assert.Empty(result.Items);
    }
}
