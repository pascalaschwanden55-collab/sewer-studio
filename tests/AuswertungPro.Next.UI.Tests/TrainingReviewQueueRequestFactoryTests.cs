using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewQueueRequestFactoryTests
{
    [Fact]
    public void LoadFactory_verdrahtet_review_queue_load_request()
    {
        var calls = new List<string>();
        var service = new InfraSelfImproving.ReviewQueueService();
        var queue = new ObservableCollection<InfraSelfImproving.ReviewQueueItem>();

        var request = TrainingReviewQueueLoadRequestFactory.Create(
            new TrainingReviewQueueLoadRequestFactoryRequest(
                QueueService: service,
                ReviewQueue: queue,
                SetReviewQueueCount: value => calls.Add("count:" + value),
                SetReviewStatusText: value => calls.Add("status:" + value),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Same(service, request.QueueService);
        Assert.Same(queue, request.ReviewQueue);
        request.OnUi(() =>
        {
            request.SetReviewQueueCount(2);
            request.SetReviewStatusText("ok");
        });

        Assert.Equal(["on-ui", "count:2", "status:ok"], calls);
    }
}
