using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Ai.Live;

internal enum LiveFrameRingOverlayMode
{
    Compact,
    Detail,
    Interactive
}

/// <summary>
/// Gemeinsame Ring-Zeichnung fuer eingebetteten Live-Frame, abgedocktes Fenster
/// und den Ring-Rueckfall des Players. Sichtbare Stilunterschiede bleiben erhalten.
/// </summary>
internal static class LiveFrameRingOverlayRenderer
{
    private static readonly RingStyle CompactStyle = new(
        DotDiameter: 7,
        LabelBackground: Color.FromArgb(228, 14, 19, 28),
        LabelPadding: new Thickness(4, 2, 4, 2),
        LabelFontSize: 10,
        LabelGap: 6,
        LabelBuilder: LiveFindingSummaryBuilder.BuildFindingLabel);

    private static readonly RingStyle DetailStyle = new(
        DotDiameter: 8,
        LabelBackground: Color.FromArgb(228, 17, 19, 24),
        LabelPadding: new Thickness(5, 2, 5, 2),
        LabelFontSize: 11,
        LabelGap: 8,
        LabelBuilder: BuildDetailLabel);

    private static readonly RingStyle InteractiveStyle = new(
        DotDiameter: 8,
        LabelBackground: Color.FromArgb(228, 17, 19, 24),
        LabelPadding: new Thickness(5, 2, 5, 2),
        LabelFontSize: 11,
        LabelGap: 8,
        LabelBuilder: LiveDetectionDisplayPolicy.BuildDetectionLabel);

    internal static void Draw(
        Canvas canvas,
        IReadOnlyList<LiveFrameFinding> findings,
        LiveFrameRingOverlayMode mode,
        double width,
        double height,
        double timestampSeconds = 0,
        Action<LiveFrameFinding, double>? onFindingClicked = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(findings);

        if (width < 60 || height < 60)
            return;

        var layout = RingLayout.Create(width, height);
        AddGuides(canvas, layout);

        var style = ResolveStyle(mode);
        var renderCount = Math.Min(findings.Count, 8);
        for (var index = 0; index < renderCount; index++)
        {
            DrawFindingCore(
                canvas,
                findings[index],
                index,
                findings.Count,
                layout,
                style,
                timestampSeconds,
                onFindingClicked);
        }
    }

    internal static void DrawFinding(
        Canvas canvas,
        LiveFrameFinding finding,
        int index,
        int totalCount,
        LiveFrameRingOverlayMode mode,
        double width,
        double height,
        double timestampSeconds = 0,
        Action<LiveFrameFinding, double>? onFindingClicked = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(finding);

        if (width < 60 || height < 60 || totalCount <= 0)
            return;

        DrawFindingCore(
            canvas,
            finding,
            index,
            totalCount,
            RingLayout.Create(width, height),
            ResolveStyle(mode),
            timestampSeconds,
            onFindingClicked);
    }

    private static void AddGuides(Canvas canvas, RingLayout layout)
    {
        var outer = CreateGuide(
            layout.OuterRadius,
            Color.FromArgb(125, 197, 209, 134),
            thickness: 1.0);
        Center(outer, layout.CenterX, layout.CenterY);
        canvas.Children.Add(outer);

        var inner = CreateGuide(
            layout.InnerRadius,
            Color.FromArgb(105, 197, 209, 134),
            thickness: 0.9);
        Center(inner, layout.CenterX, layout.CenterY);
        canvas.Children.Add(inner);

        for (var hour = 1; hour <= 12; hour++)
        {
            var radians = LiveDetectionGeometryMapper.DegToRad(
                LiveDetectionGeometryMapper.ClockHourToAngleDegrees(hour));
            canvas.Children.Add(new Line
            {
                X1 = layout.CenterX + Math.Cos(radians) * (layout.InnerRadius - 4),
                Y1 = layout.CenterY + Math.Sin(radians) * (layout.InnerRadius - 4),
                X2 = layout.CenterX + Math.Cos(radians) * (layout.OuterRadius + 4),
                Y2 = layout.CenterY + Math.Sin(radians) * (layout.OuterRadius + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            });
        }
    }

    private static Ellipse CreateGuide(double radius, Color color, double thickness)
        => new()
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(color),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = thickness,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };

    private static void DrawFindingCore(
        Canvas canvas,
        LiveFrameFinding finding,
        int index,
        int totalCount,
        RingLayout layout,
        RingStyle style,
        double timestampSeconds,
        Action<LiveFrameFinding, double>? onFindingClicked)
    {
        var parsedClock = LiveDetectionGeometryMapper.ParseClockHour(finding.PositionClock);
        var centerDegrees = parsedClock.HasValue
            ? LiveDetectionGeometryMapper.ClockHourToAngleDegrees(parsedClock.Value)
            : -90 + index * (360.0 / totalCount);
        var sweepDegrees = finding.ExtentPercent is > 0
            ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
            : 18.0;
        var startDegrees = centerDegrees - sweepDegrees / 2.0;
        var color = StatusColors.Current.SeverityOverlay(finding.Severity);

        canvas.Children.Add(new Path
        {
            Data = LiveDetectionGeometryMapper.BuildRingSectorGeometry(
                layout.CenterX,
                layout.CenterY,
                layout.InnerRadius,
                layout.OuterRadius,
                startDegrees,
                sweepDegrees),
            Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 1.0,
            IsHitTestVisible = false
        });

        var radians = LiveDetectionGeometryMapper.DegToRad(centerDegrees);
        var markerRadius = layout.OuterRadius + 2;
        var markerX = layout.CenterX + Math.Cos(radians) * markerRadius;
        var markerY = layout.CenterY + Math.Sin(radians) * markerRadius;
        var dot = new Ellipse
        {
            Width = style.DotDiameter,
            Height = style.DotDiameter,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White,
            StrokeThickness = 0.8,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, markerX - style.DotDiameter / 2.0);
        Canvas.SetTop(dot, markerY - style.DotDiameter / 2.0);
        canvas.Children.Add(dot);

        var label = CreateLabel(finding, color, style, timestampSeconds, onFindingClicked);
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var left = Math.Cos(radians) >= 0
            ? markerX + style.LabelGap
            : markerX - desired.Width - style.LabelGap;
        var top = markerY - desired.Height / 2.0;
        Canvas.SetLeft(label, OverlayCanvasPosition.Clamp(left, layout.Width, desired.Width));
        Canvas.SetTop(label, OverlayCanvasPosition.Clamp(top, layout.Height, desired.Height));
        canvas.Children.Add(label);
    }

    private static Border CreateLabel(
        LiveFrameFinding finding,
        Color color,
        RingStyle style,
        double timestampSeconds,
        Action<LiveFrameFinding, double>? onFindingClicked)
    {
        var label = new Border
        {
            Background = new SolidColorBrush(style.LabelBackground),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = style.LabelPadding,
            Child = new TextBlock
            {
                Text = style.LabelBuilder(finding),
                FontSize = style.LabelFontSize,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        if (onFindingClicked is not null)
        {
            label.Cursor = Cursors.Hand;
            label.IsHitTestVisible = true;
            label.ToolTip = LiveDetectionDisplayPolicy.BuildFindingAssignmentTooltip(finding);
            label.MouseLeftButtonDown += (_, _) => onFindingClicked(finding, timestampSeconds);
        }

        return label;
    }

    private static RingStyle ResolveStyle(LiveFrameRingOverlayMode mode)
        => mode switch
        {
            LiveFrameRingOverlayMode.Compact => CompactStyle,
            LiveFrameRingOverlayMode.Detail => DetailStyle,
            LiveFrameRingOverlayMode.Interactive => InteractiveStyle,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private static string BuildDetailLabel(LiveFrameFinding finding)
        => LiveFindingSummaryBuilder.BuildFindingLabel(finding, titleLimit: 24);

    private static void Center(FrameworkElement element, double centerX, double centerY)
    {
        Canvas.SetLeft(element, centerX - element.Width / 2.0);
        Canvas.SetTop(element, centerY - element.Height / 2.0);
    }

    private readonly record struct RingLayout(
        double Width,
        double Height,
        double CenterX,
        double CenterY,
        double OuterRadius,
        double InnerRadius)
    {
        internal static RingLayout Create(double width, double height)
        {
            var size = Math.Min(width, height) * 0.78;
            return new RingLayout(
                width,
                height,
                width / 2.0,
                height / 2.0,
                size * 0.42,
                size * 0.28);
        }
    }

    private readonly record struct RingStyle(
        double DotDiameter,
        Color LabelBackground,
        Thickness LabelPadding,
        double LabelFontSize,
        double LabelGap,
        Func<LiveFrameFinding, string> LabelBuilder);
}
