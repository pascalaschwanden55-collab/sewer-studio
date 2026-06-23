using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
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

            CodingSchemaOverlayRenderer.AddPipeReference(
                CodingOverlayCanvas,
                pipeCenter,
                pipeRadius,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                intrusionStroke,
                glowEffect,
                tag);

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

            CodingOverlayDotMarkerRenderer.Add(CodingOverlayCanvas, tip, 6, intrusionStroke, tag, glowEffect);
            if (overlay.FillPercent.HasValue)
                CodingSchemaOverlayRenderer.AddLabel(
                    CodingOverlayCanvas,
                    tip,
                    $"Einragung {overlay.FillPercent:F1}%",
                    intrusionStroke,
                    glowEffect,
                    OverlayTags.Measure);
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
            CodingSchemaOverlayRenderer.AddPipeReference(
                CodingOverlayCanvas,
                cal.PipeCenter,
                cal.NormalizedDiameter / 2.0,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                stroke,
                glowEffect,
                tag);

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
            CodingSchemaOverlayRenderer.AddLabel(
                CodingOverlayCanvas,
                new Point((p1.X + p2.X) / 2, y),
                $"{overlay.FillPercent:F1}%",
                stroke,
                glowEffect,
                OverlayTags.Measure);
    }
}
