using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalyzedFrameTimestampPolicyTests
{
    [Fact]
    public void Resolve_uses_clean_frame_when_pending_is_missing()
    {
        Assert.Equal(2.5, CodingAnalyzedFrameTimestampPolicy.Resolve(null, firstCleanFrameSeconds: 2.5));
    }

    [Fact]
    public void Resolve_uses_clean_frame_when_pending_is_before_clean_frame()
    {
        Assert.Equal(2.5, CodingAnalyzedFrameTimestampPolicy.Resolve(pendingTimestampSeconds: 1.0, firstCleanFrameSeconds: 2.5));
    }

    [Fact]
    public void Resolve_keeps_pending_when_it_is_after_clean_frame()
    {
        Assert.Equal(3.0, CodingAnalyzedFrameTimestampPolicy.Resolve(pendingTimestampSeconds: 3.0, firstCleanFrameSeconds: 2.5));
    }

    [Fact]
    public void Resolve_keeps_pending_without_clean_frame()
    {
        Assert.Equal(3.0, CodingAnalyzedFrameTimestampPolicy.Resolve(pendingTimestampSeconds: 3.0, firstCleanFrameSeconds: null));
    }
}
