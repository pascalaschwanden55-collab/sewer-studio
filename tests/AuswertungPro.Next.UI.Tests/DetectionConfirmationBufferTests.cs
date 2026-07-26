using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DetectionConfirmationBufferTests
{
    [Fact]
    public void StoreFindings_copies_findings_and_exposes_pending_state()
    {
        var buffer = new DetectionConfirmationBuffer();
        var findings = new List<LiveFrameFinding>
        {
            Finding("Riss", severity: 3)
        };
        var frame = new byte[] { 1, 2, 3 };

        buffer.StoreFindings(findings, frame, timestampSeconds: 12.5);
        findings.Clear();

        Assert.True(buffer.HasFindings);
        Assert.Equal(12.5, buffer.TimestampSeconds);
        Assert.Same(frame, buffer.FrameBytes);
        Assert.Single(buffer.Findings);
        Assert.Equal("Riss", buffer.Findings[0].Label);
    }

    [Fact]
    public void StoreAnalyzedFrame_updates_frame_and_timestamp_without_touching_findings()
    {
        var buffer = new DetectionConfirmationBuffer();
        buffer.StoreFindings([Finding("Wurzel", severity: 4)], [9], timestampSeconds: 3);

        buffer.StoreAnalyzedFrame([4, 5], timestampSeconds: 8.25);

        Assert.True(buffer.HasFindings);
        Assert.Equal(8.25, buffer.TimestampSeconds);
        Assert.Equal([4, 5], buffer.FrameBytes);
        Assert.Single(buffer.Findings);
    }

    [Fact]
    public void Clear_removes_findings_frame_and_timestamp()
    {
        var buffer = new DetectionConfirmationBuffer();
        buffer.StoreFindings([Finding("Riss", severity: 3)], [1], timestampSeconds: 2);

        buffer.Clear();

        Assert.False(buffer.HasFindings);
        Assert.Empty(buffer.Findings);
        Assert.Null(buffer.FrameBytes);
        Assert.Null(buffer.TimestampSeconds);
    }

    private static LiveFrameFinding Finding(string label, int severity)
        => new(label, severity, null, null, null, null, null, null);
}
