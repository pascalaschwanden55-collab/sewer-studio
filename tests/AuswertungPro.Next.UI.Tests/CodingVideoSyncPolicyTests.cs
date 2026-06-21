using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingVideoSyncPolicyTests
{
    [Fact]
    public void TryResolveTargetTimeMs_maps_meter_ratio_to_video_time()
    {
        var ok = CodingVideoSyncPolicy.TryResolveTargetTimeMs(
            currentMeter: 25,
            endMeter: 100,
            playerLengthMs: 10_000,
            out var target);

        Assert.True(ok);
        Assert.Equal(2_500, target);
    }

    [Fact]
    public void TryResolveTargetTimeMs_clamps_before_start()
    {
        var ok = CodingVideoSyncPolicy.TryResolveTargetTimeMs(-10, 100, 10_000, out var target);

        Assert.True(ok);
        Assert.Equal(0, target);
    }

    [Fact]
    public void TryResolveTargetTimeMs_clamps_after_end()
    {
        var ok = CodingVideoSyncPolicy.TryResolveTargetTimeMs(120, 100, 10_000, out var target);

        Assert.True(ok);
        Assert.Equal(10_000, target);
    }

    [Theory]
    [InlineData(0, 10_000)]
    [InlineData(-1, 10_000)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void TryResolveTargetTimeMs_returns_false_for_unusable_inputs(
        double endMeter,
        long playerLengthMs)
    {
        var ok = CodingVideoSyncPolicy.TryResolveTargetTimeMs(10, endMeter, playerLengthMs, out var target);

        Assert.False(ok);
        Assert.Equal(0, target);
    }
}
