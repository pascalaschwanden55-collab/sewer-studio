using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// ImageAnnotationWindow Ring-Modus: Konzentrische Kreise (Aussen + Innen)
// fuer Ring-Risse, plus klickbasierte SAM-Segmentierung pro Defekt-Punkt
// und automatisches BBox-Tile-Scanning. Aus dem Hauptdatei extrahiert
// (Slice 32a).
public partial class ImageAnnotationWindow
{
    private void ToggleRingMode_Click(object sender, RoutedEventArgs e) => EnterRingMode();

    private void EnterRingMode()
    {
        _ringState = RingState.WaitCenter;
        AnnotationCanvas.Cursor = Cursors.Cross;
        ClearDisplayCanvas();
        ClearRingOverlay();

        if (DataContext is ImageAnnotationViewModel vm)
        {
            vm.CurrentBbox = null;
            vm.ClearPointPrompts();
            vm.StatusText = "Ring-Modus: Klick = Zentrum setzen";
        }
    }

    private void HandleRingClick(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(AnnotationCanvas);
        e.Handled = true;

        switch (_ringState)
        {
            case RingState.WaitCenter:
                _ringCenter = pos;
                _ringOuterRadius = 0;
                _ringInnerRadius = 0;
                _ringState = RingState.SettingOuter;
                if (DataContext is ImageAnnotationViewModel vm1)
                    vm1.StatusText = "Ring-Modus: Maus bewegen = aeusserer Ring, Klick = fixieren";
                break;

            case RingState.SettingOuter:
                _ringOuterRadius = Math.Sqrt(
                    Math.Pow(pos.X - _ringCenter.X, 2) + Math.Pow(pos.Y - _ringCenter.Y, 2));
                if (_ringOuterRadius < 15) return;
                _ringState = RingState.SettingInner;
                if (DataContext is ImageAnnotationViewModel vm2)
                    vm2.StatusText = "Ring-Modus: Maus bewegen = innerer Ring, Klick = fixieren";
                break;

            case RingState.SettingInner:
                double r = Math.Sqrt(
                    Math.Pow(pos.X - _ringCenter.X, 2) + Math.Pow(pos.Y - _ringCenter.Y, 2));
                _ringInnerRadius = Math.Min(r, _ringOuterRadius - 5);
                _ringInnerRadius = Math.Max(_ringInnerRadius, 5);
                _ringState = RingState.Complete;
                UpdateRingPreview();
                FinalizeRing();
                break;

            case RingState.Complete:
                // Klick im Ring = Defekt markieren → SAM segmentiert mit Ring-Constraints
                HandleRingDefectClick(pos);
                break;
        }
    }

    /// <summary>
    /// Klick auf Schaden im Ring: Erzeugt automatisch eine kleine BBox um den
    /// Klickpunkt und sendet sie als BBox-Prompt an SAM (wie beim Rechteck-Zeichnen).
    /// BBox-Groesse = 1/3 der Ring-Breite — gross genug fuer typische Schaeden.
    /// </summary>
    private void HandleRingDefectClick(Point clickPos)
    {
        if (DataContext is not ImageAnnotationViewModel vm) return;

        var w = AnnotationCanvas.ActualWidth;
        var h = AnnotationCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Pruefen ob Klick im Annulus liegt
        double dist = Math.Sqrt(Math.Pow(clickPos.X - _ringCenter.X, 2) +
                                Math.Pow(clickPos.Y - _ringCenter.Y, 2));
        if (dist < _ringInnerRadius || dist > _ringOuterRadius)
        {
            vm.StatusText = "Klick liegt ausserhalb des Rings — bitte innerhalb klicken.";
            return;
        }

        // BBox-Groesse: 1/3 der Ring-Breite (Annulus-Dicke)
        double ringWidth = _ringOuterRadius - _ringInnerRadius;
        double boxHalf = Math.Max(ringWidth / 3.0, 15.0);

        // BBox um den Klickpunkt (in Canvas-Koordinaten)
        double bx1 = Math.Max(0, clickPos.X - boxHalf);
        double by1 = Math.Max(0, clickPos.Y - boxHalf);
        double bx2 = Math.Min(w, clickPos.X + boxHalf);
        double by2 = Math.Min(h, clickPos.Y + boxHalf);

        // Normierte BBox fuer Speicherung
        vm.CurrentBbox = new NormalizedBoundingBox
        {
            XCenter = clickPos.X / w,
            YCenter = clickPos.Y / h,
            Width = (bx2 - bx1) / w,
            Height = (by2 - by1) / h
        };

        vm.StatusText = "Segmentiere Klickposition...";

        // Klick-Marker + BBox-Vorschau anzeigen
        var marker = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brushes.Lime,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            Tag = "ring_click"
        };
        Canvas.SetLeft(marker, clickPos.X - 5);
        Canvas.SetTop(marker, clickPos.Y - 5);
        DisplayCanvas.Children.Add(marker);

        var preview = new Rectangle
        {
            Width = bx2 - bx1,
            Height = by2 - by1,
            Stroke = Brushes.Lime,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(25, 0, 255, 0)),
            IsHitTestVisible = false,
            Tag = "ring_click"
        };
        Canvas.SetLeft(preview, bx1);
        Canvas.SetTop(preview, by1);
        DisplayCanvas.Children.Add(preview);

        // SAM mit BBox aufrufen (gleicher Weg wie Rechteck-Zeichnung)
        _ = SegmentRingClickAsync(vm);
    }

    private async Task SegmentRingClickAsync(ImageAnnotationViewModel vm)
    {
        // SAM mit BBox (nicht Punkt-Prompts!) — funktioniert zuverlaessig
        await vm.SegmentWithSamAsync();

        if (vm.CurrentSamResult is { Masks.Count: > 0 })
        {
            // V4.3 Ring-Fix: im Ring-Modus NICHT die vorherigen Masken loeschen,
            // damit der User mehrere Ring-Klicks visuell sieht. Klick-Marker (Punkte+Vorschau)
            // entfernen wir schon, aber die fixierten SAM-Masken bleiben.
            var oldClicks = DisplayCanvas.Children.OfType<FrameworkElement>()
                .Where(e => "ring_click".Equals(e.Tag)).ToList();
            foreach (var c in oldClicks) DisplayCanvas.Children.Remove(c);

            // NEU: Masken nicht mehr loeschen — sie sollen alle sichtbar bleiben
            SamMaskRenderer.RenderMasks(
                DisplayCanvas, vm.CurrentSamResult, [],
                DisplayCanvas.ActualWidth, DisplayCanvas.ActualHeight);

            // V4.3: Auto-Save pro Ring-Klick wenn VSA-Code gesetzt.
            // So kann der User klick-klick-klick fuer alle Schaeden im Ring machen.
            if (!string.IsNullOrWhiteSpace(vm.VsaCode))
            {
                vm.PreserveCodeAfterSave = true;
                try { await vm.SaveAnnotationCommand.ExecuteAsync(null); }
                finally { vm.PreserveCodeAfterSave = false; }
                vm.StatusText = $"Ring-Schaden {vm.AnnotatedCount} gespeichert — weiterer Klick = naechster Schaden (gleicher Code), Esc = Ring-Modus beenden";
            }
            else
            {
                vm.StatusText = $"Segment gefunden ({vm.CurrentSamResult.InferenceTimeMs:F0}ms) — " +
                                "VSA-Code eingeben dann Enter (oder weiter klicken nach Code-Eingabe)";
            }
        }
        else
        {
            vm.StatusText = "SAM konnte nichts segmentieren — anderen Punkt versuchen.";
        }
    }

    private void UpdateRingPreview()
    {
        ClearRingOverlay();

        // Aeusserer Ring (Cyan gestrichelt)
        _outerEllipse = new Ellipse
        {
            Width = _ringOuterRadius * 2,
            Height = _ringOuterRadius * 2,
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([6, 3]),
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_outerEllipse, _ringCenter.X - _ringOuterRadius);
        Canvas.SetTop(_outerEllipse, _ringCenter.Y - _ringOuterRadius);
        DisplayCanvas.Children.Add(_outerEllipse);

        // Innerer Ring (Rot gestrichelt) — nur wenn > 0
        if (_ringInnerRadius > 0)
        {
            _innerEllipse = new Ellipse
            {
                Width = _ringInnerRadius * 2,
                Height = _ringInnerRadius * 2,
                Stroke = new SolidColorBrush(Color.FromRgb(220, 40, 40)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection([6, 3]),
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_innerEllipse, _ringCenter.X - _ringInnerRadius);
            Canvas.SetTop(_innerEllipse, _ringCenter.Y - _ringInnerRadius);
            DisplayCanvas.Children.Add(_innerEllipse);

            // Suchbereich leicht andeuten (sehr subtil, nicht gruen fuellen)
            var outer = new EllipseGeometry(_ringCenter, _ringOuterRadius, _ringOuterRadius);
            var inner = new EllipseGeometry(_ringCenter, _ringInnerRadius, _ringInnerRadius);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
            _ringFill = new Path
            {
                Data = combined,
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 180, 255)),  // Sehr subtiles Blau
                Stroke = Brushes.Transparent,
                IsHitTestVisible = false
            };
            DisplayCanvas.Children.Add(_ringFill);
        }

        // Zentrum-Marker
        var center = new Ellipse
        {
            Width = 8, Height = 8,
            Fill = Brushes.White,
            Stroke = Brushes.Cyan,
            StrokeThickness = 1,
            IsHitTestVisible = false,
            Tag = "ring_center"
        };
        Canvas.SetLeft(center, _ringCenter.X - 4);
        Canvas.SetTop(center, _ringCenter.Y - 4);
        DisplayCanvas.Children.Add(center);
    }

    private void FinalizeRing()
    {
        if (DataContext is not ImageAnnotationViewModel vm) return;

        var w = AnnotationCanvas.ActualWidth;
        var h = AnnotationCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // BBox aus aeusserem Ring (fuer Speicherung)
        double normCx = _ringCenter.X / w;
        double normCy = _ringCenter.Y / h;
        var bbox = new NormalizedBoundingBox
        {
            XCenter = normCx,
            YCenter = normCy,
            Width = Math.Min((_ringOuterRadius * 2) / w, 1.0),
            Height = Math.Min((_ringOuterRadius * 2) / h, 1.0)
        };
        vm.CurrentBbox = bbox;

        _ringState = RingState.Complete;
        vm.ClearPointPrompts();
        vm.StatusText = "Ring gesetzt — Klick auf Schaden im Ring segmentiert automatisch. Rechtsklick = verschieben.";
    }

    /// <summary>
    /// Annulus in BBox-Kacheln aufteilen und alle an SAM senden (Batch).
    /// Gleicher Mechanismus wie das kleine Rechteck — aber automatisch
    /// 12 Sektoren × 1 Kachel pro Sektor = 12 BBoxen ueber den ganzen Ring.
    /// </summary>
    private async Task ScanRingWithBBoxTilesAsync(ImageAnnotationViewModel vm, double canvasW, double canvasH)
    {
        vm.StatusText = "Ring-Scan: Annulus wird in Kacheln zerlegt...";

        var bmp = vm.CurrentImage;
        if (bmp is null) return;

        int imgW = bmp.PixelWidth, imgH = bmp.PixelHeight;

        // Ring-Koordinaten in Pixel umrechnen
        double cxPx = _ringCenter.X / canvasW * imgW;
        double cyPx = _ringCenter.Y / canvasH * imgH;
        double rInPx = _ringInnerRadius / canvasW * imgW;
        double rOutPx = _ringOuterRadius / canvasW * imgW;

        // Kachel-Groesse: Ring-Breite bestimmt die Kachelgroesse
        double ringWidth = rOutPx - rInPx;
        double tileSize = ringWidth * 0.9; // Kachel etwas kleiner als Ring-Breite
        double midR = (rInPx + rOutPx) / 2.0;

        // 16 Sektoren (alle 22.5°) — Kacheln entlang des Rings
        int numSectors = 16;
        var boxes = new List<SamBoundingBox>();

        for (int i = 0; i < numSectors; i++)
        {
            double angle = i * 2.0 * Math.PI / numSectors;
            double tileCx = cxPx + midR * Math.Cos(angle);
            double tileCy = cyPx + midR * Math.Sin(angle);

            double x1 = Math.Max(0, tileCx - tileSize / 2);
            double y1 = Math.Max(0, tileCy - tileSize / 2);
            double x2 = Math.Min(imgW, tileCx + tileSize / 2);
            double y2 = Math.Min(imgH, tileCy + tileSize / 2);

            if (x2 - x1 > 10 && y2 - y1 > 10)
            {
                boxes.Add(new SamBoundingBox(x1, y1, x2, y2,
                    $"ring_tile_{i}", 1.0));
            }
        }

        vm.StatusText = $"Ring-Scan: {boxes.Count} Kacheln → SAM segmentiert...";

        try
        {
            await vm.ScanRingWithBBoxesAsync(boxes);
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Ring-Scan Fehler: {ex.Message}";
            return;
        }

        if (vm.CurrentSamResult is { Masks.Count: > 0 })
        {
            SamMaskRenderer.ClearMasks(DisplayCanvas);
            SamMaskRenderer.RenderMasks(
                DisplayCanvas, vm.CurrentSamResult, [],
                DisplayCanvas.ActualWidth, DisplayCanvas.ActualHeight);
            vm.StatusText = $"Ring-Scan: {vm.CurrentSamResult.Masks.Count} Segment(e) " +
                            $"({vm.CurrentSamResult.InferenceTimeMs:F0}ms) — " +
                            "Klick im Ring = weitere Segmentierung, Rechtsklick = verschieben";
        }
        else
        {
            vm.StatusText = "Ring-Scan: Keine Segmente gefunden — Klick auf Schaden fuer manuelle Segmentierung.";
        }
    }
}
