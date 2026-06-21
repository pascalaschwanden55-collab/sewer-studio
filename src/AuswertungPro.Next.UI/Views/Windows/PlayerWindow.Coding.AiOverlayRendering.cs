using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        // Bestehende KI-Overlays entfernen (Tags beginnen mit "ai_")
        var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s.StartsWith("ai_"))
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
                            Tag = "ai_overlay",
                            Effect = aiGlow
                        };
                        CodingOverlayCanvas.Children.Add(line);
                    }
                    break;

                case OverlayToolType.Rectangle:
                    if (geo.Points.Count >= 4)
                    {
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
                            Tag = "ai_overlay",
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
                            Tag = "ai_overlay",
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
                            Tag = "ai_overlay",
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
                        var arc = CreateArcPath(geo.Points[0], geo.Points[1], stroke, aiGlow, "ai_overlay", dashed: true);
                        if (arc != null)
                            CodingOverlayCanvas.Children.Add(arc);
                    }
                    break;

                case OverlayToolType.PipeBend:
                    RenderPipeBendOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, "ai_overlay", null);
                    break;
            }
        }
    }
}
