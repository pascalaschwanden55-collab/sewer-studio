using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingAiRectangleOverlayRenderStyle(
    Brush Stroke,
    Color FillBaseColor,
    Effect? Effect,
    string Tag);

public static class CodingAiRectangleOverlayRenderer
{
    /// <summary>
    /// Zeichnet eine KI-Box mit Beschriftung. Die Ecken gehen durch <paramref name="toPixel"/>,
    /// der das tatsaechlich sichtbare Videorechteck kennt; <paramref name="canvasWidth"/> und
    /// <paramref name="canvasHeight"/> begrenzen nur die Lage der Beschriftung.
    /// </summary>
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        Func<NormalizedPoint, Point> toPixel,
        double canvasWidth,
        double canvasHeight,
        string? code,
        double? confidence,
        CodingAiRectangleOverlayRenderStyle style)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);
        ArgumentNullException.ThrowIfNull(style);

        if (overlay.ToolType != OverlayToolType.Rectangle || overlay.Points.Count < 4)
            return false;

        var corner = toPixel(overlay.Points[0]);
        var opposite = toPixel(overlay.Points[2]);
        var rectLeft = Math.Min(corner.X, opposite.X);
        var rectTop = Math.Min(corner.Y, opposite.Y);
        var rectAbsW = Math.Abs(opposite.X - corner.X);
        var rectAbsH = Math.Abs(opposite.Y - corner.Y);

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
        var labelWidth = labelBorder.DesiredSize.Width;
        var labelHeight = labelBorder.DesiredSize.Height;

        // Passt die Beschriftung nicht in die Flaeche (z. B. vor dem ersten Layout), bleibt
        // nur die Box. Sonst waere die Obergrenze fuer die Lage kleiner als die Untergrenze.
        if (labelWidth + 4 > canvasWidth || labelHeight + 4 > canvasHeight)
            return true;

        var lx = Math.Clamp(rectLeft, 2, canvasWidth - labelWidth - 2);
        var ly = Math.Clamp(rectTop - labelHeight - 4, 2, canvasHeight - labelHeight - 2);
        Canvas.SetLeft(labelBorder, lx);
        Canvas.SetTop(labelBorder, ly);
        canvas.Children.Add(labelBorder);

        return true;
    }
}
