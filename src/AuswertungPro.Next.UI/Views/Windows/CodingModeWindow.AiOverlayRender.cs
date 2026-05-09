using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow KI-Overlay-Rendering (Slice 8a.2.2): Zeichnet die KI-
// Befunde als Bounding-Box / Wasserstand-Linie / Mess-Linie auf den
// AiOverlayCanvas. Reine WPF-Shape-Erzeugung — keine Geschaeftslogik,
// kein Session-State. Aus dem Hauptdatei extrahiert.
public partial class CodingModeWindow
{
    private void RenderAiOverlays(List<AiOverlay> overlays)
    {
        AiOverlayCanvas.Children.Clear();
        _currentAiOverlays = overlays;

        var canvasWidth = AiOverlayCanvas.ActualWidth;
        var canvasHeight = AiOverlayCanvas.ActualHeight;
        if (canvasWidth < 10 || canvasHeight < 10) return;

        // Nur das selektierte Overlay anzeigen (nicht alle gleichzeitig)
        var selectedEvent = _vm?.SelectedDefect;
        if (selectedEvent != null)
        {
            var selectedStatus = CodingSessionViewModel.GetDefectStatus(selectedEvent);
            if (selectedStatus is DefectStatus.Accepted or DefectStatus.AcceptedWithEdit or DefectStatus.Rejected)
                return; // Nach Benutzerentscheidung Overlay fuer selektierten Befund ausblenden
        }

        foreach (var overlay in overlays)
        {
            if (overlay.IsRejected) continue;

            // Wenn ein Event selektiert ist: nur dessen Overlay zeichnen
            if (selectedEvent != null)
            {
                var overlayCode = overlay.VsaCodeHint ?? overlay.Label;
                bool isMatch = string.Equals(overlayCode, selectedEvent.Entry.Code,
                    StringComparison.OrdinalIgnoreCase);
                if (!isMatch) continue;
            }

            RenderSingleAiOverlay(overlay, canvasWidth, canvasHeight);
        }
    }

    private void RenderSingleAiOverlay(AiOverlay overlay, double canvasWidth, double canvasHeight)
    {
        var geo = overlay.Geometry;
        var color = MapSeverityColor(overlay.Severity);
        var strokeBrush = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B));
        var fillBrush = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));

        switch (geo.ToolType)
        {
            case OverlayToolType.Rectangle:
                RenderAiRectangle(geo, canvasWidth, canvasHeight, strokeBrush, fillBrush, overlay);
                break;
            case OverlayToolType.Level:
                RenderAiLevel(geo, canvasWidth, canvasHeight, overlay);
                break;
            case OverlayToolType.Line:
                RenderAiLine(geo, canvasWidth, canvasHeight, strokeBrush, overlay);
                break;
        }
    }

    private void RenderAiRectangle(OverlayGeometry geo, double w, double h,
        Brush stroke, Brush fill, AiOverlay overlay)
    {
        if (geo.Points.Count < 4) return;
        double x1 = geo.Points[0].X * w, y1 = geo.Points[0].Y * h;
        double x2 = geo.Points[2].X * w, y2 = geo.Points[2].Y * h;

        var rect = new Rectangle
        {
            Width = Math.Abs(x2 - x1), Height = Math.Abs(y2 - y1),
            Stroke = stroke, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = fill, Tag = "ai_overlay",
            Cursor = System.Windows.Input.Cursors.Hand
        };
        // Klick auf BBox selektiert den zugehoerigen Befund in der Event-Liste
        var code = overlay.VsaCodeHint ?? overlay.Label;
        rect.MouseLeftButtonDown += (_, _) => SelectEventByCode(code);
        Canvas.SetLeft(rect, Math.Min(x1, x2));
        Canvas.SetTop(rect, Math.Min(y1, y2));
        AiOverlayCanvas.Children.Add(rect);

        AddAiLabel(overlay, Math.Min(x1, x2), Math.Min(y1, y2) - 20, w);
    }

    private void RenderAiLevel(OverlayGeometry geo, double w, double h, AiOverlay overlay)
    {
        if (geo.Points.Count < 2) return;
        double x1 = geo.Points[0].X * w, x2 = geo.Points[1].X * w;
        double y = geo.Points[0].Y * h;

        var levelColor = geo.LevelSubMode switch
        {
            LevelMode.Water => Color.FromRgb(65, 105, 225),
            LevelMode.Obstacle => Color.FromRgb(220, 20, 60),
            _ => Color.FromRgb(210, 105, 30)
        };

        var stroke = new SolidColorBrush(Color.FromArgb(220, levelColor.R, levelColor.G, levelColor.B));
        var fill = new SolidColorBrush(Color.FromArgb(40, levelColor.R, levelColor.G, levelColor.B));

        var line = new Line
        {
            X1 = x1, Y1 = y, X2 = x2, Y2 = y,
            Stroke = stroke, StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Tag = "ai_overlay"
        };
        AiOverlayCanvas.Children.Add(line);

        // Fuellflaeche mit Rohr-Ellipsen-Clip
        var aiCalib = _overlayService.Calibration;
        var aiPipeCenter = aiCalib?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
        double aiPipeR = (aiCalib?.NormalizedDiameter ?? 0.7) / 2.0;
        double aiCxPx = aiPipeCenter.X * w, aiCyPx = aiPipeCenter.Y * h;
        double aiRxPx = aiPipeR * w, aiRyPx = aiPipeR * h;

        double fillTop = geo.LevelSubMode == LevelMode.Obstacle ? (aiCyPx - aiRyPx) : y;
        double fillBottom = geo.LevelSubMode == LevelMode.Obstacle ? y : (aiCyPx + aiRyPx);
        var fillRect = new Rectangle
        {
            Width = aiRxPx * 2, Height = Math.Abs(fillBottom - fillTop),
            Fill = fill, Tag = "ai_overlay",
            Clip = new EllipseGeometry(
                new Point(aiRxPx, aiCyPx - Math.Min(fillTop, fillBottom)),
                aiRxPx, aiRyPx)
        };
        Canvas.SetLeft(fillRect, aiCxPx - aiRxPx);
        Canvas.SetTop(fillRect, Math.Min(fillTop, fillBottom));
        AiOverlayCanvas.Children.Add(fillRect);

        var pctText = geo.FillPercent.HasValue ? $"{geo.FillPercent:F1}%" : "?%";
        var modeText = geo.LevelSubMode switch
        {
            LevelMode.Water => "Wasser",
            LevelMode.Obstacle => "Hindernis",
            _ => "Ablagerung"
        };
        AddAiLabel(overlay, (x1 + x2) / 2 - 40, y - 24, w, $"{modeText}: {pctText}");
    }

    private void RenderAiLine(OverlayGeometry geo, double w, double h, Brush stroke, AiOverlay overlay)
    {
        if (geo.Points.Count < 2) return;
        double x1 = geo.Points[0].X * w, y1 = geo.Points[0].Y * h;
        double x2 = geo.Points[1].X * w, y2 = geo.Points[1].Y * h;

        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = "ai_overlay"
        };
        AiOverlayCanvas.Children.Add(line);

        var mmText = geo.Q1Mm.HasValue ? $"{geo.Q1Mm:F0}mm" : "";
        AddAiLabel(overlay, (x1 + x2) / 2 - 30, (y1 + y2) / 2 - 12, w, $"{overlay.Label} {mmText}".Trim());
    }

    private void AddAiLabel(AiOverlay overlay, double x, double y, double canvasWidth, string? customText = null)
    {
        var color = MapSeverityColor(overlay.Severity);
        var text = customText ?? $"{overlay.VsaCodeHint ?? overlay.Label}";
        if (text.Length > 30) text = text[..30] + "...";

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(210, 17, 19, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 4, 2), Tag = "ai_overlay",
            Child = new TextBlock
            {
                Text = text, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        Canvas.SetLeft(label, Math.Clamp(x, 2, canvasWidth - 120));
        Canvas.SetTop(label, Math.Max(2, y));
        AiOverlayCanvas.Children.Add(label);
    }

    private static Color MapSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),
        4 => Color.FromRgb(249, 115, 22),
        3 => Color.FromRgb(245, 158, 11),
        2 => Color.FromRgb(132, 204, 22),
        _ => Color.FromRgb(34, 197, 94)
    };
}
