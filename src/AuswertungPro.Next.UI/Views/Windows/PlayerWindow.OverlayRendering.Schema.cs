using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

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
        var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == OverlayTags.RefDn)
            .ToList();
        foreach (var el in old)
            CodingOverlayCanvas.Children.Remove(el);

        if (!_showReferenceDn || _codingOverlayService?.Calibration == null) return;
        var cal = _codingOverlayService.Calibration;
        if (!cal.IsCalibrated || cal.NormalizedDiameter <= 0) return;

        double w = CodingOverlayCanvas.ActualWidth, h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var circleRect = ReferenceDnGeometry.BuildCircleRect(
            cal.PipeCenter,
            cal.NormalizedDiameter,
            w,
            h);
        if (circleRect.IsEmpty) return;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = circleRect.Width,
            Height = circleRect.Height,
            Stroke = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = OverlayTags.RefDn
        };
        Canvas.SetLeft(circle, circleRect.Left);
        Canvas.SetTop(circle, circleRect.Top);
        CodingOverlayCanvas.Children.Add(circle);

        var lbl = new TextBlock
        {
            Text = $"Ref: DN {cal.NominalDiameterMm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            Tag = OverlayTags.RefDn
        };
        Canvas.SetLeft(lbl, circleRect.Right + 4);
        Canvas.SetTop(lbl, circleRect.Top + circleRect.Height / 2.0 - 8);
        CodingOverlayCanvas.Children.Add(lbl);
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
