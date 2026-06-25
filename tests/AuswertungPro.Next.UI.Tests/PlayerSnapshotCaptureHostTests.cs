using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotCaptureHostTests
{
    [Fact]
    public void Host_forwards_snapshot_capture()
    {
        string? pathSeen = null;
        uint? widthSeen = null;
        uint? heightSeen = null;
        var host = new PlayerSnapshotCaptureHost((path, width, height) =>
        {
            pathSeen = path;
            widthSeen = width;
            heightSeen = height;
            return true;
        });

        var result = host.TakeSnapshot("frame.png", 640, 480);

        Assert.True(result);
        Assert.Equal("frame.png", pathSeen);
        Assert.Equal(640u, widthSeen);
        Assert.Equal(480u, heightSeen);
    }
}
