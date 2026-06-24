using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderOverlayGeometry(OverlayGeometry overlay, bool isPreview, NormalizedPoint? labelAnchor = null)
    {
        _codingOverlayRenderController.RenderOverlayGeometry(
            overlay,
            isPreview,
            labelAnchor,
            _codingOverlayService?.Calibration);
    }
}
