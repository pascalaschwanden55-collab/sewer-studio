using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataApprovalRequestFactoryTests
{
    [Fact]
    public async Task Create_uebernimmt_queue_auswahl_runtime_und_ui_callbacks()
    {
        var queue = new ReviewQueueService();
        var item = new ReviewQueueItem("item-1", Entry: null, Priority: 0.5, EnqueuedUtc: DateTime.UtcNow);
        var settings = new AppSettings();
        var token = new CancellationToken(canceled: false);
        var calls = new List<string>();

        var request = TrainingProtocolStartdataApprovalRequestFactory.Create(
            new TrainingProtocolStartdataApprovalRequestFactoryRequest(
                QueueService: queue,
                SelectItems: () =>
                {
                    calls.Add("select");
                    return [item];
                },
                Settings: settings,
                ApproveReviewItemAsync: (_, _, _, _, _, _) =>
                {
                    calls.Add("approve-review");
                    return Task.CompletedTask;
                },
                CancellationToken: token,
                Log: value => calls.Add($"log:{value}"),
                OnUi: action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                },
                SetReviewStatusText: value => calls.Add($"status:{value}")),
            ApproveWithRuntimeAsync: (actualItem, actualQueue, actualToken, actualSettings, approveAsync) =>
            {
                Assert.Same(item, actualItem);
                Assert.Same(queue, actualQueue);
                Assert.Equal(token, actualToken);
                Assert.Same(settings, actualSettings);
                Assert.NotNull(approveAsync);
                calls.Add("runtime");
                return Task.CompletedTask;
            });

        Assert.Same(queue, request.QueueService);
        Assert.Equal([item], request.SelectItems());
        await request.ApproveAsync(item, queue, token);
        Assert.Equal(token, request.CancellationToken);
        request.Log("fertig");
        request.OnUi(() => calls.Add("ui-action"));
        request.SetReviewStatusText("bereit");

        Assert.Equal(
            ["select", "runtime", "log:fertig", "ui-before", "ui-action", "ui-after", "status:bereit"],
            calls);
    }
}
