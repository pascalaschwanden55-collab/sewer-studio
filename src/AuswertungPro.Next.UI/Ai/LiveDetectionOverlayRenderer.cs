using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

internal static class LiveDetectionOverlayRenderer
{
    public static void Render(
        Canvas canvas,
        IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec,
        Action<LiveFrameFinding, double> onFindingClicked)
    {
        canvas.Children.Clear();

        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width < 60 || height < 60 || findings.Count == 0)
            return;

        var hasBbox = findings.Any(f => f.BboxX1.HasValue && f.BboxY1.HasValue
                                     && f.BboxX2.HasValue && f.BboxY2.HasValue);

        if (!hasBbox)
        {
            LiveFrameRingOverlayRenderer.Draw(
                canvas,
                findings,
                LiveFrameRingOverlayMode.Interactive,
                width,
                height,
                timestampSec,
                onFindingClicked);
            return;
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var finding = findings[i];
            var color = LiveDetectionDisplayPolicy.DetectionSeverityColor(finding.Severity);

            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var bboxRect = LiveDetectionGeometryMapper.BBoxToCanvasRect(finding, width, height);
                if (bboxRect is null)
                    continue;

                AddDetectionCornerMarkers(
                    canvas,
                    bboxRect.Value.Left,
                    bboxRect.Value.Top,
                    bboxRect.Value.Width,
                    bboxRect.Value.Height,
                    color);

                var labelText = $"{finding.VsaCodeHint ?? finding.Label} [S{finding.Severity}]";
                if (finding.ExtentPercent is > 0)
                    labelText += $" {finding.ExtentPercent}%";

                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    IsHitTestVisible = true,
                    Child = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                };

                var capturedFinding = finding;
                var capturedTimestamp = timestampSec;
                label.MouseLeftButtonDown += (_, _) => onFindingClicked(capturedFinding, capturedTimestamp);
                label.ToolTip = LiveDetectionDisplayPolicy.BuildFindingAssignmentTooltip(finding);

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = label.DesiredSize;
                var lx = OverlayCanvasPosition.Clamp(
                    bboxRect.Value.Left,
                    width,
                    desired.Width);
                var ly = OverlayCanvasPosition.Clamp(
                    bboxRect.Value.Top - desired.Height - 4,
                    height,
                    desired.Height);
                Canvas.SetLeft(label, lx);
                Canvas.SetTop(label, ly);
                canvas.Children.Add(label);
            }
            else
            {
                LiveFrameRingOverlayRenderer.DrawFinding(
                    canvas,
                    finding,
                    i,
                    findings.Count,
                    LiveFrameRingOverlayMode.Interactive,
                    width,
                    height,
                    timestampSec,
                    onFindingClicked);
            }
        }
    }

    private static void AddDetectionCornerMarkers(Canvas canvas, double left, double top, double w, double h, Color color)
    {
        var len = Math.Clamp(Math.Min(w, h) * 0.18, 8, 22);
        var stroke = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B));

        var right = left + w;
        var bottom = top + h;

        AddCorner(left, top, +1, +1);
        AddCorner(right, top, -1, +1);
        AddCorner(left, bottom, +1, -1);
        AddCorner(right, bottom, -1, -1);

        void AddCorner(double x, double y, int dx, int dy)
        {
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x, Y1 = y, X2 = x + dx * len, Y2 = y,
                Stroke = stroke, StrokeThickness = 2.5, IsHitTestVisible = false
            });
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = x, Y1 = y, X2 = x, Y2 = y + dy * len,
                Stroke = stroke, StrokeThickness = 2.5, IsHitTestVisible = false
            });
        }
    }
}
