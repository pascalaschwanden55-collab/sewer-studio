using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveIntrusionSchema(IntrusionSchema intrusion, DropShadowEffect glowEffect)
    {
        var overlay = BuildCodingSchemaGeometry();
        CodingActiveIntrusionSchemaRenderer.Render(
            CodingOverlayCanvas,
            intrusion,
            overlay,
            glowEffect,
            CodingNormToPixel,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight);
    }
}
