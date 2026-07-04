using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewQueueLoadWorkflowTests
{
    [Fact]
    public void Run_laedt_items_auf_ui_thread_und_setzt_count_und_status()
    {
        var queue = new ObservableCollection<InfraSelfImproving.ReviewQueueItem>
        {
            Item("old")
        };
        var service = new InfraSelfImproving.ReviewQueueService();
        service.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: "BAA",
            suggestedCode: "BAA",
            meter: 12.3,
            framePath: "frame.jpg",
            matchLevel: "PartialMatch");
        service.EnqueueFromSelfTraining(
            caseId: "case-2",
            vsaCode: "BAB",
            suggestedCode: "BAB",
            meter: 13.4,
            framePath: "frame2.jpg",
            matchLevel: "ExactMatch");
        var uiCalls = 0;
        var count = -1;
        var status = "";

        TrainingReviewQueueLoadWorkflow.Run(
            new TrainingReviewQueueLoadWorkflowRequest(
                service,
                queue,
                value => count = value,
                value => status = value,
                action =>
                {
                    uiCalls++;
                    action();
                }));

        Assert.Equal(1, uiCalls);
        Assert.Equal(2, queue.Count);
        Assert.Equal(2, count);
        Assert.Equal("2 Eintr\u00e4ge zur Pr\u00fcfung", status);
        Assert.DoesNotContain(queue, item => item.Id == "old");
    }

    [Fact]
    public void Run_verlangt_queue_service()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TrainingReviewQueueLoadWorkflow.Run(
                new TrainingReviewQueueLoadWorkflowRequest(
                    QueueService: null!,
                    ReviewQueue: new ObservableCollection<InfraSelfImproving.ReviewQueueItem>(),
                    SetReviewQueueCount: _ => { },
                    SetReviewStatusText: _ => { },
                    OnUi: action => action())));
    }

    private static InfraSelfImproving.ReviewQueueItem Item(string id)
        => new(
            Id: id,
            Entry: null,
            Priority: 0,
            EnqueuedUtc: DateTime.UtcNow);
}
