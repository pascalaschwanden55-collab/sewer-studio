using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSnapshotFileCaptureServiceFactoryTests
{
    [Fact]
    public void Create_returns_snapshot_file_capture_service()
    {
        var service = CodingSnapshotFileCaptureServiceFactory.Create();

        Assert.IsType<CodingSnapshotFileCaptureService>(service);
    }
}
