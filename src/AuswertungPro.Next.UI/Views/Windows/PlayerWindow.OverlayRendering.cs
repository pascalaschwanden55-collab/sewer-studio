using System;
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
                if (overlay.Points.Count >= 2)
                {
                    var p1 = CodingNormToPixel(overlay.Points[0]);
                    var p2 = CodingNormToPixel(overlay.Points[1]);
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = p1.X,
                        Y1 = p1.Y,
                        X2 = p2.X,
                        Y2 = p2.Y,
                        Stroke = stroke,
                        StrokeThickness = 3,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        line.StrokeDashArray = new DoubleCollection { 4, 2 };
                    CodingOverlayCanvas.Children.Add(line);
                }
                break;

            case OverlayToolType.Rectangle:
                if (overlay.Points.Count >= 4)
                {
                    // Ueber das sichtbare Video-Rechteck rechnen (Letterbox-bewusst), nicht volle Flaeche.
                    var pix = overlay.Points.Select(CodingNormToPixel).ToList();
                    double minX = pix.Min(p => p.X);
                    double maxX = pix.Max(p => p.X);
                    double minY = pix.Min(p => p.Y);
                    double maxY = pix.Max(p => p.Y);

                    var rect = new Rectangle
                    {
                        Width = Math.Max(1, maxX - minX),
                        Height = Math.Max(1, maxY - minY),
                        Stroke = stroke,
                        StrokeThickness = 3,
                        Fill = fill,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        rect.StrokeDashArray = new DoubleCollection { 4, 2 };

                    Canvas.SetLeft(rect, minX);
                    Canvas.SetTop(rect, minY);
                    CodingOverlayCanvas.Children.Add(rect);
                }
                break;

            case OverlayToolType.Point:
                if (overlay.Points.Count >= 1)
                {
                    var p = CodingNormToPixel(overlay.Points[0]);
                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = stroke,
                        Stroke = Brushes.White,
                        StrokeThickness = 2,
                        Effect = glowEffect,
                        Tag = tag
                    };
                    Canvas.SetLeft(dot, p.X - 8);
                    Canvas.SetTop(dot, p.Y - 8);
                    CodingOverlayCanvas.Children.Add(dot);
                }
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
                if (overlay.Points.Count >= 2)
                {
                    var ep1 = CodingNormToPixel(overlay.Points[0]);
                    var ep2 = CodingNormToPixel(overlay.Points[1]);
                    var elli = new System.Windows.Shapes.Ellipse
                    {
                        Width = Math.Max(1, Math.Abs(ep2.X - ep1.X)),
                        Height = Math.Max(1, Math.Abs(ep2.Y - ep1.Y)),
                        Stroke = isPreview ? Brushes.MediumPurple : new SolidColorBrush(Color.FromRgb(147, 112, 219)),
                        StrokeThickness = isPreview ? 2 : 2.5,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
                        Effect = glowEffect,
                        Tag = tag
                    };
                    if (isPreview)
                        elli.StrokeDashArray = new DoubleCollection { 4, 2 };
                    Canvas.SetLeft(elli, Math.Min(ep1.X, ep2.X));
                    Canvas.SetTop(elli, Math.Min(ep1.Y, ep2.Y));
                    CodingOverlayCanvas.Children.Add(elli);
                }
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

    // --- Referenz-DN: Gestrichelter Kreis am kalibrierten Rohrdurchmesser ---

    private void UpdateCodingOverlayInfo(OverlayGeometry? overlay)
    {
        var state = CodingOverlayMeasurementFormatter.BuildPanelState(overlay);
        TxtCodingQ1.Text = state.Q1Text;
        TxtCodingQ2.Text = state.Q2Text;
        TxtCodingClock.Text = state.ClockText;
        TxtCodingArc.Text = state.ArcText;
        TxtCodingMeasurement.Text = state.MeasurementText;
        CodingMeasurementPanel.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // --- Coding Code-Auswahl ---
}
