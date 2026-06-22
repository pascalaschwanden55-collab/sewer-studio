namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayCleanupPolicy
{
    public static bool ShouldRemoveTransientTag(object? tag, bool clearManualOverlay)
    {
        return tag is string tagValue
               && (tagValue == OverlayTags.ToolBadge
                   || tagValue == OverlayTags.Preview
                   || tagValue == OverlayTags.Measure
                   || (clearManualOverlay && tagValue == OverlayTags.Manual));
    }
}
