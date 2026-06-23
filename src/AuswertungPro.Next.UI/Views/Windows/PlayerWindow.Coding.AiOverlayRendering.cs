using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        CodingAiOverlayRenderer.Render(
            CodingOverlayCanvas,
            _codingVm.Events,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight,
            _codingOverlayService?.Calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5),
            CodingNormToPixel);
    }
}
