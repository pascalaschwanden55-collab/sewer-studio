using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewQueueReloadControllerTests
{
    [Fact]
    public void Reload_laed_queue_wenn_service_vorhanden_ist()
    {
        var queueService = new InfraSelfImproving.ReviewQueueService();
        InfraSelfImproving.ReviewQueueService? loadedService = null;

        TrainingReviewQueueReloadController.Reload(
            queueService,
            service => loadedService = service);

        Assert.Same(queueService, loadedService);
    }

    [Fact]
    public void Reload_ignoriert_fehlenden_queue_service()
    {
        var callCount = 0;

        TrainingReviewQueueReloadController.Reload(
            queueService: null,
            _ => callCount++);

        Assert.Equal(0, callCount);
    }
}
