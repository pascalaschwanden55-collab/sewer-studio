using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLibVlcArgumentsTests
{
    [Fact]
    public void Build_maps_default_player_options_to_vlc_arguments()
    {
        var args = PlayerLibVlcArguments.Build(PlayerWindowOptions.Default);

        Assert.Contains("--vout=direct3d11", args);
        Assert.Contains("--avcodec-hw=dxva2", args);
        Assert.Contains("--avcodec-threads=4", args);
        Assert.Contains("--file-caching=3000", args);
        Assert.Contains("--network-caching=3000", args);
        Assert.Contains("--drop-late-frames", args);
        Assert.Contains("--skip-frames", args);
        Assert.Contains("--clock-jitter=0", args);
        Assert.Contains("--clock-synchro=0", args);
        Assert.Contains("--no-snapshot-preview", args);
    }

    [Fact]
    public void Build_omits_video_output_when_any_is_selected()
    {
        var options = PlayerWindowOptions.Default with
        {
            EnableHardwareDecoding = false,
            DropLateFrames = false,
            SkipFrames = false,
            CodecThreads = 2,
            FileCachingMs = 1000,
            NetworkCachingMs = 2000,
            VideoOutput = "any"
        };

        var args = PlayerLibVlcArguments.Build(options);

        Assert.Equal(
            new[]
            {
                "--avcodec-hw=none",
                "--avcodec-threads=2",
                "--file-caching=1000",
                "--network-caching=2000",
                "--clock-jitter=0",
                "--clock-synchro=0",
                "--no-snapshot-preview"
            },
            args);
    }
}
