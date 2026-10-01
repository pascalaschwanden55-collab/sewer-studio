using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerVideoAspectResolverTests
{
    [Fact]
    public void TryResolve_returns_native_video_aspect()
    {
        uint? requestedVideoNumber = null;
        var resolved = PlayerVideoAspectResolver.TryResolve(
            (uint videoNumber, ref uint width, ref uint height) =>
            {
                requestedVideoNumber = videoNumber;
                width = 1440;
                height = 1080;
                return true;
            },
            out var aspect);

        Assert.True(resolved);
        Assert.Equal(0u, requestedVideoNumber);
        Assert.Equal(4d / 3d, aspect, precision: 12);
    }

    [Theory]
    [InlineData(16u, 15u, false, 4d / 3d)]
    [InlineData(64u, 45u, false, 16d / 9d)]
    [InlineData(16u, 15u, true, 3d / 4d)]
    public void TryResolve_applies_sample_aspect_ratio_and_rotation(
        uint sarNumerator,
        uint sarDenominator,
        bool swapAxes,
        double expectedAspect)
    {
        var resolved = PlayerVideoAspectResolver.TryResolve(
            (uint _, ref uint width, ref uint height) =>
            {
                width = 720;
                height = 576;
                return true;
            },
            new PlayerVideoAspectMetadata(sarNumerator, sarDenominator, swapAxes),
            out var aspect);

        Assert.True(resolved);
        Assert.Equal(expectedAspect, aspect, precision: 12);
    }

    [Theory]
    [InlineData(false, 1440u, 1080u)]
    [InlineData(true, 1440u, 0u)]
    [InlineData(true, 0u, 1080u)]
    public void TryResolve_rejects_unavailable_or_invalid_video_size(
        bool available,
        uint width,
        uint height)
    {
        var resolved = PlayerVideoAspectResolver.TryResolve(
            (uint _, ref uint resolvedWidth, ref uint resolvedHeight) =>
            {
                resolvedWidth = width;
                resolvedHeight = height;
                return available;
            },
            out var aspect);

        Assert.False(resolved);
        Assert.Equal(0, aspect);
    }

    [Fact]
    public void TryResolve_keeps_optional_metadata_failure_out_of_the_ui_flow()
    {
        var resolved = PlayerVideoAspectResolver.TryResolve(
            (uint _, ref uint _, ref uint _) =>
                throw new InvalidOperationException("video metadata unavailable"),
            out var aspect);

        Assert.False(resolved);
        Assert.Equal(0, aspect);
    }
}
