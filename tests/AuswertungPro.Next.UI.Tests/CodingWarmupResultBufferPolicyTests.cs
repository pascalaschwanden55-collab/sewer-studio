using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingWarmupResultBufferPolicyTests
{
    [Fact]
    public void Select_uses_current_result_when_no_pending_result_exists()
    {
        var current = Detection("current", hasFinding: true);

        var selection = CodingWarmupResultBufferPolicy.Select(current, pending: null);

        Assert.Same(current, selection.Result);
        Assert.False(selection.ShouldClearPending);
    }

    [Fact]
    public void Select_uses_pending_result_when_current_is_empty_and_pending_has_findings()
    {
        var current = Detection("current", hasFinding: false);
        var pending = Detection("pending", hasFinding: true);

        var selection = CodingWarmupResultBufferPolicy.Select(current, pending);

        Assert.Same(pending, selection.Result);
        Assert.True(selection.ShouldClearPending);
    }

    [Fact]
    public void Select_keeps_current_result_when_current_has_findings()
    {
        var current = Detection("current", hasFinding: true);
        var pending = Detection("pending", hasFinding: true);

        var selection = CodingWarmupResultBufferPolicy.Select(current, pending);

        Assert.Same(current, selection.Result);
        Assert.True(selection.ShouldClearPending);
    }

    [Fact]
    public void Select_keeps_current_result_when_pending_is_empty()
    {
        var current = Detection("current", hasFinding: false);
        var pending = Detection("pending", hasFinding: false);

        var selection = CodingWarmupResultBufferPolicy.Select(current, pending);

        Assert.Same(current, selection.Result);
        Assert.True(selection.ShouldClearPending);
    }

    private static LiveDetection Detection(string label, bool hasFinding)
        => new(
            TimestampSeconds: 1,
            Findings: hasFinding
                ? [new LiveFrameFinding(label, Severity: 1, PositionClock: null, ExtentPercent: null)]
                : [],
            MeterReading: null,
            Error: null);
}
