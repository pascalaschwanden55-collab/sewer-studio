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
            RenderRingSectorOverlay(canvas, findings, timestampSec, width, height, onFindingClicked);
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
                var lx = Math.Clamp(bboxRect.Value.Left, 2, width - desired.Width - 2);
                var ly = Math.Clamp(bboxRect.Value.Top - desired.Height - 4, 2, height - desired.Height - 2);
                Canvas.SetLeft(label, lx);
                Canvas.SetTop(label, ly);
                canvas.Children.Add(label);
            }
            else
            {
                RenderRingSectorFinding(canvas, finding, i, findings.Count, width, height, timestampSec, onFindingClicked);
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

    private static void RenderRingSectorOverlay(
        Canvas canvas,
        IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec,
        double width,
        double height,
        Action<LiveFrameFinding, double> onFindingClicked)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var guide = new System.Windows.Shapes.Ellipse
        {
            Width = ringOuter * 2, Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        canvas.Children.Add(guide);

        var guideInner = new System.Windows.Shapes.Ellipse
        {
            Width = ringInner * 2, Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        canvas.Children.Add(guideInner);

        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = LiveDetectionGeometryMapper.DegToRad(angleDeg);
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = cx + Math.Cos(rad) * (ringInner - 4),
                Y1 = cy + Math.Sin(rad) * (ringInner - 4),
                X2 = cx + Math.Cos(rad) * (ringOuter + 4),
                Y2 = cy + Math.Sin(rad) * (ringOuter + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8, IsHitTestVisible = false
            });
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
            RenderRingSectorFinding(canvas, findings[i], i, findings.Count, width, height, timestampSec, onFindingClicked);
    }

    private static void RenderRingSectorFinding(
        Canvas canvas,
        LiveFrameFinding finding,
        int index,
        int total,
        double width,
        double height,
        double timestampSec,
        Action<LiveFrameFinding, double> onFindingClicked)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var parsedClock = LiveDetectionGeometryMapper.ParseClockHour(finding.PositionClock);
        var centerDeg = parsedClock.HasValue
            ? -90 + (parsedClock.Value % 12) * 30
            : -90 + index * (360.0 / total);

        var sweep = finding.ExtentPercent is > 0
            ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
            : 18.0;

        var startDeg = centerDeg - sweep / 2.0;
        var color = LiveDetectionDisplayPolicy.DetectionSeverityColor(finding.Severity);

        var sector = new System.Windows.Shapes.Path
        {
            Data = LiveDetectionGeometryMapper.BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
            Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 1.0, IsHitTestVisible = false
        };
        canvas.Children.Add(sector);

        var rad2 = LiveDetectionGeometryMapper.DegToRad(centerDeg);
        var mx = cx + Math.Cos(rad2) * (ringOuter + 2);
        var my = cy + Math.Sin(rad2) * (ringOuter + 2);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White, StrokeThickness = 0.8, IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, mx - 4);
        Canvas.SetTop(dot, my - 4);
        canvas.Children.Add(dot);

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 17, 19, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = LiveDetectionDisplayPolicy.BuildDetectionLabel(finding),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        var capturedFinding = finding;
        var capturedTimestamp = timestampSec;
        label.MouseLeftButtonDown += (_, _) => onFindingClicked(capturedFinding, capturedTimestamp);
        label.ToolTip = LiveDetectionDisplayPolicy.BuildFindingAssignmentTooltip(finding);

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var lx = Math.Cos(rad2) >= 0 ? mx + 8 : mx - desired.Width - 8;
        var ly = my - desired.Height / 2.0;
        Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
        Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
        canvas.Children.Add(label);
    }
}
