using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoCaptureServicesTests
{
    [Fact]
    public void Services_are_created_lazily_and_reused()
    {
        var frameCreateCount = 0;
        var snapshotCreateCount = 0;

        var services = new CodingPhotoCaptureServices(
            createFrameExtractionService: () =>
            {
                frameCreateCount++;
                return new CodingFrameExtractionService();
            },
            createSnapshotFileCaptureService: () =>
            {
                snapshotCreateCount++;
                return new CodingSnapshotFileCaptureService();
            });

        var firstFrame = services.FrameExtractionService;
        var secondFrame = services.FrameExtractionService;
        var firstSnapshot = services.SnapshotFileCaptureService;
        var secondSnapshot = services.SnapshotFileCaptureService;

        Assert.Same(firstFrame, secondFrame);
        Assert.Same(firstSnapshot, secondSnapshot);
        Assert.Equal(1, frameCreateCount);
        Assert.Equal(1, snapshotCreateCount);
    }
}
