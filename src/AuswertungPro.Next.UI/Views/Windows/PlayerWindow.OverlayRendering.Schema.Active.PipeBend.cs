using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActivePipeBendSchema(PipeBendSchema bend, DropShadowEffect glowEffect)
    {
        var overlay = BuildCodingSchemaGeometry();
        if (overlay != null)
            RenderPipeBendOverlay(overlay, true, Brushes.Gold, glowEffect, OverlayTags.Preview, bend.Center);

        var center = CodingNormToPixel(bend.Center);
        var radiusHandle = CodingNormToPixel(bend.GetRadiusHandle());

        var guide = new System.Windows.Shapes.Line
        {
            X1 = center.X,
            Y1 = center.Y,
            X2 = radiusHandle.X,
            Y2 = radiusHandle.Y,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 184, 0)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Tag = OverlayTags.Preview
        };
        CodingOverlayCanvas.Children.Add(guide);

        CodingOverlayDotMarkerRenderer.Add(CodingOverlayCanvas, radiusHandle, 5, Brushes.White, OverlayTags.Preview, glowEffect);
    }
}
