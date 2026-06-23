using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotFileCaptureServiceFactoryTests
{
    [Fact]
    public void Create_returns_snapshot_file_capture_service()
    {
        var service = PlayerSnapshotFileCaptureServiceFactory.Create();

        Assert.IsType<PlayerSnapshotFileCaptureService>(service);
    }
}
