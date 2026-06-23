using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingAiRectangleOverlayRenderStyle(
    Brush Stroke,
    Color FillBaseColor,
    Effect? Effect,
    string Tag);

public static class CodingAiRectangleOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        double canvasWidth,
        double canvasHeight,
        string? code,
        double? confidence,
        CodingAiRectangleOverlayRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(style);

        if (overlay.ToolType != OverlayToolType.Rectangle || overlay.Points.Count < 4)
            return false;

        var rx = overlay.Points[0].X * canvasWidth;
        var ry = overlay.Points[0].Y * canvasHeight;
        var rw = (overlay.Points[2].X - overlay.Points[0].X) * canvasWidth;
        var rh = (overlay.Points[2].Y - overlay.Points[0].Y) * canvasHeight;
        var rectLeft = Math.Min(rx, rx + rw);
        var rectTop = Math.Min(ry, ry + rh);
        var rectAbsW = Math.Abs(rw);
        var rectAbsH = Math.Abs(rh);

        var rect = new Rectangle
        {
            Width = rectAbsW,
            Height = rectAbsH,
            Stroke = style.Stroke,
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(
                30,
                style.FillBaseColor.R,
                style.FillBaseColor.G,
                style.FillBaseColor.B)),
            RadiusX = 6,
            RadiusY = 6,
            Tag = style.Tag,
            Effect = style.Effect
        };
        Canvas.SetLeft(rect, rectLeft);
        Canvas.SetTop(rect, rectTop);
        canvas.Children.Add(rect);

        var labelBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(
                210,
                style.FillBaseColor.R,
                style.FillBaseColor.G,
                style.FillBaseColor.B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Tag = style.Tag,
            Effect = style.Effect,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = CodingAiOverlayDisplayPolicy.LabelText(code, confidence),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }
        };
        labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var lx = Math.Clamp(rectLeft, 2, canvasWidth - labelBorder.DesiredSize.Width - 2);
        var ly = Math.Clamp(rectTop - labelBorder.DesiredSize.Height - 4, 2, canvasHeight - labelBorder.DesiredSize.Height - 2);
        Canvas.SetLeft(labelBorder, lx);
        Canvas.SetTop(labelBorder, ly);
        canvas.Children.Add(labelBorder);

        return true;
    }
}
