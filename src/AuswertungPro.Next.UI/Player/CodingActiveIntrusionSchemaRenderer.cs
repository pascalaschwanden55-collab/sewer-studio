using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class CodingActiveIntrusionSchemaRenderer
{
    public static bool Render(
        Canvas canvas,
        IntrusionSchema intrusion,
        OverlayGeometry? overlay,
        Effect? effect,
        Func<NormalizedPoint, Point> toPixel,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(intrusion);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (overlay is null)
            return false;

        var stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        var fillBrush = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));

        CodingSchemaOverlayRenderer.AddPipeReference(
            canvas,
            intrusion.PipeCenter,
            intrusion.PipeRadius,
            canvasWidth,
            canvasHeight,
            stroke,
            effect,
            OverlayTags.Preview);

        var tip = toPixel(intrusion.GetIntrusionTip());
        var edge = toPixel(intrusion.GetEdgePoint());
        var (leftNorm, rightNorm) = intrusion.GetSpreadEdges();
        var left = toPixel(leftNorm);
        var right = toPixel(rightNorm);

        var tongue = new System.Windows.Shapes.Polygon
        {
            Stroke = stroke,
            StrokeThickness = 2.5,
            Fill = fillBrush,
            Effect = effect,
            Tag = OverlayTags.Preview
        };
        tongue.Points.Add(left);
        tongue.Points.Add(tip);
        tongue.Points.Add(right);
        canvas.Children.Add(tongue);

        var spine = new System.Windows.Shapes.Line
        {
            X1 = edge.X,
            Y1 = edge.Y,
            X2 = tip.X,
            Y2 = tip.Y,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Effect = effect,
            Tag = OverlayTags.Preview
        };
        canvas.Children.Add(spine);

        CodingOverlayDotMarkerRenderer.Add(canvas, tip, 7, stroke, OverlayTags.Preview, effect);
        CodingOverlayDotMarkerRenderer.Add(canvas, edge, 5, Brushes.White, OverlayTags.Preview, effect);
        CodingSchemaOverlayRenderer.AddLabel(
            canvas,
            tip,
            $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h",
            stroke,
            effect,
            OverlayTags.Measure);

        return true;
    }
}
