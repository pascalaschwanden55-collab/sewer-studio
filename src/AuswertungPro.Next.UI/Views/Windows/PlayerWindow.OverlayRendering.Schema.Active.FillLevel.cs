using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderActiveFillLevelSchema(FillLevelSchema fill, DropShadowEffect glowEffect)
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

        CodingSchemaOverlayRenderer.AddPipeReference(
            CodingOverlayCanvas,
            fill.PipeCenter,
            fill.PipeRadius,
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight,
            stroke,
            glowEffect,
            OverlayTags.Preview);

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
            Tag = OverlayTags.Preview,
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
            Tag = OverlayTags.Preview
        };
        CodingOverlayCanvas.Children.Add(levelLine);

        CodingOverlayDotMarkerRenderer.Add(CodingOverlayCanvas, new Point(center.X, levelY), 6, stroke, OverlayTags.Preview, glowEffect);
        CodingSchemaOverlayRenderer.AddLabel(
            CodingOverlayCanvas,
            new Point(center.X, levelY),
            $"{overlay.FillPercent:F1}%",
            stroke,
            glowEffect,
            OverlayTags.Measure);
    }
}
