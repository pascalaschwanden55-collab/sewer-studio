using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOverlayCursorPolicy
{
    public static bool ShouldUseCrossCursor(
        bool isOverlayOpen,
        bool isCalibrating,
        OverlayToolType activeTool)
    {
        return isOverlayOpen
               && (isCalibrating || activeTool != OverlayToolType.None);
    }
}
