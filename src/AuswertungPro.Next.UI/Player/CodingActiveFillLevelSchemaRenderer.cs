using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public static class CodingActiveFillLevelSchemaRenderer
{
    public static bool Render(
        Canvas canvas,
        FillLevelSchema fill,
        OverlayGeometry? overlay,
        Effect? effect,
        Func<NormalizedPoint, Point> toPixel,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(fill);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (overlay is null || overlay.Points.Count < 2)
            return false;

        var strokeColor = fill.Mode switch
        {
            LevelMode.Water => Color.FromRgb(65, 105, 225),
            LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
            _ => Color.FromRgb(210, 105, 30)
        };
        var stroke = new SolidColorBrush(strokeColor);
        var fillBrush = new SolidColorBrush(Color.FromArgb(68, strokeColor.R, strokeColor.G, strokeColor.B));

        CodingSchemaOverlayRenderer.AddPipeReference(
            canvas,
            fill.PipeCenter,
            fill.PipeRadius,
            canvasWidth,
            canvasHeight,
            stroke,
            effect,
            OverlayTags.Preview);

        var center = toPixel(fill.PipeCenter);
        double radiusPx = fill.PipeRadius * Math.Min(canvasWidth, canvasHeight);
        double top = center.Y - radiusPx;
        double bottom = center.Y + radiusPx;
        var lineP1 = toPixel(overlay.Points[0]);
        var lineP2 = toPixel(overlay.Points[1]);
        double levelY = lineP1.Y;

        var segment = new Rectangle
        {
            Width = Math.Max(1, radiusPx * 2),
            Height = Math.Max(1, fill.Mode == LevelMode.Obstacle ? levelY - top : bottom - levelY),
            Fill = fillBrush,
            Tag = OverlayTags.Preview,
            Clip = new EllipseGeometry(center, radiusPx, radiusPx)
        };
        Canvas.SetLeft(segment, center.X - radiusPx);
        Canvas.SetTop(segment, fill.Mode == LevelMode.Obstacle ? top : levelY);
        canvas.Children.Add(segment);

        var levelLine = new System.Windows.Shapes.Line
        {
            X1 = lineP1.X,
            Y1 = levelY,
            X2 = lineP2.X,
            Y2 = levelY,
            Stroke = stroke,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Effect = effect,
            Tag = OverlayTags.Preview
        };
        canvas.Children.Add(levelLine);

        CodingOverlayDotMarkerRenderer.Add(canvas, new Point(center.X, levelY), 6, stroke, OverlayTags.Preview, effect);
        CodingSchemaOverlayRenderer.AddLabel(
            canvas,
            new Point(center.X, levelY),
            $"{overlay.FillPercent:F1}%",
            stroke,
            effect,
            OverlayTags.Measure);

        return true;
    }
}
