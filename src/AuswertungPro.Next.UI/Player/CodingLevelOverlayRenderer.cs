using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public static class CodingLevelOverlayRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Effect? effect,
        string tag,
        Func<NormalizedPoint, Point> toPixel,
        PipeCalibration? calibration,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (overlay.Points.Count >= 5)
        {
            RenderIntrusion(canvas, overlay, isPreview, effect, tag, toPixel, calibration, canvasWidth, canvasHeight);
            return true;
        }

        if (overlay.Points.Count < 2)
            return false;

        RenderLevel(canvas, overlay, isPreview, effect, tag, toPixel, calibration, canvasWidth, canvasHeight);
        return true;
    }

    private static void RenderIntrusion(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Effect? effect,
        string tag,
        Func<NormalizedPoint, Point> toPixel,
        PipeCalibration? calibration,
        double canvasWidth,
        double canvasHeight)
    {
        var intrusionStroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        var edge = toPixel(overlay.Points[0]);
        var tip = toPixel(overlay.Points[1]);
        var pipeCenter = overlay.Points[2];
        var left = toPixel(overlay.Points[3]);
        var right = toPixel(overlay.Points[4]);
        var pipeRadius = calibration?.NormalizedDiameter / 2.0 ?? 0.35;

        CodingSchemaOverlayRenderer.AddPipeReference(
            canvas,
            pipeCenter,
            pipeRadius,
            canvasWidth,
            canvasHeight,
            intrusionStroke,
            effect,
            tag);

        var tongue = new System.Windows.Shapes.Polygon
        {
            Stroke = intrusionStroke,
            StrokeThickness = 2.5,
            Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)72 : (byte)95, 239, 68, 68)),
            Effect = effect,
            Tag = tag
        };
        tongue.Points.Add(left);
        tongue.Points.Add(tip);
        tongue.Points.Add(right);
        canvas.Children.Add(tongue);

        var spine = new System.Windows.Shapes.Line
        {
            X1 = edge.X,
            Y1 = edge.Y,
            X2 = tip.X,
            Y2 = tip.Y,
            Stroke = intrusionStroke,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Effect = effect,
            Tag = tag
        };
        canvas.Children.Add(spine);

        CodingOverlayDotMarkerRenderer.Add(canvas, tip, 6, intrusionStroke, tag, effect);
        if (overlay.FillPercent.HasValue)
        {
            CodingSchemaOverlayRenderer.AddLabel(
                canvas,
                tip,
                $"Einragung {overlay.FillPercent:F1}%",
                intrusionStroke,
                effect,
                OverlayTags.Measure);
        }
    }

    private static void RenderLevel(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        Effect? effect,
        string tag,
        Func<NormalizedPoint, Point> toPixel,
        PipeCalibration? calibration,
        double canvasWidth,
        double canvasHeight)
    {
        var p1 = toPixel(overlay.Points[0]);
        var p2 = toPixel(overlay.Points[1]);
        var y = p1.Y;
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
            Effect = effect,
            Tag = tag
        };
        canvas.Children.Add(line);

        if (calibration is { IsCalibrated: true } cal)
        {
            CodingSchemaOverlayRenderer.AddPipeReference(
                canvas,
                cal.PipeCenter,
                cal.NormalizedDiameter / 2.0,
                canvasWidth,
                canvasHeight,
                stroke,
                effect,
                tag);

            var center = toPixel(cal.PipeCenter);
            var radiusPx = (cal.NormalizedDiameter / 2.0) * Math.Min(canvasWidth, canvasHeight);
            var top = center.Y - radiusPx;
            var bottom = center.Y + radiusPx;

            var segment = new Rectangle
            {
                Width = Math.Max(1, radiusPx * 2),
                Height = Math.Max(1, overlay.LevelSubMode == LevelMode.Obstacle ? y - top : bottom - y),
                Fill = new SolidColorBrush(Color.FromArgb(isPreview ? (byte)68 : (byte)88, strokeColor.R, strokeColor.G, strokeColor.B)),
                Tag = tag,
                Clip = new EllipseGeometry(center, radiusPx, radiusPx)
            };
            Canvas.SetLeft(segment, center.X - radiusPx);
            Canvas.SetTop(segment, overlay.LevelSubMode == LevelMode.Obstacle ? top : y);
            canvas.Children.Add(segment);
        }

        if (overlay.FillPercent.HasValue)
        {
            CodingSchemaOverlayRenderer.AddLabel(
                canvas,
                new Point((p1.X + p2.X) / 2, y),
                $"{overlay.FillPercent:F1}%",
                stroke,
                effect,
                OverlayTags.Measure);
        }
    }
}
