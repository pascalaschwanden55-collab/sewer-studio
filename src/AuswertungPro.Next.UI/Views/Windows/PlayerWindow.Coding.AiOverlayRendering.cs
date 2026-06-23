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
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        // Bestehende KI-Overlays entfernen (Tags beginnen mit "ai_")
        var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s.StartsWith(OverlayTags.AiPrefix))
            .ToList();
        foreach (var el in toRemove)
            CodingOverlayCanvas.Children.Remove(el);

        var aiGlow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (var ev in _codingVm.Events)
        {
            if (ev.Overlay == null || ev.AiContext == null) continue;
            var geo = ev.Overlay;

            var strokeColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext.Decision);
            Brush stroke = new SolidColorBrush(strokeColor);

            switch (geo.ToolType)
            {
                case OverlayToolType.Line:
                case OverlayToolType.Stretch:
                    if (geo.Points.Count >= 2)
                    {
                        var line = new System.Windows.Shapes.Line
                        {
                            X1 = geo.Points[0].X * w,
                            Y1 = geo.Points[0].Y * h,
                            X2 = geo.Points[1].X * w,
                            Y2 = geo.Points[1].Y * h,
                            Stroke = stroke,
                            StrokeThickness = 2.5,
                            StrokeDashArray = new DoubleCollection { 5, 3 },
                            Tag = OverlayTags.AiOverlay,
                            Effect = aiGlow
                        };
                        CodingOverlayCanvas.Children.Add(line);
                    }
                    break;

                case OverlayToolType.Rectangle:
                    RenderAiRectangleOverlay(ev, geo, stroke, aiGlow, w, h);
                    break;

                case OverlayToolType.Point:
                    if (geo.Points.Count >= 1)
                    {
                        double px = geo.Points[0].X * w;
                        double py = geo.Points[0].Y * h;
                        var dot = new System.Windows.Shapes.Ellipse
                        {
                            Width = 14,
                            Height = 14,
                            Fill = stroke,
                            Opacity = 0.8,
                            Stroke = Brushes.White,
                            StrokeThickness = 1.5,
                            Tag = OverlayTags.AiOverlay,
                            Effect = aiGlow
                        };
                        Canvas.SetLeft(dot, px - 7);
                        Canvas.SetTop(dot, py - 7);
                        CodingOverlayCanvas.Children.Add(dot);
                    }
                    break;

                case OverlayToolType.Arc:
                    if (geo.Points.Count >= 2)
                    {
                        var arc = CreateArcPath(geo.Points[0], geo.Points[1], stroke, aiGlow, OverlayTags.AiOverlay, dashed: true);
                        if (arc != null)
                            CodingOverlayCanvas.Children.Add(arc);
                    }
                    break;

                case OverlayToolType.PipeBend:
                    RenderPipeBendOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;
            }
        }
    }
}
