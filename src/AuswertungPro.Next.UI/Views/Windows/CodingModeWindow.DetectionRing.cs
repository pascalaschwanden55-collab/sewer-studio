using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Detection-Ring-Sektor (Slice 8a.2.5): Zeichnet
// einen 12-Stunden-Ring mit Sektoren fuer die KI-Befunde gemaess
// Uhrlage und Ausdehnung. Reine WPF-Path-Geometrie auf dem
// DetectionCanvas — keine Geschaeftslogik. Aus dem Hauptdatei
// extrahiert.
public partial class CodingModeWindow
{
    private void RenderDetectionRingSector(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
    {
        DetectionCanvas.Children.Clear();

        var width = DetectionCanvas.ActualWidth;
        var height = DetectionCanvas.ActualHeight;
        if (width < 60 || height < 60 || findings.Count == 0) return;

        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var guide = new Ellipse
        {
            Width = ringOuter * 2, Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        DetectionCanvas.Children.Add(guide);

        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = angleDeg * Math.PI / 180.0;
            DetectionCanvas.Children.Add(new Line
            {
                X1 = cx + Math.Cos(rad) * (ringInner - 4),
                Y1 = cy + Math.Sin(rad) * (ringInner - 4),
                X2 = cx + Math.Cos(rad) * (ringOuter + 4),
                Y2 = cy + Math.Sin(rad) * (ringOuter + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(50, 227, 227, 201)),
                StrokeThickness = 0.8, IsHitTestVisible = false
            });
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var f = findings[i];
            var normalizedClock = VsaCodeResolver.NormalizeClock(f.PositionClock);
            var clockMatch = Regex.Match(normalizedClock ?? "", @"(\d{1,2})");
            int parsedClock = clockMatch.Success && int.TryParse(clockMatch.Groups[1].Value, out var ch) ? ch : 0;
            if (parsedClock == 0) parsedClock = 12;

            var centerDeg = -90 + (parsedClock % 12) * 30;
            var sweep = f.ExtentPercent is > 0
                ? Math.Clamp(f.ExtentPercent.Value * 3.6, 14.0, 160.0) : 18.0;
            var startDeg = centerDeg - sweep / 2.0;
            var color = MapSeverityColor(f.Severity);

            var startRad = startDeg * Math.PI / 180.0;
            var endRad = (startDeg + sweep) * Math.PI / 180.0;
            var large = sweep > 180;
            var p1 = new Point(cx + Math.Cos(startRad) * ringOuter, cy + Math.Sin(startRad) * ringOuter);
            var p2 = new Point(cx + Math.Cos(endRad) * ringOuter, cy + Math.Sin(endRad) * ringOuter);
            var p3 = new Point(cx + Math.Cos(endRad) * ringInner, cy + Math.Sin(endRad) * ringInner);
            var p4 = new Point(cx + Math.Cos(startRad) * ringInner, cy + Math.Sin(startRad) * ringInner);

            var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
            fig.Segments.Add(new ArcSegment(p2, new Size(ringOuter, ringOuter), 0, large, SweepDirection.Clockwise, true));
            fig.Segments.Add(new LineSegment(p3, true));
            fig.Segments.Add(new ArcSegment(p4, new Size(ringInner, ringInner), 0, large, SweepDirection.Counterclockwise, true));

            var sector = new Path
            {
                Data = new PathGeometry(new[] { fig }),
                Fill = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
                StrokeThickness = 1.0, IsHitTestVisible = false
            };
            DetectionCanvas.Children.Add(sector);
        }
    }
}
