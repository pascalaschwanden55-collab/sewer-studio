using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOptionsTests
{
    [Fact]
    public void FromSettings_maps_video_settings()
    {
        var settings = new AppSettings
        {
            VideoHwDecoding = false,
            VideoDropLateFrames = false,
            VideoSkipFrames = false,
            VideoFileCachingMs = 1234,
            VideoNetworkCachingMs = 2345,
            VideoCodecThreads = 6,
            VideoOutput = "direct3d9"
        };

        var options = PlayerWindowOptions.FromSettings(settings);

        Assert.False(options.EnableHardwareDecoding);
        Assert.False(options.DropLateFrames);
        Assert.False(options.SkipFrames);
        Assert.Equal(1234, options.FileCachingMs);
        Assert.Equal(2345, options.NetworkCachingMs);
        Assert.Equal(6, options.CodecThreads);
        Assert.Equal("direct3d9", options.VideoOutput);
    }
}
