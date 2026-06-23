using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderSchemaPipeReference(
        NormalizedPoint centerNorm,
        double radiusNorm,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        var center = CodingNormToPixel(centerNorm);
        double rPx = radiusNorm * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);

        var pipe = new System.Windows.Shapes.Ellipse
        {
            Width = rPx * 2,
            Height = rPx * 2,
            Stroke = stroke,
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        Canvas.SetLeft(pipe, center.X - rPx);
        Canvas.SetTop(pipe, center.Y - rPx);
        CodingOverlayCanvas.Children.Add(pipe);
    }

    private void RenderReferenceDn()
    {
        ReferenceDnOverlayRenderer.Render(
            CodingOverlayCanvas,
            _codingOverlayService?.Calibration,
            _showReferenceDn,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight);
    }

    private void AddSchemaLabel(
        Point anchor,
        string text,
        Brush foreground,
        System.Windows.Media.Effects.DropShadowEffect glowEffect)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Background = new SolidColorBrush(Color.FromArgb(205, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = glowEffect,
            Tag = OverlayTags.Measure
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        CodingOverlayCanvas.Children.Add(label);
    }
}
