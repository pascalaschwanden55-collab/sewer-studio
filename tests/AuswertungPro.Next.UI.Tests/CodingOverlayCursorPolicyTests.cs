using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayCursorPolicyTests
{
    [Fact]
    public void ShouldUseCrossCursor_returns_false_when_overlay_is_closed()
    {
        Assert.False(CodingOverlayCursorPolicy.ShouldUseCrossCursor(
            isOverlayOpen: false,
            isCalibrating: true,
            activeTool: OverlayToolType.Rectangle));
    }

    [Fact]
    public void ShouldUseCrossCursor_returns_true_while_calibrating()
    {
        Assert.True(CodingOverlayCursorPolicy.ShouldUseCrossCursor(
            isOverlayOpen: true,
            isCalibrating: true,
            activeTool: OverlayToolType.None));
    }

    [Theory]
    [InlineData(OverlayToolType.Rectangle, true)]
    [InlineData(OverlayToolType.Point, true)]
    [InlineData(OverlayToolType.None, false)]
    public void ShouldUseCrossCursor_depends_on_active_tool_when_not_calibrating(
        OverlayToolType activeTool,
        bool expected)
    {
        Assert.Equal(expected, CodingOverlayCursorPolicy.ShouldUseCrossCursor(
            isOverlayOpen: true,
            isCalibrating: false,
            activeTool));
    }
}
