using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderOverlayGeometry(OverlayGeometry overlay, bool isPreview, NormalizedPoint? labelAnchor = null)
    {
        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        string tag = isPreview ? OverlayTags.Preview : OverlayTags.Manual;
        var stroke = isPreview
            ? Brushes.Lime
            : new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
        var fill = isPreview
            ? new SolidColorBrush(Color.FromArgb(50, 0x00, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(35, 0x00, 0xE5, 0xFF));
        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        switch (overlay.ToolType)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
                RenderLineOverlay(overlay, isPreview, stroke, glowEffect, tag);
                break;

            case OverlayToolType.Rectangle:
                RenderRectangleOverlay(overlay, isPreview, stroke, fill, glowEffect, tag);
                break;

            case OverlayToolType.Point:
                RenderPointOverlay(overlay, stroke, glowEffect, tag);
                break;

            case OverlayToolType.Arc:
                if (overlay.Points.Count >= 2)
                {
                    var arc = CreateArcPath(overlay.Points[0], overlay.Points[1], stroke, glowEffect, tag, isPreview);
                    if (arc != null)
                        CodingOverlayCanvas.Children.Add(arc);
                }
                break;

            case OverlayToolType.PipeBend:
                RenderPipeBendOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.LateralCircle:
                RenderLateralCircleOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ruler:
                RenderRulerOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Level:
                RenderLevelOverlay(overlay, isPreview, glowEffect, tag);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ellipse:
                RenderEllipseOverlay(overlay, isPreview, glowEffect, tag);
                break;

            case OverlayToolType.Freehand:
                if (overlay.Points.Count >= 3)
                {
                    // Geschlossenes Polygon (nicht offene Polyline) â€” umschliesst den Schadensbereich
                    var poly = new System.Windows.Shapes.Polygon
                    {
                        Stroke = isPreview ? Brushes.HotPink : new SolidColorBrush(Color.FromRgb(255, 105, 180)),
                        StrokeThickness = isPreview ? 2 : 2.5,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = new SolidColorBrush(Color.FromArgb(25, 255, 105, 180)), // Leicht gefuellt
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        poly.StrokeDashArray = new DoubleCollection { 3, 2 };
                    foreach (var pt in overlay.Points)
                    {
                        var px = CodingNormToPixel(pt);
                        poly.Points.Add(new Point(px.X, px.Y));
                    }
                    CodingOverlayCanvas.Children.Add(poly);
                }
                break;
        }

        var text = CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(overlay);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var anchorNorm = labelAnchor ?? overlay.Points.LastOrDefault() ?? new NormalizedPoint(0.5, 0.5);
            var anchor = CodingNormToPixel(anchorNorm);

            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(5, 2, 5, 2),
                Effect = glowEffect,
                Tag = isPreview ? OverlayTags.Measure : OverlayTags.Manual
            };
            Canvas.SetLeft(label, anchor.X + 12);
            Canvas.SetTop(label, anchor.Y - 20);
            CodingOverlayCanvas.Children.Add(label);
        }
    }

}
