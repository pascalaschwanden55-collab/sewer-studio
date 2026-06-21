using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.Domain.Models;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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

    private void RenderPipeBendOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        var stroke = isPreview
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (overlay.Points.Count == 2)
        {
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

            AddDotMarker(a, 6, stroke, tag, glowEffect);
            AddDotMarker(b, 6, stroke, tag, glowEffect);
            return;
        }

        if (overlay.Points.Count < 3) return;

        var p1 = CodingNormToPixel(overlay.Points[0]);
        var vertex = CodingNormToPixel(overlay.Points[1]);
        var p3 = CodingNormToPixel(overlay.Points[2]);

        var line1 = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = vertex.X, Y2 = vertex.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line1.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line1);

        var line2 = new System.Windows.Shapes.Line
        {
            X1 = vertex.X, Y1 = vertex.Y, X2 = p3.X, Y2 = p3.Y,
            Stroke = stroke, StrokeThickness = 3, Effect = glowEffect, Tag = tag
        };
        if (isPreview) line2.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(line2);

        AddDotMarker(p1, 6, stroke, tag, glowEffect);
        AddDotMarker(vertex, 8, stroke, tag, glowEffect);
        AddDotMarker(p3, 6, stroke, tag, glowEffect);

        double arcRadius = 30;
        double angle1 = Math.Atan2(p1.Y - vertex.Y, p1.X - vertex.X);
        double angle2 = Math.Atan2(p3.Y - vertex.Y, p3.X - vertex.X);

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

    private void RenderLateralCircleOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

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

        AddDotMarker(center, 5, stroke, tag, glowEffect);

        var radLine = new System.Windows.Shapes.Line
        {
            X1 = center.X, Y1 = center.Y, X2 = edge.X, Y2 = edge.Y,
            Stroke = stroke, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Effect = glowEffect, Tag = tag
        };
        CodingOverlayCanvas.Children.Add(radLine);

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

    private void RenderRulerOverlay(
        OverlayGeometry overlay, bool isPreview, Brush defaultStroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect, string tag,
        NormalizedPoint? labelAnchor)
    {
        if (overlay.Points.Count < 2) return;

        var stroke = Brushes.White;
        var p1 = CodingNormToPixel(overlay.Points[0]);
        var p2 = CodingNormToPixel(overlay.Points[1]);

        var mainLine = new System.Windows.Shapes.Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
            Stroke = stroke, StrokeThickness = 2.5,
            Effect = glowEffect, Tag = tag
        };
        if (isPreview) mainLine.StrokeDashArray = new DoubleCollection { 4, 2 };
        CodingOverlayCanvas.Children.Add(mainLine);

        double totalMm = overlay.Q1Mm ?? 0;
        if (totalMm <= 0) return;

        double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
        double lineLen = Math.Sqrt(dx * dx + dy * dy);
        if (lineLen < 10) return;

        double normX = -dy / lineLen, normY = dx / lineLen;

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

            bool isMajor = i % 5 == 0;
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
}
