using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveFillLevelSchema(FillLevelSchema fill, DropShadowEffect glowEffect)
    {
        var overlay = BuildCodingSchemaGeometry();
        CodingActiveFillLevelSchemaRenderer.Render(
            CodingOverlayCanvas,
            fill,
            overlay,
            glowEffect,
            CodingNormToPixel,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight);
    }
}
