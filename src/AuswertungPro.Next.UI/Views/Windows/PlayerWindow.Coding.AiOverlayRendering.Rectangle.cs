using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderAiRectangleOverlay(
        CodingEvent ev,
        OverlayGeometry geo,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect aiGlow,
        double w,
        double h)
    {
        if (geo.Points.Count < 4 || ev.AiContext == null) return;

        double rx = geo.Points[0].X * w;
        double ry = geo.Points[0].Y * h;
        double rw = (geo.Points[2].X - geo.Points[0].X) * w;
        double rh = (geo.Points[2].Y - geo.Points[0].Y) * h;
        var rectLeft = Math.Min(rx, rx + rw);
        var rectTop = Math.Min(ry, ry + rh);
        var rectAbsW = Math.Abs(rw);
        var rectAbsH = Math.Abs(rh);

        // Farbige Kontur mit halbtransparenter Fuellung
        var fillColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext.Decision);
        var rect = new Rectangle
        {
            Width = rectAbsW,
            Height = rectAbsH,
            Stroke = stroke,
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(30, fillColor.R, fillColor.G, fillColor.B)),
            RadiusX = 6,
            RadiusY = 6,
            Tag = OverlayTags.AiOverlay,
            Effect = aiGlow
        };
        Canvas.SetLeft(rect, rectLeft);
        Canvas.SetTop(rect, rectTop);
        CodingOverlayCanvas.Children.Add(rect);

        // Label-Badge: Code [Konfidenz%]
        var labelText = CodingAiOverlayDisplayPolicy.LabelText(ev.Entry.Code, ev.AiContext.Confidence);
        var labelBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, fillColor.R, fillColor.G, fillColor.B)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Tag = OverlayTags.AiOverlay,
            Effect = aiGlow,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = labelText,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }
        };
        labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var lx = Math.Clamp(rectLeft, 2, w - labelBorder.DesiredSize.Width - 2);
        var ly = Math.Clamp(rectTop - labelBorder.DesiredSize.Height - 4, 2, h - labelBorder.DesiredSize.Height - 2);
        Canvas.SetLeft(labelBorder, lx);
        Canvas.SetTop(labelBorder, ly);
        CodingOverlayCanvas.Children.Add(labelBorder);
    }
}
