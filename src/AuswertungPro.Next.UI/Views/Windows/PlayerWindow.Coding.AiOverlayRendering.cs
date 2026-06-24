using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        _codingOverlayRenderController.RenderAiOverlays(
            _codingVm.Events,
            _codingOverlayService?.Calibration);
    }
}
