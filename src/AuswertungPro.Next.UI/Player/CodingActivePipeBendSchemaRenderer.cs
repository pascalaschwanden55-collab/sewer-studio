using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class CodingActivePipeBendSchemaRenderer
{
    public static bool Render(
        Canvas canvas,
        PipeBendSchema bend,
        OverlayGeometry? overlay,
        Effect? effect,
        Func<NormalizedPoint, Point> toPixel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(bend);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (overlay is not null)
        {
            CodingPipeBendOverlayRenderer.Render(
                canvas,
                overlay,
                isPreview: true,
                effect,
                OverlayTags.Preview,
                OverlayTags.Measure,
                toPixel);
        }

        var center = toPixel(bend.Center);
        var radiusHandle = toPixel(bend.GetRadiusHandle());

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
        canvas.Children.Add(guide);

        CodingOverlayDotMarkerRenderer.Add(canvas, radiusHandle, 5, Brushes.White, OverlayTags.Preview, effect);
        return true;
    }
}
