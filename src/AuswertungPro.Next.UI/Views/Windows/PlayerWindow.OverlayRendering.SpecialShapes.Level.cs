using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderLevelOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        DropShadowEffect glowEffect,
        string tag)
    {
        CodingLevelOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            isPreview,
            glowEffect,
            tag,
            CodingNormToPixel,
            _codingOverlayService?.Calibration,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight);
    }
}
