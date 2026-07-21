using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

internal enum PipelinePipeRadarMode
{
    Compact,
    Detail
}

/// <summary>
/// Zeichnet das Rohr-Radar der Videoanalyse. Das Fenster liefert nur Daten,
/// Anzeigeart und verfuegbare Groesse.
/// </summary>
internal static class PipelinePipeRadarRenderer
{
    internal static void Render(
        Canvas canvas,
        TextBlock emptyText,
        IEnumerable<DetectionItem> detections,
        PipelinePipeRadarMode mode,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(emptyText);
        ArgumentNullException.ThrowIfNull(detections);

        // Das Fenster kann waehrend des Aufbaus kurz noch keine brauchbare Groesse haben.
        if (width < 80 || height < 80)
            return;

        canvas.Children.Clear();

        var isCompact = mode == PipelinePipeRadarMode.Compact;
        var items = detections
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.MeterStart)
            .Take(isCompact ? 5 : 8)
            .ToList();

        emptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var layout = RadarLayout.Create(width, height, isCompact);
        AddPipeBase(canvas, layout);
        AddClockScale(canvas, layout, isCompact);

        for (var index = 0; index < items.Count; index++)
            AddDetection(canvas, layout, items[index], index, items.Count, isCompact);
    }

    private static void AddPipeBase(Canvas canvas, RadarLayout layout)
    {
        var backdrop = new Ellipse
        {
            Width = layout.OuterPipeRadius * 2.06,
            Height = layout.OuterPipeRadius * 2.06,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.52, 0.46),
                RadiusX = 0.70,
                RadiusY = 0.70,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(84, 23, 59, 46), 0.0),
                    new GradientStop(Color.FromArgb(26, 12, 38, 29), 0.72),
                    new GradientStop(Color.FromArgb(0, 12, 38, 29), 1.0)
                }
            }
        };
        Center(backdrop, layout.CenterX, layout.CenterY);
        canvas.Children.Add(backdrop);

        var pipeBody = new Ellipse
        {
            Width = layout.OuterPipeRadius * 2,
            Height = layout.OuterPipeRadius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(118, 122, 113)),
            StrokeThickness = 1.1,
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.46, 0.38),
                Center = new Point(0.50, 0.49),
                RadiusX = 0.63,
                RadiusY = 0.63,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(168, 162, 145), 0.0),
                    new GradientStop(Color.FromRgb(134, 129, 116), 0.32),
                    new GradientStop(Color.FromRgb(94, 93, 87), 0.78),
                    new GradientStop(Color.FromRgb(66, 70, 69), 1.0)
                }
            }
        };
        Center(pipeBody, layout.CenterX, layout.CenterY);
        canvas.Children.Add(pipeBody);

        canvas.Children.Add(new Path
        {
            Data = LiveDetectionGeometryMapper.BuildRingSectorGeometry(
                layout.CenterX,
                layout.CenterY,
                layout.RingInnerRadius,
                layout.RingOuterRadius,
                -90,
                359.9),
            Fill = new SolidColorBrush(Color.FromArgb(70, 50, 78, 48)),
            StrokeThickness = 0
        });

        var guideOuter = CreateGuide(
            layout.RingOuterRadius,
            Color.FromArgb(175, 232, 219, 92),
            1.0);
        Center(guideOuter, layout.CenterX, layout.CenterY);
        canvas.Children.Add(guideOuter);

        var guideInner = CreateGuide(
            layout.RingInnerRadius,
            Color.FromArgb(130, 232, 219, 92),
            0.9);
        Center(guideInner, layout.CenterX, layout.CenterY);
        canvas.Children.Add(guideInner);

        var hole = new Ellipse
        {
            Width = layout.CenterHoleRadius * 2,
            Height = layout.CenterHoleRadius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(33, 40, 39)),
            StrokeThickness = 1.0,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.45),
                GradientOrigin = new Point(0.45, 0.45),
                RadiusX = 0.8,
                RadiusY = 0.8,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(29, 35, 34), 0.0),
                    new GradientStop(Color.FromRgb(10, 14, 16), 1.0)
                }
            }
        };
        Center(hole, layout.CenterX, layout.CenterY);
        canvas.Children.Add(hole);
    }

    private static Ellipse CreateGuide(double radius, Color color, double thickness)
        => new()
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(color),
            StrokeDashArray = new DoubleCollection { 2, 3 },
            StrokeThickness = thickness,
            Fill = Brushes.Transparent
        };

    private static void AddClockScale(Canvas canvas, RadarLayout layout, bool isCompact)
    {
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleRad = LiveDetectionGeometryMapper.DegToRad(
                LiveDetectionGeometryMapper.ClockHourToAngleDegrees(hour));
            canvas.Children.Add(new Line
            {
                X1 = layout.CenterX + Math.Cos(angleRad) * (layout.RingInnerRadius - 6),
                Y1 = layout.CenterY + Math.Sin(angleRad) * (layout.RingInnerRadius - 6),
                X2 = layout.CenterX + Math.Cos(angleRad) * (layout.RingOuterRadius + 2),
                Y2 = layout.CenterY + Math.Sin(angleRad) * (layout.RingOuterRadius + 2),
                Stroke = new SolidColorBrush(Color.FromArgb(75, 204, 206, 184)),
                StrokeThickness = 0.9
            });
        }

        if (isCompact)
            return;

        foreach (var hour in new[] { 12, 3, 6, 9 })
        {
            var angleRad = LiveDetectionGeometryMapper.DegToRad(
                LiveDetectionGeometryMapper.ClockHourToAngleDegrees(hour));
            var text = new TextBlock
            {
                Text = hour.ToString(CultureInfo.InvariantCulture),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 233, 227, 160))
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var x = layout.CenterX + Math.Cos(angleRad) * (layout.RingOuterRadius + 10);
            var y = layout.CenterY + Math.Sin(angleRad) * (layout.RingOuterRadius + 10);
            Canvas.SetLeft(text, x - text.DesiredSize.Width / 2.0);
            Canvas.SetTop(text, y - text.DesiredSize.Height / 2.0);
            canvas.Children.Add(text);
        }
    }

    private static void AddDetection(
        Canvas canvas,
        RadarLayout layout,
        DetectionItem item,
        int index,
        int totalCount,
        bool isCompact)
    {
        var centerDegrees = ResolveCenterAngle(item, index, totalCount);
        var sweepDegrees = ResolveSweep(item);
        var startDegrees = centerDegrees - sweepDegrees / 2.0;
        var middleRadians = LiveDetectionGeometryMapper.DegToRad(centerDegrees);

        canvas.Children.Add(new Path
        {
            Data = LiveDetectionGeometryMapper.BuildRingSectorGeometry(
                layout.CenterX,
                layout.CenterY,
                layout.RingInnerRadius,
                layout.RingOuterRadius,
                startDegrees,
                sweepDegrees),
            Fill = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Clamp(84 + item.Confidence * 120, 84, 196),
                item.SeverityColor.R,
                item.SeverityColor.G,
                item.SeverityColor.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(232, 193, 237, 126)),
            StrokeThickness = 1.3
        });

        if (!isCompact && item.Confidence >= 0.85)
            AddConfidenceHalo(canvas, layout, startDegrees, sweepDegrees);

        var anchorRadius = layout.RingOuterRadius + 1;
        var labelRadius = layout.RingOuterRadius
            + (isCompact ? 14 : 16)
            + index % 2 * (isCompact ? 8 : 11);
        var anchorX = layout.CenterX + Math.Cos(middleRadians) * anchorRadius;
        var anchorY = layout.CenterY + Math.Sin(middleRadians) * anchorRadius;
        var labelX = layout.CenterX + Math.Cos(middleRadians) * labelRadius;
        var labelY = layout.CenterY + Math.Sin(middleRadians) * labelRadius;

        canvas.Children.Add(new Line
        {
            X1 = anchorX,
            Y1 = anchorY,
            X2 = labelX,
            Y2 = labelY,
            Stroke = new SolidColorBrush(Color.FromArgb(210, 66, 93, 51)),
            StrokeThickness = isCompact ? 1.0 : 1.1
        });

        var label = CreateLabel(item, layout.LabelMaxWidth, isCompact);
        PositionLabel(label, layout, labelX, labelY, middleRadians, index, isCompact);
        canvas.Children.Add(label);
    }

    private static void AddConfidenceHalo(
        Canvas canvas,
        RadarLayout layout,
        double startDegrees,
        double sweepDegrees)
    {
        canvas.Children.Add(new Path
        {
            Data = LiveDetectionGeometryMapper.BuildRingSectorGeometry(
                layout.CenterX,
                layout.CenterY,
                layout.RingInnerRadius - 2,
                layout.RingOuterRadius + 2,
                startDegrees,
                sweepDegrees),
            Fill = Brushes.Transparent,
            Stroke = new SolidColorBrush(Color.FromArgb(130, 233, 245, 128)),
            StrokeThickness = 1.2
        });
    }

    private static Border CreateLabel(DetectionItem item, double maxWidth, bool isCompact)
    {
        var title = string.IsNullOrWhiteSpace(item.Code) ? item.Label : $"{item.Code} {item.Label}";
        var titleLimit = isCompact ? 18 : 22;
        if (title.Length > titleLimit)
            title = title[..titleLimit] + "...";

        var detail = $"{item.MeterStart:0.0}-{item.MeterEnd:0.0}m";
        if (!string.IsNullOrWhiteSpace(item.PositionClock))
            detail += $" @ {item.PositionClock}h";
        if (!isCompact && item.ExtentPercent is > 0)
            detail += $" / {item.ExtentPercent}%";
        if (!isCompact)
            detail += $" / {item.ConfidencePct}";

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(240, 246, 250, 241)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(215, 146, 186, 104)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 3, 5, 3),
            Child = new TextBlock
            {
                Text = $"{title}\n{detail}",
                Foreground = new SolidColorBrush(Color.FromRgb(39, 44, 43)),
                FontSize = isCompact ? 9.0 : 9.4,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = maxWidth
            }
        };
    }

    private static void PositionLabel(
        Border label,
        RadarLayout layout,
        double anchorX,
        double anchorY,
        double middleRadians,
        int index,
        bool isCompact)
    {
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var alignRight = Math.Cos(middleRadians) >= 0;
        var left = alignRight ? anchorX + 3 : anchorX - desired.Width - 3;
        var topOffset = isCompact
            ? (index % 2 == 0 ? -4.0 : 4.0)
            : (index % 3 - 1) * 11.0;
        var top = anchorY - desired.Height / 2.0 + topOffset;

        Canvas.SetLeft(label, ClampCoordinate(left, layout.Width, desired.Width));
        Canvas.SetTop(label, ClampCoordinate(top, layout.Height, desired.Height));
    }

    private static double ClampCoordinate(double value, double available, double required)
        => Math.Clamp(value, 2, Math.Max(2, available - required - 2));

    private static double ResolveCenterAngle(DetectionItem item, int index, int totalCount)
    {
        var parsedClock = LiveDetectionGeometryMapper.ParseClockHour(item.PositionClock);
        if (parsedClock.HasValue)
            return LiveDetectionGeometryMapper.ClockHourToAngleDegrees(parsedClock.Value);

        return totalCount <= 1 ? -90 : -90 + index * (360.0 / totalCount);
    }

    private static double ResolveSweep(DetectionItem item)
    {
        if (item.ExtentPercent is > 0)
            return Math.Clamp(item.ExtentPercent.Value * 3.6, 22.0, 150.0);

        var meterSpan = Math.Max(0, item.MeterEnd - item.MeterStart);
        return Math.Clamp(20.0 + meterSpan * 3.0 + item.Confidence * 22.0, 20.0, 62.0);
    }

    private static void Center(FrameworkElement element, double centerX, double centerY)
    {
        Canvas.SetLeft(element, centerX - element.Width / 2.0);
        Canvas.SetTop(element, centerY - element.Height / 2.0);
    }

    private readonly record struct RadarLayout(
        double Width,
        double Height,
        double CenterX,
        double CenterY,
        double OuterPipeRadius,
        double RingOuterRadius,
        double RingInnerRadius,
        double CenterHoleRadius,
        double LabelMaxWidth)
    {
        internal static RadarLayout Create(double width, double height, bool isCompact)
        {
            var size = Math.Min(width, height);
            return new RadarLayout(
                width,
                height,
                width / 2.0,
                height / 2.0,
                size * 0.455,
                size * 0.385,
                size * 0.255,
                size * 0.19,
                Math.Max(isCompact ? 118 : 138, width * (isCompact ? 0.36 : 0.44)));
        }
    }
}
