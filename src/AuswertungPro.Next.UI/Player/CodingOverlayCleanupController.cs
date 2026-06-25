using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayCleanupController
{
    public static void ClearAiOverlays(Canvas canvas)
        => CodingOverlayCanvasCleaner.ClearAiOverlays(canvas);
}
