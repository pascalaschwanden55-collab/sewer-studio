using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderPipeBendOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        Brush defaultStroke,
        DropShadowEffect glowEffect,
        string tag,
        NormalizedPoint? labelAnchor)
    {
        CodingPipeBendOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            isPreview,
            glowEffect,
            tag,
            isPreview ? OverlayTags.Measure : OverlayTags.Manual,
            CodingNormToPixel);
    }
}
