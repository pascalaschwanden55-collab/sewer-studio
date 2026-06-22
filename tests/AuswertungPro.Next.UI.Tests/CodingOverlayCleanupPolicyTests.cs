using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayCleanupPolicyTests
{
    [Theory]
    [InlineData(OverlayTags.ToolBadge)]
    [InlineData(OverlayTags.Preview)]
    [InlineData(OverlayTags.Measure)]
    public void ShouldRemoveTransientTag_removes_always_transient_tags(string tag)
    {
        Assert.True(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(tag, clearManualOverlay: false));
        Assert.True(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(tag, clearManualOverlay: true));
    }

    [Fact]
    public void ShouldRemoveTransientTag_removes_manual_only_when_requested()
    {
        Assert.False(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(OverlayTags.Manual, clearManualOverlay: false));
        Assert.True(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(OverlayTags.Manual, clearManualOverlay: true));
    }

    [Theory]
    [InlineData(OverlayTags.RefDn)]
    [InlineData(OverlayTags.AiOverlay)]
    [InlineData(OverlayTags.BendMarker)]
    [InlineData("other")]
    public void ShouldRemoveTransientTag_keeps_non_transient_tags(string tag)
    {
        Assert.False(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(tag, clearManualOverlay: true));
    }

    [Fact]
    public void ShouldRemoveTransientTag_keeps_non_string_tags()
    {
        Assert.False(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(null, clearManualOverlay: true));
        Assert.False(CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(123, clearManualOverlay: true));
    }
}
