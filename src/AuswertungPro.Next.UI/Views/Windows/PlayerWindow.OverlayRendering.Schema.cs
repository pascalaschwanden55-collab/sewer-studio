using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderReferenceDn()
    {
        ReferenceDnOverlayRenderer.Render(
            CodingOverlayCanvas,
            _codingOverlayService?.Calibration,
            _showReferenceDn,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight);
    }
}
