using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActivePipeBendSchema(PipeBendSchema bend, DropShadowEffect glowEffect)
    {
        var overlay = BuildCodingSchemaGeometry();
        CodingActivePipeBendSchemaRenderer.Render(
            CodingOverlayCanvas,
            bend,
            overlay,
            glowEffect,
            CodingNormToPixel);
    }
}
