using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// PhotoMeasurementWindow Multi-Punkt-Werkzeuge: Deformation (4 Klicks) und
// Querschnittsverminderung (Freihand-Polygon mit Doppelklick-Schluss). Plus
// Subject-Auswahl-Dialog. Aus dem Hauptdatei extrahiert (Slice 5b).
public partial class PhotoMeasurementWindow
{
    // ═══════════════════════════════════════════════
    // Deformation (4-Punkt-Klick)
    // ═══════════════════════════════════════════════

    private void AddDeformationPoint(NormalizedPoint point)
    {
        if (_clickPoints.Count >= 4) return;

        _clickPoints.Add(point);
        int idx = _clickPoints.Count;

        // Marker zeichnen
        var canvasPos = NormToCanvas(point.X, point.Y);
        var marker = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brushes.Orange,
            Stroke = Brushes.White, StrokeThickness = 1,
            Tag = TagOverlay
        };
        Canvas.SetLeft(marker, canvasPos.X - 5);
        Canvas.SetTop(marker, canvasPos.Y - 5);
        OverlayCanvas.Children.Add(marker);
        _clickMarkers.Add(marker);

        // Nummer-Label
        AddCanvasLabel($"{idx}", canvasPos.X + 8, canvasPos.Y - 14, TagOverlay);
        // Label ist das letzte Child
        var label = OverlayCanvas.Children[^1];

        // Undo-Frame: Marker + Label zusammen
        _undoFrames.Push(new List<UIElement> { marker, (UIElement)label });

        TxtStatus.Text = $"Deformation: Punkt {idx}/4 gesetzt";

        if (_clickPoints.Count == 4)
            FinalizeDeformation();
    }

    private void FinalizeDeformation()
    {
        // Automatisch sortieren: nach Uhr-Position relativ zur Rohrmitte
        // Oben = naechster Punkt an 12h, Unten = naechster an 6h, Links = 9h, Rechts = 3h
        var sorted = _clickPoints.ToList();
        var cx = _calibration.PipeCenter.X;
        var cy = _calibration.PipeCenter.Y;

        NormalizedPoint FindClosestToAngle(List<NormalizedPoint> pts, double targetDeg)
        {
            NormalizedPoint best = pts[0];
            double bestDelta = double.MaxValue;
            foreach (var p in pts)
            {
                double dx = p.X - cx, dy = p.Y - cy;
                double deg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
                if (deg < 0) deg += 360;
                double delta = Math.Abs(deg - targetDeg);
                if (delta > 180) delta = 360 - delta;
                if (delta < bestDelta) { bestDelta = delta; best = p; }
            }
            pts.Remove(best);
            return best;
        }

        var top = FindClosestToAngle(sorted, 0);       // 12 Uhr
        var bottom = FindClosestToAngle(sorted, 180);   // 6 Uhr
        var right = FindClosestToAngle(sorted, 90);     // 3 Uhr
        var left = sorted[0];                            // letzter = 9 Uhr

        double dVertNorm = PipeCalibration.AspectCorrectedDistance(top, bottom, _imageAspect);
        double dHorizNorm = PipeCalibration.AspectCorrectedDistance(left, right, _imageAspect);

        double dMax = Math.Max(dVertNorm, dHorizNorm);
        double dMin = Math.Min(dVertNorm, dHorizNorm);
        double dNominal = _calibration.NormalizedDiameter > 0
            ? _calibration.NormalizedDiameter
            : Math.Max(dVertNorm, dHorizNorm);

        double deformPct = dNominal > 0 ? ((dMax - dMin) / dNominal) * 100.0 : 0;

        _currentGeometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.Ellipse, // Deformation = ovale Verformung
            Points = _clickPoints.Select(p => new NormalizedPoint(p.X, p.Y)).ToList(),
            FillPercent = Math.Round(deformPct, 1)
        };

        // Kreuzlinien zeichnen
        var pTop = NormToCanvas(top.X, top.Y);
        var pBot = NormToCanvas(bottom.X, bottom.Y);
        var pLeft = NormToCanvas(left.X, left.Y);
        var pRight = NormToCanvas(right.X, right.Y);

        var vLine = new Line
        {
            X1 = pTop.X, Y1 = pTop.Y, X2 = pBot.X, Y2 = pBot.Y,
            Stroke = Brushes.Orange, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        var hLine = new Line
        {
            X1 = pLeft.X, Y1 = pLeft.Y, X2 = pRight.X, Y2 = pRight.Y,
            Stroke = Brushes.Orange, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(vLine);
        OverlayCanvas.Children.Add(hLine);

        var center = new Point((pTop.X + pBot.X) / 2, (pTop.Y + pBot.Y) / 2);
        AddCanvasLabel($"Deform: {deformPct:F1}%", center.X, center.Y - 20, TagOverlay);

        TxtMeasureInfo.Text = $"Deform: {deformPct:F1}%";
        TxtStatus.Text = $"Deformation: {deformPct:F1}% (V={dVertNorm:F3}, H={dHorizNorm:F3})";
    }

    // ═══════════════════════════════════════════════
    // Querschnittsverminderung (Freihand-Polygon)
    // ═══════════════════════════════════════════════

    private void AddPolygonPoint(NormalizedPoint point)
    {
        _clickPoints.Add(point);

        var canvasPos = NormToCanvas(point.X, point.Y);
        var marker = new Ellipse
        {
            Width = 8, Height = 8,
            Fill = Brushes.MediumPurple,
            Stroke = Brushes.White, StrokeThickness = 1,
            Tag = TagOverlay
        };
        Canvas.SetLeft(marker, canvasPos.X - 4);
        Canvas.SetTop(marker, canvasPos.Y - 4);
        OverlayCanvas.Children.Add(marker);
        _clickMarkers.Add(marker);

        // Undo-Frame: Marker + optional Verbindungslinie
        var frame = new List<UIElement> { marker };

        // Verbindungslinie zum vorherigen Punkt
        if (_clickPoints.Count >= 2)
        {
            var prev = _clickPoints[^2];
            var prevCanvas = NormToCanvas(prev.X, prev.Y);
            var line = new Line
            {
                X1 = prevCanvas.X, Y1 = prevCanvas.Y,
                X2 = canvasPos.X, Y2 = canvasPos.Y,
                Stroke = Brushes.MediumPurple, StrokeThickness = 1.5,
                Tag = TagOverlay
            };
            OverlayCanvas.Children.Add(line);
            frame.Add(line);
        }

        _undoFrames.Push(frame);

        TxtStatus.Text = $"Querschnitt: {_clickPoints.Count} Punkte | Doppelklick = schliessen";
    }

    private void ClosePolygon()
    {
        if (_clickPoints.Count < 3) return;
        _polygonClosed = true;

        // Polygon-Flaeche (Shoelace in Pixel-Koordinaten fuer korrekte Aspect-Ratio)
        var r = GetImageRenderedRect(PhotoImage);
        double area = 0;
        for (int i = 0; i < _clickPoints.Count; i++)
        {
            var curr = _clickPoints[i];
            var next = _clickPoints[(i + 1) % _clickPoints.Count];
            double cx = curr.X * r.Width, cy = curr.Y * r.Height;
            double nx = next.X * r.Width, ny = next.Y * r.Height;
            area += cx * ny - nx * cy;
        }
        area = Math.Abs(area) / 2.0;

        // Rohr-Querschnittsflaeche (in Pixel-Raum)
        double pipeRadius = (_calibration.NormalizedDiameter > 0
            ? _calibration.NormalizedDiameter : 0.7) / 2.0;
        double pipeRPx = pipeRadius * Math.Min(r.Width, r.Height);
        double pipeArea = Math.PI * pipeRPx * pipeRPx;

        double reductionPct = pipeArea > 0 ? (area / pipeArea) * 100.0 : 0;

        _currentGeometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.CrossSection,
            Points = _clickPoints.Select(p => new NormalizedPoint(p.X, p.Y)).ToList(),
            FillPercent = Math.Round(reductionPct, 1)
        };

        // Polygon zeichnen
        ClearByTag(TagOverlay);
        var polygon = new Polygon
        {
            Fill = PolygonFillBrush,
            Stroke = Brushes.MediumPurple,
            StrokeThickness = 2,
            Tag = TagOverlay
        };
        foreach (var pt in _clickPoints)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            polygon.Points.Add(cp);
        }
        OverlayCanvas.Children.Add(polygon);

        // Schwerpunkt fuer Label
        double centroidX = _clickPoints.Average(p => p.X);
        double centroidY = _clickPoints.Average(p => p.Y);
        var labelPos = NormToCanvas(centroidX, centroidY);
        AddCanvasLabel($"Quersch: {reductionPct:F1}%", labelPos.X, labelPos.Y - 12, TagOverlay);

        TxtMeasureInfo.Text = $"Quersch: {reductionPct:F1}%";
        TxtStatus.Text = $"Querschnittsverminderung: {reductionPct:F1}%";

        // V4.3: Subject-Auswahl fuer VsaFinding.MeasurementSubject
        _crossSectionSubject = AskCrossSectionSubject();
        if (_crossSectionSubject is not null)
            TxtStatus.Text = $"Querschnittsverminderung: {reductionPct:F1}% — {_crossSectionSubject}";
    }

    /// <summary>
    /// V4.3 — Fragt nach Polygon-Schluss welche Art von Querschnitts-Reduktion gemeint ist.
    /// Kleines Dialog-Fenster mit 4 Buttons (Wurzel/Abplatzung/Fehlstelle/Sonstige).
    /// </summary>
    private string? AskCrossSectionSubject()
    {
        var dlg = new Window
        {
            Title = "Querschnitt — Art auswaehlen",
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };
        var stack = new StackPanel { Margin = new Thickness(12) };
        stack.Children.Add(new TextBlock
        {
            Text = "Was wird hier vermessen?",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        });

        string? selected = "Sonstige";
        void AddButton(string label, string value)
        {
            var b = new Button
            {
                Content = label,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            b.Click += (_, _) => { selected = value; dlg.DialogResult = true; };
            stack.Children.Add(b);
        }
        AddButton("Wurzel", "Wurzel");
        AddButton("Abplatzung", "Abplatzung");
        AddButton("Fehlstelle", "Fehlstelle");
        AddButton("Sonstige Querschnittsreduktion", "Sonstige");

        dlg.Content = stack;
        dlg.ShowDialog();
        return selected;
    }
}
