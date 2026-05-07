using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Coding-Overlay-Rendering extrahiert aus PlayerWindow.xaml.cs.
// Zeichnet auf der CodingOverlayCanvas alle Geometrien (Linie, Rechteck, Punkt,
// Bogen, Pipe-Bend, Pipe-Direction, Lateral-Circle, Ruler, Level, Ellipse,
// Freihand) plus die Schema-Layer (PipeBend/FillLevel/Intrusion) und die
// Mess-Labels.
public partial class PlayerWindow
{
    private void RenderOverlayGeometry(OverlayGeometry overlay, bool isPreview, NormalizedPoint? labelAnchor = null)
    {
        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        string tag = isPreview ? "overlay_preview" : "overlay_manual";
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
                    var xs = overlay.Points.Select(p => p.X * w).ToList();
                    var ys = overlay.Points.Select(p => p.Y * h).ToList();
                    double minX = xs.Min();
                    double maxX = xs.Max();
                    double minY = ys.Min();
                    double maxY = ys.Max();

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

            case OverlayToolType.PipeDirection:
                RenderPipeDirectionOverlay(overlay, isPreview, glowEffect, tag);
                return;

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
                    // Geschlossenes Polygon (nicht offene Polyline) — umschliesst den Schadensbereich
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

        var text = BuildOverlayMeasurementText(overlay);
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
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(label, anchor.X + 12);
            Canvas.SetTop(label, anchor.Y - 20);
            CodingOverlayCanvas.Children.Add(label);
        }
    }

    private void RenderActiveCodingSchema()
    {
        if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)
            return;

        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 8,
            ShadowDepth = 0,
            Opacity = 0.95
        };

        switch (_codingSchemaManager.Active)
        {
            case PipeBendSchema bend:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay != null)
                    RenderPipeBendOverlay(overlay, true, Brushes.Gold, glowEffect, "overlay_preview", bend.Center);

                var center = CodingNormToPixel(bend.Center);
                var radiusHandle = CodingNormToPixel(bend.GetRadiusHandle());

                var guide = new System.Windows.Shapes.Line
                {
                    X1 = center.X,
                    Y1 = center.Y,
                    X2 = radiusHandle.X,
                    Y2 = radiusHandle.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 184, 0)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(guide);

                AddDotMarker(radiusHandle, 5, Brushes.White, "overlay_preview", glowEffect);
                break;
            }

            case FillLevelSchema fill:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null || overlay.Points.Count < 2)
                    return;

                var strokeColor = fill.Mode switch
                {
                    LevelMode.Water => Color.FromRgb(65, 105, 225),
                    LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
                    _ => Color.FromRgb(210, 105, 30)
                };
                var stroke = new SolidColorBrush(strokeColor);
                var fillBrush = new SolidColorBrush(Color.FromArgb(68, strokeColor.R, strokeColor.G, strokeColor.B));

                RenderSchemaPipeReference(fill.PipeCenter, fill.PipeRadius, stroke, glowEffect, "overlay_preview");

                var center = CodingNormToPixel(fill.PipeCenter);
                double rPx = fill.PipeRadius * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);
                double rx = rPx;
                double ry = rPx;
                double top = center.Y - rPx;
                double bottom = center.Y + rPx;
                var lineP1 = CodingNormToPixel(overlay.Points[0]);
                var lineP2 = CodingNormToPixel(overlay.Points[1]);
                double levelY = lineP1.Y;

                var segment = new Rectangle
                {
                    Width = Math.Max(1, rx * 2),
                    Height = Math.Max(1, fill.Mode == LevelMode.Obstacle ? levelY - top : bottom - levelY),
                    Fill = fillBrush,
                    Tag = "overlay_preview",
                    Clip = new EllipseGeometry(center, rx, ry)
                };
                Canvas.SetLeft(segment, center.X - rx);
                Canvas.SetTop(segment, fill.Mode == LevelMode.Obstacle ? top : levelY);
                CodingOverlayCanvas.Children.Add(segment);

                var levelLine = new System.Windows.Shapes.Line
                {
                    X1 = lineP1.X,
                    Y1 = levelY,
                    X2 = lineP2.X,
                    Y2 = levelY,
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect,
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(levelLine);

                AddDotMarker(new Point(center.X, levelY), 6, stroke, "overlay_preview", glowEffect);
                AddSchemaLabel(new Point(center.X, levelY), $"{overlay.FillPercent:F1}%", stroke, glowEffect);
                break;
            }

            case IntrusionSchema intrusion:
            {
                var overlay = BuildCodingSchemaGeometry();
                if (overlay == null)
                    return;

                var stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                var fillBrush = new SolidColorBrush(Color.FromArgb(72, 239, 68, 68));

                RenderSchemaPipeReference(intrusion.PipeCenter, intrusion.PipeRadius, stroke, glowEffect, "overlay_preview");

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
                    Tag = "overlay_preview"
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
                    Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(spine);

                AddDotMarker(tip, 7, stroke, "overlay_preview", glowEffect);
                AddDotMarker(edge, 5, Brushes.White, "overlay_preview", glowEffect);
                AddSchemaLabel(tip, $"{overlay.FillPercent:F1}% @ {overlay.ClockFrom:F1}h", stroke, glowEffect);
                break;
            }

            case PipeDirectionSchema pipeDir:
            {
                var pipeDirectionOverlay = BuildCodingSchemaGeometry();
                if (pipeDirectionOverlay != null)
                    RenderPipeDirectionOverlay(pipeDirectionOverlay, true, glowEffect, "overlay_preview");

                // Zwei Ellipsen + Verbindungslinie + Winkel-Label
                var stroke1 = new SolidColorBrush(Color.FromRgb(0, 200, 255));   // Cyan
                var stroke2 = new SolidColorBrush(Color.FromRgb(255, 165, 0));   // Orange
                var fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 200, 255));

                var c1 = CodingNormToPixel(pipeDir.Center1);
                var c2 = CodingNormToPixel(pipeDir.Center2);

                double canvasW = CodingOverlayCanvas.ActualWidth;
                double canvasH = CodingOverlayCanvas.ActualHeight;

                // Ellipse 1 (Rohrverbindung — Cyan)
                double rx1Px = pipeDir.RadiusX1 * canvasW;
                double ry1Px = pipeDir.RadiusY1 * canvasH;
                var ellipse1 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx1Px * 2, Height = ry1Px * 2,
                    Stroke = stroke1, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = fillBrush,
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse1, c1.X - rx1Px);
                Canvas.SetTop(ellipse1, c1.Y - ry1Px);
                CodingOverlayCanvas.Children.Add(ellipse1);

                // Ellipse 2 (weiter im Rohr — Orange)
                double rx2Px = pipeDir.RadiusX2 * canvasW;
                double ry2Px = pipeDir.RadiusY2 * canvasH;
                var ellipse2 = new System.Windows.Shapes.Ellipse
                {
                    Width = rx2Px * 2, Height = ry2Px * 2,
                    Stroke = stroke2, StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                Canvas.SetLeft(ellipse2, c2.X - rx2Px);
                Canvas.SetTop(ellipse2, c2.Y - ry2Px);
                CodingOverlayCanvas.Children.Add(ellipse2);

                // Verbindungslinie (Richtungswechsel)
                var connector = new System.Windows.Shapes.Line
                {
                    X1 = c1.X, Y1 = c1.Y, X2 = c2.X, Y2 = c2.Y,
                    Stroke = Brushes.White, StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Effect = glowEffect, Tag = "overlay_preview"
                };
                CodingOverlayCanvas.Children.Add(connector);

                // Handles
                AddDotMarker(c1, 7, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(c2, 7, stroke2, "overlay_preview", glowEffect);

                // Groessen-Handles (kleine Punkte an den Ellipsenraendern)
                AddDotMarker(new Point(c1.X + rx1Px, c1.Y), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c1.X, c1.Y + ry1Px), 4, stroke1, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X + rx2Px, c2.Y), 4, stroke2, "overlay_preview", glowEffect);
                AddDotMarker(new Point(c2.X, c2.Y + ry2Px), 4, stroke2, "overlay_preview", glowEffect);

                // Winkel-Label
                var midPoint = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
                AddSchemaLabel(midPoint, $"{pipeDir.AngleDeg:F0}°", Brushes.White, glowEffect);
                break;
            }
        }
    }

    private void RenderSchemaPipeReference(
        NormalizedPoint centerNorm,
        double radiusNorm,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        var center = CodingNormToPixel(centerNorm);
        // Kreisprofil: Radius in Pixel basierend auf Canvas-Hoehe
        // (Hoehe ist die kuerzere Dimension, damit der Kreis immer rund bleibt)
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
            Tag = "overlay_measure"
        };
        Canvas.SetLeft(label, anchor.X + 12);
        Canvas.SetTop(label, anchor.Y - 20);
        CodingOverlayCanvas.Children.Add(label);
    }

    private void RenderLevelOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        if (overlay.Points.Count >= 5)
        {
            var intrusionStroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            var edge = CodingNormToPixel(overlay.Points[0]);
            var tip = CodingNormToPixel(overlay.Points[1]);
            var pipeCenter = overlay.Points[2];
            var left = CodingNormToPixel(overlay.Points[3]);
            var right = CodingNormToPixel(overlay.Points[4]);
            var pipeRadius = _codingOverlayService?.Calibration?.NormalizedDiameter / 2.0 ?? 0.35;

            RenderSchemaPipeReference(pipeCenter, pipeRadius, intrusionStroke, glowEffect, tag);

            var tongue = new System.Windows.Shapes.Polygon
            {
                Stroke = intrusionStroke,
                StrokeThickness = 2.5,
                Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)72 : (byte)95, 239, 68, 68)),
                Effect = glowEffect,
                Tag = tag
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
                Stroke = intrusionStroke,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Effect = glowEffect,
                Tag = tag
            };
            CodingOverlayCanvas.Children.Add(spine);

            AddDotMarker(tip, 6, intrusionStroke, tag, glowEffect);
            if (overlay.FillPercent.HasValue)
                AddSchemaLabel(tip, $"Einragung {overlay.FillPercent:F1}%", intrusionStroke, glowEffect);
            return;
        }

        if (overlay.Points.Count < 2)
            return;

        var p1 = CodingNormToPixel(overlay.Points[0]);
        var p2 = CodingNormToPixel(overlay.Points[1]);
        double y = p1.Y;
        var strokeColor = overlay.LevelSubMode switch
        {
            LevelMode.Water => Color.FromRgb(65, 105, 225),
            LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
            _ => Color.FromRgb(210, 105, 30)
        };
        var stroke = new SolidColorBrush(strokeColor);

        var line = new System.Windows.Shapes.Line
        {
            X1 = p1.X,
            Y1 = y,
            X2 = p2.X,
            Y2 = y,
            Stroke = stroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        CodingOverlayCanvas.Children.Add(line);

        if (_codingOverlayService?.Calibration is { IsCalibrated: true } cal)
        {
            RenderSchemaPipeReference(cal.PipeCenter, cal.NormalizedDiameter / 2.0, stroke, glowEffect, tag);

            var center = CodingNormToPixel(cal.PipeCenter);
            double rPxCal = (cal.NormalizedDiameter / 2.0) * Math.Min(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight);
            double rx = rPxCal;
            double ry = rPxCal;
            double top = center.Y - rPxCal;
            double bottom = center.Y + rPxCal;

            var segment = new Rectangle
            {
                Width = Math.Max(1, rx * 2),
                Height = Math.Max(1, overlay.LevelSubMode == LevelMode.Obstacle ? y - top : bottom - y),
                Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)68 : (byte)88, strokeColor.R, strokeColor.G, strokeColor.B)),
                Tag = tag,
                Clip = new EllipseGeometry(center, rx, ry)
            };
            Canvas.SetLeft(segment, center.X - rx);
            Canvas.SetTop(segment, overlay.LevelSubMode == LevelMode.Obstacle ? top : y);
            CodingOverlayCanvas.Children.Add(segment);
        }

        if (overlay.FillPercent.HasValue)
            AddSchemaLabel(new Point((p1.X + p2.X) / 2, y), $"{overlay.FillPercent:F1}%", stroke, glowEffect);
    }

    private System.Windows.Shapes.Path? CreateArcPath(
        NormalizedPoint start,
        NormalizedPoint end,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect effect,
        string tag,
        bool dashed)
    {
        var centerNorm = _codingOverlayService?.Calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
        var center = CodingNormToPixel(centerNorm);
        var sp = CodingNormToPixel(start);
        var ep = CodingNormToPixel(end);

        double radius = Math.Sqrt(Math.Pow(sp.X - center.X, 2) + Math.Pow(sp.Y - center.Y, 2));
        if (radius < 3)
            return null;

        double startAngle = Math.Atan2(sp.X - center.X, -(sp.Y - center.Y));
        double endAngle = Math.Atan2(ep.X - center.X, -(ep.Y - center.Y));
        double angleDiff = endAngle - startAngle;
        if (angleDiff < 0) angleDiff += 2 * Math.PI;

        var arcEnd = new Point(
            center.X + radius * Math.Sin(endAngle),
            center.Y - radius * Math.Cos(endAngle));

        var figure = new System.Windows.Media.PathFigure { StartPoint = sp, IsClosed = false };
        figure.Segments.Add(new System.Windows.Media.ArcSegment(
            arcEnd,
            new Size(radius, radius),
            0,
            angleDiff > Math.PI,
            System.Windows.Media.SweepDirection.Clockwise,
            true));

        var geometry = new System.Windows.Media.PathGeometry();
        geometry.Figures.Add(figure);

        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = 3,
            Effect = effect,
            Tag = tag
        };
        if (dashed)
            path.StrokeDashArray = new DoubleCollection { 4, 2 };

        return path;
    }

    // --- Winkelmesser (Protractor): 2 Linien + Winkelbogen + Grad-Label ---

    /// <summary>
    /// Zeichnet ein gespeichertes PipeDirection-Overlay (2 Ellipsen + Winkel).
    /// Points: [Center1, Corner1(cx+rx, cy+ry), Center2, Corner2(cx+rx, cy+ry)]
    /// </summary>
    private void RenderPipeDirectionOverlay(
        OverlayGeometry overlay, bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag)
    {
        if (overlay.Points.Count < 4) return;

        var c1 = CodingNormToPixel(overlay.Points[0]);
        var corner1 = CodingNormToPixel(overlay.Points[1]);
        var c2 = CodingNormToPixel(overlay.Points[2]);
        var corner2 = CodingNormToPixel(overlay.Points[3]);

        double rx1 = Math.Abs(corner1.X - c1.X);
        double ry1 = Math.Abs(corner1.Y - c1.Y);
        double rx2 = Math.Abs(corner2.X - c2.X);
        double ry2 = Math.Abs(corner2.Y - c2.Y);

        var colorStart = Color.FromRgb(0, 200, 255);
        var colorEnd = Color.FromRgb(255, 165, 0);

        static Point LerpPoint(Point a, Point b, double t)
            => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        static double Lerp(double a, double b, double t)
            => a + (b - a) * t;
        static Color LerpColor(Color a, Color b, double t)
            => Color.FromRgb(
                (byte)Math.Clamp((int)Math.Round(a.R + (b.R - a.R) * t), 0, 255),
                (byte)Math.Clamp((int)Math.Round(a.G + (b.G - a.G) * t), 0, 255),
                (byte)Math.Clamp((int)Math.Round(a.B + (b.B - a.B) * t), 0, 255));

        double axisDx = c2.X - c1.X;
        double axisDy = c2.Y - c1.Y;
        double axisLen = Math.Sqrt(axisDx * axisDx + axisDy * axisDy);
        int ringCount = Math.Clamp((int)Math.Round(axisLen / 30.0), 4, 12);

        var spine = new System.Windows.Shapes.Line
        {
            X1 = c1.X,
            Y1 = c1.Y,
            X2 = c2.X,
            Y2 = c2.Y,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            StrokeThickness = 1.6,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Effect = glowEffect,
            Tag = tag
        };
        CodingOverlayCanvas.Children.Add(spine);

        if (axisLen > 0.5)
        {
            double nx = -axisDy / axisLen;
            double ny = axisDx / axisLen;
            double off1 = Math.Max(2.0, Math.Min(rx1, ry1) * 0.55);
            double off2 = Math.Max(2.0, Math.Min(rx2, ry2) * 0.55);

            var leftRail = new System.Windows.Shapes.Line
            {
                X1 = c1.X + nx * off1,
                Y1 = c1.Y + ny * off1,
                X2 = c2.X + nx * off2,
                Y2 = c2.Y + ny * off2,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Effect = glowEffect,
                Tag = tag
            };
            var rightRail = new System.Windows.Shapes.Line
            {
                X1 = c1.X - nx * off1,
                Y1 = c1.Y - ny * off1,
                X2 = c2.X - nx * off2,
                Y2 = c2.Y - ny * off2,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Effect = glowEffect,
                Tag = tag
            };
            CodingOverlayCanvas.Children.Add(leftRail);
            CodingOverlayCanvas.Children.Add(rightRail);
        }

        for (int i = 0; i <= ringCount; i++)
        {
            double t = ringCount == 0 ? 0 : i / (double)ringCount;
            var center = LerpPoint(c1, c2, t);
            double ringRx = Math.Max(2.0, Lerp(rx1, rx2, t));
            double ringRy = Math.Max(2.0, Lerp(ry1, ry2, t));
            var ringColor = LerpColor(colorStart, colorEnd, t);

            var ring = new System.Windows.Shapes.Ellipse
            {
                Width = ringRx * 2,
                Height = ringRy * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(240, ringColor.R, ringColor.G, ringColor.B)),
                StrokeThickness = i == 0 || i == ringCount ? 2.6 : 1.8,
                Fill = new SolidColorBrush(Color.FromArgb(24, ringColor.R, ringColor.G, ringColor.B)),
                Effect = glowEffect,
                Tag = tag
            };
            if (isPreview && i % 2 == 1)
                ring.StrokeDashArray = new DoubleCollection { 4, 2 };

            Canvas.SetLeft(ring, center.X - ringRx);
            Canvas.SetTop(ring, center.Y - ringRy);
            CodingOverlayCanvas.Children.Add(ring);
        }

        if (overlay.ArcDegrees.HasValue)
        {
            var mid = new Point((c1.X + c2.X) / 2, (c1.Y + c2.Y) / 2);
            AddSchemaLabel(mid, $"{overlay.ArcDegrees:F0}°", Brushes.White, glowEffect);
        }
    }

    private void RenderPipeBendOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        // Farbe: Gold fuer Vorschau, Orange-Gold finalisiert
        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (overlay.Points.Count == 2)
        {
            // Teilvorschau: nur Linie P1 → P2
            var a = CodingNormToPixel(overlay.Points[0]);
            var b = CodingNormToPixel(overlay.Points[1]);
            var line = new System.Windows.Shapes.Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = stroke, StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(line);

            // Punkt-Markierungen
            AddDotMarker(a, 6, stroke, tag, glowEffect);
            AddDotMarker(b, 6, stroke, tag, glowEffect);
            return;
        }

        if (overlay.Points.Count < 3) return;

        var p1 = CodingNormToPixel(overlay.Points[0]);
        var vertex = CodingNormToPixel(overlay.Points[1]);
        var p3 = CodingNormToPixel(overlay.Points[2]);

        // Linie 1: P1 → Vertex
        var line1 = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = vertex.X, Y2 = vertex.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line1.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line1);

        // Linie 2: Vertex → P3
        var line2 = new System.Windows.Shapes.Line
        {
            X1 = vertex.X, Y1 = vertex.Y, X2 = p3.X, Y2 = p3.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line2.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line2);

        // Punkt-Markierungen an allen 3 Punkten
        AddDotMarker(p1, 6, stroke, tag, glowEffect);
        AddDotMarker(vertex, 8, stroke, tag, glowEffect);
        AddDotMarker(p3, 6, stroke, tag, glowEffect);

        // Winkelbogen am Vertex (kleiner Bogen, Radius ~30px)
        double arcRadius = 30;
        double angle1 = Math.Atan2(p1.Y - vertex.Y, p1.X - vertex.X);
        double angle2 = Math.Atan2(p3.Y - vertex.Y, p3.X - vertex.X);

        // Bogen von angle1 nach angle2 (kuerzerer Weg)
        double angleDiff = angle2 - angle1;
        if (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
        if (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;

        var arcStart = new Point(
            vertex.X + arcRadius * Math.Cos(angle1),
            vertex.Y + arcRadius * Math.Sin(angle1));
        var arcEnd = new Point(
            vertex.X + arcRadius * Math.Cos(angle2),
            vertex.Y + arcRadius * Math.Sin(angle2));

        var arcFigure = new System.Windows.Media.PathFigure { StartPoint = arcStart, IsClosed = false };
        arcFigure.Segments.Add(new System.Windows.Media.ArcSegment(
            arcEnd,
            new Size(arcRadius, arcRadius),
            0,
            Math.Abs(angleDiff) > Math.PI,
            angleDiff > 0 ? System.Windows.Media.SweepDirection.Clockwise : System.Windows.Media.SweepDirection.Counterclockwise,
            true));

        var arcGeo = new System.Windows.Media.PathGeometry();
        arcGeo.Figures.Add(arcFigure);
        var arcPath = new System.Windows.Shapes.Path
        {
            Data = arcGeo, Stroke = stroke, StrokeThickness = 2,
            Effect = glowEffect, Tag = tag
        };
        CodingOverlayCanvas.Children.Add(arcPath);

        // Grad-Label am Vertex
        string angleText = overlay.ArcDegrees.HasValue
            ? $"{overlay.ArcDegrees.Value:F1}\u00B0"
            : "";
        if (!string.IsNullOrEmpty(angleText))
        {
            var lbl = new TextBlock
            {
                Text = angleText,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = stroke,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(6, 3, 6, 3),
                Effect = glowEffect,
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(lbl, vertex.X + 14);
            Canvas.SetTop(lbl, vertex.Y - 24);
            CodingOverlayCanvas.Children.Add(lbl);
        }
    }

    // --- DN-Kreis: Kreis + DN-Label ---

    private void RenderLateralCircleOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

        // Farbe: Hot Pink Vorschau, Magenta finalisiert
        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x69, 0xB4))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF));
        var fill = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0x00, 0xFF));

        var center = CodingNormToPixel(overlay.Points[0]);
        var edge = CodingNormToPixel(overlay.Points[1]);
        double radius = Math.Sqrt(Math.Pow(edge.X - center.X, 2) + Math.Pow(edge.Y - center.Y, 2));

        if (radius < 3) return;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Stroke = stroke, StrokeThickness = 2.5,
            Fill = fill, Effect = glowEffect, Tag = tag
        };
        if (isPreview) circle.StrokeDashArray = new DoubleCollection { 4, 2 };
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
        CodingOverlayCanvas.Children.Add(circle);

        // Mittelpunkt-Markierung
        AddDotMarker(center, 5, stroke, tag, glowEffect);

        // Radius-Linie
        var radLine = new System.Windows.Shapes.Line
        {
            X1 = center.X, Y1 = center.Y, X2 = edge.X, Y2 = edge.Y,
            Stroke = stroke, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Effect = glowEffect, Tag = tag
        };
        CodingOverlayCanvas.Children.Add(radLine);

        // DN-Label
        var parts = new List<string>();
        if (overlay.Q1Mm.HasValue)
            parts.Add($"DN {overlay.Q1Mm.Value:F0}");
        if (overlay.DnRatioPercent.HasValue)
            parts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");

        if (parts.Count > 0)
        {
            var lbl = new TextBlock
            {
                Text = string.Join(" ", parts),
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = stroke,
                Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
                Padding = new Thickness(6, 3, 6, 3),
                Effect = glowEffect,
                Tag = isPreview ? "overlay_measure" : "overlay_manual"
            };
            Canvas.SetLeft(lbl, center.X + radius + 8);
            Canvas.SetTop(lbl, center.Y - 12);
            CodingOverlayCanvas.Children.Add(lbl);
        }
    }

    // --- Lineal: Linie + senkrechte Tick-Marks + mm-Werte ---

    private void RenderRulerOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

        var stroke = Brushes.White;
        var p1 = CodingNormToPixel(overlay.Points[0]);
        var p2 = CodingNormToPixel(overlay.Points[1]);

        // Hauptlinie
        var mainLine = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
            Stroke = stroke, StrokeThickness = 2.5,
            Effect = glowEffect, Tag = tag
        };
        if (isPreview) mainLine.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(mainLine);

        // Tick-Marks berechnen
        double totalMm = overlay.Q1Mm ?? 0;
        if (totalMm <= 0) return;

        double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
        double lineLen = Math.Sqrt(dx * dx + dy * dy);
        if (lineLen < 10) return;

        // Senkrechte Richtung
        double normX = -dy / lineLen, normY = dx / lineLen;

        // Adaptive Tick-Teilung
        double tickInterval;
        if (totalMm > 500) tickInterval = 100;
        else if (totalMm > 200) tickInterval = 50;
        else if (totalMm > 50) tickInterval = 10;
        else tickInterval = 5;

        int tickCount = (int)(totalMm / tickInterval);
        for (int i = 0; i <= tickCount; i++)
        {
            double t = (i * tickInterval) / totalMm;
            if (t > 1.0) break;
            double tx = p1.X + dx * t;
            double ty = p1.Y + dy * t;

            // Grosse Ticks alle 5 Intervalle, sonst kleine
            bool isMajor = (i % 5 == 0);
            double tickLen = isMajor ? 10 : 5;

            var tick = new System.Windows.Shapes.Line
            {
                X1 = tx - normX * tickLen,
                Y1 = ty - normY * tickLen,
                X2 = tx + normX * tickLen,
                Y2 = ty + normY * tickLen,
                Stroke = stroke, StrokeThickness = isMajor ? 1.5 : 1,
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(tick);

            // Beschriftung bei grossen Ticks
            if (isMajor && i > 0)
            {
                var tickLbl = new TextBlock
                {
                    Text = $"{(int)(i * tickInterval)}",
                    FontSize = 9, Foreground = stroke,
                    Tag = tag
                };
                Canvas.SetLeft(tickLbl, tx + normX * 14 - 8);
                Canvas.SetTop(tickLbl, ty + normY * 14 - 6);
                CodingOverlayCanvas.Children.Add(tickLbl);
            }
        }

        // End-Ticks an Start und Ende
        foreach (var pt in new[] { p1, p2 })
        {
            var endTick = new System.Windows.Shapes.Line
            {
                X1 = pt.X - normX * 12, Y1 = pt.Y - normY * 12,
                X2 = pt.X + normX * 12, Y2 = pt.Y + normY * 12,
                Stroke = stroke, StrokeThickness = 2,
                Effect = glowEffect, Tag = tag
            };
            CodingOverlayCanvas.Children.Add(endTick);
        }

        // Gesamtlaenge-Label
        var anchorPt = labelAnchor != null ? CodingNormToPixel(labelAnchor) : new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        var totalLbl = new TextBlock
        {
            Text = $"{totalMm:F1} mm",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = stroke,
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            Padding = new Thickness(6, 3, 6, 3),
            Effect = glowEffect,
            Tag = isPreview ? "overlay_measure" : "overlay_manual"
        };
        Canvas.SetLeft(totalLbl, anchorPt.X + 12);
        Canvas.SetTop(totalLbl, anchorPt.Y - 20);
        CodingOverlayCanvas.Children.Add(totalLbl);
    }

    // --- Referenz-DN: Gestrichelter Kreis am kalibrierten Rohrdurchmesser ---

    private void RenderReferenceDn()
    {
        // Bestehende Referenz-DN-Elemente entfernen
        var old = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == "ref_dn")
            .ToList();
        foreach (var el in old) CodingOverlayCanvas.Children.Remove(el);

        if (!_showReferenceDn || _codingOverlayService?.Calibration == null) return;
        var cal = _codingOverlayService.Calibration;
        if (!cal.IsCalibrated || cal.NormalizedDiameter <= 0) return;

        double w = CodingOverlayCanvas.ActualWidth, h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var center = CodingNormToPixel(cal.PipeCenter);
        // Radius: halber normierter Durchmesser, skaliert auf Canvas-Breite
        double radiusPxX = (cal.NormalizedDiameter / 2.0) * w;
        double radiusPxY = (cal.NormalizedDiameter / 2.0) * h;

        var circle = new System.Windows.Shapes.Ellipse
        {
            Width = radiusPxX * 2, Height = radiusPxY * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = "ref_dn"
        };
        Canvas.SetLeft(circle, center.X - radiusPxX);
        Canvas.SetTop(circle, center.Y - radiusPxY);
        CodingOverlayCanvas.Children.Add(circle);

        // Label
        var lbl = new TextBlock
        {
            Text = $"Ref: DN {cal.NominalDiameterMm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
            Tag = "ref_dn"
        };
        Canvas.SetLeft(lbl, center.X + radiusPxX + 4);
        Canvas.SetTop(lbl, center.Y - 8);
        CodingOverlayCanvas.Children.Add(lbl);
    }

    // --- Hilfsmethode: Punkt-Markierung ---

    private void AddDotMarker(Point pos, double radius, Brush fill, string tag,
        System.Windows.Media.Effects.DropShadowEffect effect)
    {
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Fill = fill, Stroke = Brushes.White, StrokeThickness = 1.5,
            Effect = effect, Tag = tag
        };
        Canvas.SetLeft(dot, pos.X - radius);
        Canvas.SetTop(dot, pos.Y - radius);
        CodingOverlayCanvas.Children.Add(dot);
    }

    private static string BuildOverlayMeasurementText(OverlayGeometry overlay)
    {
        // Werkzeug-spezifische Texte
        if (overlay.ToolType is OverlayToolType.PipeBend or OverlayToolType.PipeDirection && overlay.ArcDegrees.HasValue)
            return $"Winkel: {overlay.ArcDegrees.Value:F1}\u00B0";

        if (overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue)
        {
            var mode = overlay.Points.Count >= 3
                ? "Einragung"
                : overlay.LevelSubMode switch
                {
                    LevelMode.Water => "Wasser",
                    LevelMode.Obstacle => "Hindernis",
                    _ => "Sediment"
                };
            return $"{mode}: {overlay.FillPercent.Value:F1}%";
        }

        if (overlay.ToolType == OverlayToolType.LateralCircle)
        {
            var dnParts = new List<string>();
            if (overlay.Q1Mm.HasValue) dnParts.Add($"DN {overlay.Q1Mm.Value:F0}");
            if (overlay.DnRatioPercent.HasValue) dnParts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");
            return string.Join(" ", dnParts);
        }

        if (overlay.ToolType == OverlayToolType.Ruler && overlay.Q1Mm.HasValue)
            return $"Laenge: {overlay.Q1Mm.Value:F1} mm";

        if (overlay.ToolType is OverlayToolType.Ellipse or OverlayToolType.Freehand or OverlayToolType.CrossSection && overlay.FillPercent.HasValue)
            return $"Querschnitt: {overlay.FillPercent.Value:F1}%";

        // Standard-Text fuer bestehende Werkzeuge
        var parts = new List<string>();

        if (overlay.Q1Mm.HasValue)
            parts.Add($"Q1:{overlay.Q1Mm.Value:F0}mm");
        if (overlay.Q2Mm.HasValue)
            parts.Add($"Q2:{overlay.Q2Mm.Value:F0}mm");
        if (overlay.ClockFrom.HasValue)
        {
            parts.Add(overlay.ClockTo.HasValue
                ? $"Uhr:{overlay.ClockFrom.Value:F1}->{overlay.ClockTo.Value:F1}"
                : $"Uhr:{overlay.ClockFrom.Value:F1}");
        }
        if (overlay.ArcDegrees.HasValue)
            parts.Add($"Bogen:{overlay.ArcDegrees.Value:F0}deg");

        return string.Join("  ", parts);
    }
}
