using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataApprovalWorkflowTests
{
    [Fact]
    public async Task RunAsync_tut_nichts_wenn_queue_service_fehlt()
    {
        var calls = new List<string>();

        await TrainingProtocolStartdataApprovalWorkflow.RunAsync(
            new TrainingProtocolStartdataApprovalWorkflowRequest(
                QueueService: null,
                SelectItems: () =>
                {
                    calls.Add("select");
                    return [];
                },
                ApproveAsync: (_, _, _) =>
                {
                    calls.Add("approve");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                Log: value => calls.Add("log:" + value),
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                },
                SetReviewStatusText: value => calls.Add("status:" + value)));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_waehlt_items_gibt_sie_frei_und_setzt_abschluss_status()
    {
        var queue = new InfraSelfImproving.ReviewQueueService();
        var items = new[]
        {
            Item("item-1", "BAB"),
            Item("item-2", "BBA")
        };
        var calls = new List<string>();
        var cts = new CancellationTokenSource();

        await TrainingProtocolStartdataApprovalWorkflow.RunAsync(
            new TrainingProtocolStartdataApprovalWorkflowRequest(
                QueueService: queue,
                SelectItems: () =>
                {
                    calls.Add("select");
                    return items;
                },
                ApproveAsync: (item, queueService, token) =>
                {
                    Assert.Same(queue, queueService);
                    Assert.Equal(cts.Token, token);
                    calls.Add("approve:" + item.Id);
                    return Task.CompletedTask;
                },
                cts.Token,
                Log: value => calls.Add("log:" + value),
                OnUi: action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                },
                SetReviewStatusText: value => calls.Add("status:" + value)));

        Assert.Equal(
            [
                "select",
                "approve:item-1",
                "approve:item-2",
                "ui-before",
                "status:2/2 Protokoll-Startdaten freigegeben.",
                "ui-after"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_loggt_fehler_und_macht_mit_naechstem_item_weiter()
    {
        var queue = new InfraSelfImproving.ReviewQueueService();
        var items = new[]
        {
            Item("item-1", "BAB"),
            Item("item-2", "BBA")
        };
        var calls = new List<string>();

        await TrainingProtocolStartdataApprovalWorkflow.RunAsync(
            new TrainingProtocolStartdataApprovalWorkflowRequest(
                QueueService: queue,
                SelectItems: () => items,
                ApproveAsync: (item, _, _) =>
                {
                    calls.Add("approve:" + item.Id);
                    if (item.Id == "item-1")
                        throw new InvalidOperationException("defekt");

                    return Task.CompletedTask;
                },
                CancellationToken.None,
                Log: value => calls.Add("log:" + value),
                OnUi: action => action(),
                SetReviewStatusText: value => calls.Add("status:" + value)));

        Assert.Equal(
            [
                "approve:item-1",
                "approve:item-2",
                "log:Startdaten-Freigabe Fehler (BAB): defekt",
                "status:1/2 Protokoll-Startdaten freigegeben."
            ],
            calls);
    }

    private static InfraSelfImproving.ReviewQueueItem Item(string id, string code)
        => new(id, Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = $"case-{id}",
            SelfTrainingVsaCode = code,
            SelfTrainingSuggestedCode = code,
            SelfTrainingMeter = 1.5,
            SelfTrainingMatchLevel = "ProtocolStartdata"
        };
}
