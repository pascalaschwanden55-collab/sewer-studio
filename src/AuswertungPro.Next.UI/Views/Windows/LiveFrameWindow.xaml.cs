using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class LiveFrameWindow : Window
{
    private readonly List<LiveFrameFinding> _findings = new();

    public LiveFrameWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);
    }

    public void UpdateFrame(ImageSource? image, IReadOnlyList<LiveFrameFinding>? findings,
        string? status, string? info, string? quantSummary)
    {
        LiveImage.Source = image;
        PlaceholderText.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;

        _findings.Clear();
        if (findings is not null)
            _findings.AddRange(findings.Take(8));

        StatusText.Text = status ?? "";
        if (DataContext is VideoAnalysisPipelineViewModel vm)
        {
            // use binding fallback
        }

        QuantText.Text = quantSummary ?? "";
        RenderOverlay();
    }

    public void UpdateInfo(string? info)
    {
        // Update via header binding if DataContext is set
    }

    private void RenderOverlay()
    {
        OverlayCanvas.Children.Clear();

        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width < 60 || height < 60 || LiveImage.Source is null)
            return;

        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        // Outer guide
        var guide = new Ellipse
        {
            Width = ringOuter * 2,
            Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        OverlayCanvas.Children.Add(guide);

        // Inner guide
        var guideInner = new Ellipse
        {
            Width = ringInner * 2,
            Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        OverlayCanvas.Children.Add(guideInner);

        // Clock ticks
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            var x1 = cx + Math.Cos(rad) * (ringInner - 4);
            var y1 = cy + Math.Sin(rad) * (ringInner - 4);
            var x2 = cx + Math.Cos(rad) * (ringOuter + 4);
            var y2 = cy + Math.Sin(rad) * (ringOuter + 4);
            OverlayCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8
            });
        }

        if (_findings.Count == 0)
            return;

        for (var i = 0; i < _findings.Count; i++)
        {
            var finding = _findings[i];
            var parsedClock = ParseClockHour(finding.PositionClock);
            var centerDeg = parsedClock.HasValue
                ? -90 + (parsedClock.Value % 12) * 30
                : -90 + i * (360.0 / _findings.Count);

            var sweep = finding.ExtentPercent is > 0
                ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
                : 18.0;

            var startDeg = centerDeg - sweep / 2.0;
            var color = MapSeverityColor(finding.Severity);

            var sector = new Path
            {
                Data = BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
                Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                StrokeThickness = 1.0
            };
            OverlayCanvas.Children.Add(sector);

            var rad = DegToRad(centerDeg);
            var markerRadius = ringOuter + 2;
            var mx = cx + Math.Cos(rad) * markerRadius;
            var my = cy + Math.Sin(rad) * markerRadius;

            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 0.8
            };
            Canvas.SetLeft(dot, mx - 4);
            Canvas.SetTop(dot, my - 4);
            OverlayCanvas.Children.Add(dot);

            var labelText = BuildFindingLabel(finding);
            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(228, 17, 19, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                Child = new TextBlock
                {
                    Text = labelText,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
                }
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = label.DesiredSize;
            var lx = Math.Cos(rad) >= 0 ? mx + 8 : mx - desired.Width - 8;
            var ly = my - desired.Height / 2.0;
            Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
            Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
            OverlayCanvas.Children.Add(label);
        }
    }

    private static string BuildFindingLabel(LiveFrameFinding f)
    {
        var baseText = string.IsNullOrWhiteSpace(f.VsaCodeHint)
            ? f.Label : $"{f.VsaCodeHint} {f.Label}";
        if (baseText.Length > 24) baseText = baseText[..24] + "...";

        var clock = string.IsNullOrWhiteSpace(f.PositionClock) ? "?" : f.PositionClock;
        var extent = f.ExtentPercent is > 0 ? $"{f.ExtentPercent}%" : "n/a";
        var extra = "";
        if (f.HeightMm is > 0) extra += $" H:{f.HeightMm}mm";
        if (f.IntrusionPercent is > 0) extra += $" Einr:{f.IntrusionPercent}%";
        if (f.CrossSectionReductionPercent is > 0) extra += $" QV:{f.CrossSectionReductionPercent}%";
        return $"{clock} / {extent}{extra} - {baseText}";
    }

    private static Color MapSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),
        4 => Color.FromRgb(249, 115, 22),
        3 => Color.FromRgb(245, 158, 11),
        2 => Color.FromRgb(132, 204, 22),
        _ => Color.FromRgb(34, 197, 94)
    };

    private static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;
        if (hour == 0) return 12;
        if (hour > 12) hour %= 12;
        return hour == 0 ? 12 : hour;
    }

    private static Geometry BuildRingSectorGeometry(
        double cx, double cy, double innerR, double outerR, double startDeg, double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var large = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
        var p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
        var p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
        var p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { fig });
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;
}
