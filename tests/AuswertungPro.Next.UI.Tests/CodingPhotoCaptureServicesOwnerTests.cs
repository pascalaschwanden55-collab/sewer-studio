using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoCaptureServicesOwnerTests
{
    [Fact]
    public void Services_are_created_lazily_and_reused()
    {
        var createCount = 0;
        var services = new CodingPhotoCaptureServices(
            createFrameExtractionService: () => new CodingFrameExtractionService(),
            createSnapshotFileCaptureService: () => new CodingSnapshotFileCaptureService());
        var owner = new CodingPhotoCaptureServicesOwner(() =>
        {
            createCount++;
            return services;
        });

        var first = owner.Services;
        var second = owner.Services;

        Assert.Same(services, first);
        Assert.Same(first, second);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void Exposes_lazy_capture_services()
    {
        var frameService = new CodingFrameExtractionService();
        var snapshotService = new CodingSnapshotFileCaptureService();
        var owner = new CodingPhotoCaptureServicesOwner(() => new CodingPhotoCaptureServices(
            createFrameExtractionService: () => frameService,
            createSnapshotFileCaptureService: () => snapshotService));

        Assert.Same(frameService, owner.FrameExtractionService);
        Assert.Same(snapshotService, owner.SnapshotFileCaptureService);
    }

    [Fact]
    public void Constructor_throws_for_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CodingPhotoCaptureServicesOwner(null!));
    }
}
