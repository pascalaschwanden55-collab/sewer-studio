using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveIntrusionSchema(IntrusionSchema intrusion, DropShadowEffect glowEffect)
    {
        var overlay = BuildCodingSchemaGeometry();
        if (overlay == null)
            return;

        var stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        var fillBrush = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));

        CodingSchemaOverlayRenderer.AddPipeReference(
            CodingOverlayCanvas,
            intrusion.PipeCenter,
            intrusion.PipeRadius,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight,
            stroke,
            glowEffect,
            OverlayTags.Preview);

        var tip = CodingNormToPixel(intrusion.GetIntrusionTip());
        var edge = CodingNormToPixel(intrusion.GetEdgePoint());
        var (leftNorm, rightNorm) = intrusion.GetSpreadEdges();
        var left = CodingNormToPixel(leftNorm);
        var right = CodingNormToPixel(rightNorm);

        var tongue = new System.Windows.Shapes.Polygon
        {
            Stroke = stroke,
            StrokeThickness = 2.5,
            Fill = fillBrush,
            Effect = glowEffect,
            Tag = OverlayTags.Preview
        };
        tongue.Points.Add(left);
        tongue.Points.Add(tip);
        tongue.Points.Add(right);
        CodingOverlayCanvas.Children.Add(tongue);

        var spine = new System.Windows.Shapes.Line
        {
            X1 = edge.X,
            Y1 = edge.Y,
            X2 = tip.X,
            Y2 = tip.Y,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Effect = glowEffect,
            Tag = OverlayTags.Preview
        };
        CodingOverlayCanvas.Children.Add(spine);

        CodingOverlayDotMarkerRenderer.Add(CodingOverlayCanvas, tip, 7, stroke, OverlayTags.Preview, glowEffect);
        CodingOverlayDotMarkerRenderer.Add(CodingOverlayCanvas, edge, 5, Brushes.White, OverlayTags.Preview, glowEffect);
        CodingSchemaOverlayRenderer.AddLabel(
            CodingOverlayCanvas,
            tip,
            $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h",
            stroke,
            glowEffect,
            OverlayTags.Measure);
    }
}
