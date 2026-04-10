// AuswertungPro – Bild-Bibliothek: Manuelles Annotieren
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services.CodeCatalog;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class ImageAnnotationWindow : Window
{
    private Point? _drawStart;
    private Rectangle? _previewRect;
    // Ring-Modus: zwei konzentrische Kreise, verschiebbar
    private enum RingState { None, WaitCenter, SettingOuter, SettingInner, Complete }
    private RingState _ringState = RingState.None;

    // Bogen-Modus: 3-Punkt-Bogen (Start, Kruemmung, Ende)
    private enum ArcState { None, WaitStart, WaitMid, WaitEnd, Complete }
    private ArcState _arcState = ArcState.None;
    private Point _arcStart;
    private Point _arcMid;
    private Point _arcEnd;
    private Path? _arcPath;
    private const double ArcThickness = 40.0; // Breite des Bogenstreifens in Canvas-Pixeln
    private Point _ringCenter;
    private double _ringOuterRadius;
    private double _ringInnerRadius;
    private Ellipse? _outerEllipse;
    private Ellipse? _innerEllipse;
    private Path? _ringFill;

    // Ring verschieben (Rechtsklick gehalten)
    private bool _ringDragging;
    private Point _ringDragStart;

    public ImageAnnotationWindow()
    {
        InitializeComponent();
        FrameImage.SizeChanged += (_, _) => UpdateCanvasViewport();
        Loaded += (_, _) =>
        {
            UpdateCanvasViewport();
            TxtCode.Focus();
        };
        DataContextChanged += OnDataContextChanged;
    }

    public ImageAnnotationWindow(ImageAnnotationViewModel vm) : this()
    {
        DataContext = vm;
    }

    // --- Ordner-Auswahl ---

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Bildordner waehlen (PNG/JPG)"
        };

        if (dlg.ShowDialog() == true && DataContext is ImageAnnotationViewModel vm)
            vm.LoadFolder(dlg.FolderName);
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageAnnotationViewModel vm)
            vm.NavigatePreviousCommand.Execute(null);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageAnnotationViewModel vm)
            vm.NavigateNextCommand.Execute(null);
    }

    // --- Tastatur-Shortcuts ---

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not ImageAnnotationViewModel vm) return;

        // Nicht abfangen wenn in einer TextBox getippt wird (ausser Enter/Escape/Pfeile)
        bool inTextBox = e.OriginalSource is TextBox;

        switch (e.Key)
        {
            case Key.Enter:
                vm.SaveAnnotationCommand.Execute(null);
                TxtCode.Focus();
                TxtCode.SelectAll();
                e.Handled = true;
                break;

            case Key.N when !inTextBox:
                vm.MarkNoFindingCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.S when !inTextBox:
                vm.SkipImageCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Left when !inTextBox:
                vm.NavigatePreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right when !inTextBox:
                vm.NavigateNextCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.C when !inTextBox:
                OpenCodePicker();
                e.Handled = true;
                break;

            case Key.G when !inTextBox:
                SelectFullImage();
                e.Handled = true;
                break;

            case Key.R when !inTextBox:
                EnterRingMode();
                e.Handled = true;
                break;

            case Key.B when !inTextBox:
                EnterArcMode();
                e.Handled = true;
                break;

            case Key.Escape:
                // Alle Modi verlassen
                _ringState = RingState.None;
                _arcState = ArcState.None;
                AnnotationCanvas.Cursor = Cursors.Cross;
                // Alles loeschen
                vm.CurrentBbox = null;
                vm.ClearPointPrompts();
                ClearRingOverlay();
                ClearArcOverlay();
                ClearDisplayCanvas();
                SamMaskRenderer.ClearMasks(DisplayCanvas);
                vm.StatusText = "Rechteck-Modus aktiv.";
                e.Handled = true;
                break;
        }
    }

    // --- ViewModel-Property-Aenderungen beobachten ---

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ImageAnnotationViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is ImageAnnotationViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageAnnotationViewModel.CurrentBbox))
        {
            // BBox geloescht (z.B. nach Speichern) → Canvas + SAM-Masken leeren
            if (sender is ImageAnnotationViewModel vm && vm.CurrentBbox is null)
            {
                SamMaskRenderer.ClearMasks(DisplayCanvas);
                ClearRingOverlay();
                ClearArcOverlay();
                ClearDisplayCanvas();

                _ringState = RingState.None;
                _arcState = ArcState.None;
                AnnotationCanvas.Cursor = Cursors.Cross;
            }
        }

        if (e.PropertyName == nameof(ImageAnnotationViewModel.CurrentImage))
        {
            // Bei Bildwechsel: alles leeren + Modi aus
            ClearRingOverlay();
            ClearArcOverlay();
            ClearDisplayCanvas();
            UpdateCanvasViewport();
            _ringState = RingState.None;
            _arcState = ArcState.None;
            AnnotationCanvas.Cursor = Cursors.Cross;
            TxtCode.Focus();
        }
    }

    // --- Code-Katalog oeffnen ---

    private void OpenCodePicker_Click(object sender, RoutedEventArgs e) => OpenCodePicker();

    private void OpenCodePicker()
    {
        if (DataContext is not ImageAnnotationViewModel vm) return;

        // VsaCodeExplorerWindow oeffnen (gleiche Struktur wie im Codiermodus)
        var entry = new ProtocolEntry();
        var explorerVm = new VsaCodeExplorerViewModel(entry);
        var dlg = new VsaCodeExplorerWindow(explorerVm) { Owner = this };

        if (dlg.ShowDialog() == true && dlg.SelectedEntry is { } result)
        {
            // Code uebernehmen
            vm.VsaCode = result.Code;

            // Severity mappen (low→2, mid→3, high→4)
            if (!string.IsNullOrWhiteSpace(result.CodeMeta?.Severity))
            {
                vm.SeverityText = result.CodeMeta.Severity switch
                {
                    "low" => "2",
                    "high" => "4",
                    _ => "3"
                };
            }

            // Uhrlage aus VsaCodeExplorerViewModel uebernehmen
            if (!string.IsNullOrWhiteSpace(explorerVm.ClockVon))
                vm.ClockText = explorerVm.ClockVon;

            // Notiz mit Beschreibung fuellen falls leer
            if (string.IsNullOrWhiteSpace(vm.Notiz) && !string.IsNullOrWhiteSpace(result.Beschreibung))
                vm.Notiz = result.Beschreibung;

            vm.StatusText = $"Code {result.Code} aus Katalog uebernommen.";
            TxtCode.Focus();
        }
    }

    // --- Ring-Modus ---

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
            // Vorherige Ring-Klick-Marker entfernen (nicht den Ring selbst)
            var oldClicks = DisplayCanvas.Children.OfType<FrameworkElement>()
                .Where(e => "ring_click".Equals(e.Tag)).ToList();
            foreach (var c in oldClicks) DisplayCanvas.Children.Remove(c);

            SamMaskRenderer.ClearMasks(DisplayCanvas);
            SamMaskRenderer.RenderMasks(
                DisplayCanvas, vm.CurrentSamResult, [],
                DisplayCanvas.ActualWidth, DisplayCanvas.ActualHeight);
            vm.StatusText = $"Segment gefunden ({vm.CurrentSamResult.InferenceTimeMs:F0}ms) — " +
                            "weiterer Klick = naechster Schaden, Enter = speichern";
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

    // --- Bogen-Modus ---

    private void ToggleArcMode_Click(object sender, RoutedEventArgs e) => EnterArcMode();

    private void EnterArcMode()
    {
        _arcState = ArcState.WaitStart;
        _ringState = RingState.None;
        AnnotationCanvas.Cursor = Cursors.Cross;
        ClearDisplayCanvas();
        ClearRingOverlay();
        ClearArcOverlay();

        if (DataContext is ImageAnnotationViewModel vm)
        {
            vm.CurrentBbox = null;
            vm.ClearPointPrompts();
            vm.VsaCode = "BCC";
            vm.StatusText = "Bogen-Modus: Klick = Startpunkt des Bogens setzen";
        }
    }

    private void HandleArcClick(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(AnnotationCanvas);
        e.Handled = true;

        switch (_arcState)
        {
            case ArcState.WaitStart:
                _arcStart = pos;
                _arcState = ArcState.WaitMid;
                AddArcPointMarker(pos, Brushes.Lime, "arc_pt");
                if (DataContext is ImageAnnotationViewModel vm1)
                    vm1.StatusText = "Bogen-Modus: Klick = Kruemmungspunkt (Mitte des Bogens)";
                break;

            case ArcState.WaitMid:
                _arcMid = pos;
                _arcState = ArcState.WaitEnd;
                AddArcPointMarker(pos, Brushes.Yellow, "arc_pt");
                if (DataContext is ImageAnnotationViewModel vm2)
                    vm2.StatusText = "Bogen-Modus: Klick = Endpunkt des Bogens";
                break;

            case ArcState.WaitEnd:
                _arcEnd = pos;
                _arcState = ArcState.Complete;
                AddArcPointMarker(pos, Brushes.OrangeRed, "arc_pt");
                UpdateArcPreview(_arcStart, _arcMid, _arcEnd);
                FinalizeArc();
                break;
        }
    }

    private void AddArcPointMarker(Point pos, Brush fill, string tag)
    {
        var marker = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            IsHitTestVisible = false,
            Tag = tag
        };
        Canvas.SetLeft(marker, pos.X - 5);
        Canvas.SetTop(marker, pos.Y - 5);
        DisplayCanvas.Children.Add(marker);
    }

    /// <summary>
    /// Zeichnet einen dicken Bogenstreifen durch 3 Punkte (quadratische Bezier-Kurve).
    /// Der Streifen hat eine feste Breite (ArcThickness) — gut sichtbar als Markierung.
    /// </summary>
    private void UpdateArcPreview(Point p1, Point p2, Point p3)
    {
        ClearArcOverlay();

        // Quadratische Bezier-Kurve durch 3 Punkte:
        // Kontrollpunkt so berechnen, dass die Kurve durch p2 geht
        // B(0.5) = p2  →  Ctrl = 2*p2 - 0.5*p1 - 0.5*p3
        var ctrl = new Point(
            2 * p2.X - 0.5 * p1.X - 0.5 * p3.X,
            2 * p2.Y - 0.5 * p1.Y - 0.5 * p3.Y);

        var pathFig = new PathFigure
        {
            StartPoint = p1,
            Segments = { new QuadraticBezierSegment(ctrl, p3, true) }
        };

        var pathGeo = new PathGeometry(new[] { pathFig });

        _arcPath = new Path
        {
            Data = pathGeo,
            Stroke = Brushes.Orange,
            StrokeThickness = ArcThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0.4,
            IsHitTestVisible = false,
            Tag = "arc_overlay"
        };
        DisplayCanvas.Children.Add(_arcPath);

        // Mittellinie (duenn, gut sichtbar)
        var centerLine = new Path
        {
            Data = pathGeo,
            Stroke = Brushes.Orange,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            IsHitTestVisible = false,
            Tag = "arc_overlay"
        };
        DisplayCanvas.Children.Add(centerLine);
    }

    private void FinalizeArc()
    {
        if (DataContext is not ImageAnnotationViewModel vm) return;

        var w = AnnotationCanvas.ActualWidth;
        var h = AnnotationCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // BBox aus den 3 Punkten + Dicke berechnen
        double minX = Math.Min(_arcStart.X, Math.Min(_arcMid.X, _arcEnd.X)) - ArcThickness / 2;
        double minY = Math.Min(_arcStart.Y, Math.Min(_arcMid.Y, _arcEnd.Y)) - ArcThickness / 2;
        double maxX = Math.Max(_arcStart.X, Math.Max(_arcMid.X, _arcEnd.X)) + ArcThickness / 2;
        double maxY = Math.Max(_arcStart.Y, Math.Max(_arcMid.Y, _arcEnd.Y)) + ArcThickness / 2;

        // Canvas-Grenzen einhalten
        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(w, maxX);
        maxY = Math.Min(h, maxY);

        double bboxW = (maxX - minX) / w;
        double bboxH = (maxY - minY) / h;

        if (bboxW < 0.02 || bboxH < 0.02) return;

        vm.CurrentBbox = new NormalizedBoundingBox
        {
            XCenter = ((minX + maxX) / 2.0) / w,
            YCenter = ((minY + maxY) / 2.0) / h,
            Width = bboxW,
            Height = bboxH
        };

        vm.StatusText = "Bogen gesetzt (BCC) — SAM segmentiert...";

        // SAM-Segmentierung starten
        _ = RunArcSamAsync(vm);
    }

    private async Task RunArcSamAsync(ImageAnnotationViewModel vm)
    {
        await vm.SegmentWithSamAsync();

        if (vm.CurrentSamResult is { Masks.Count: > 0 })
        {
            SamMaskRenderer.ClearMasks(DisplayCanvas);
            SamMaskRenderer.RenderMasks(
                DisplayCanvas, vm.CurrentSamResult, [],
                DisplayCanvas.ActualWidth, DisplayCanvas.ActualHeight);
            vm.StatusText = $"Bogen segmentiert ({vm.CurrentSamResult.InferenceTimeMs:F0}ms) — " +
                            "Enter = speichern, Esc = zuruecksetzen";
        }
        else
        {
            vm.StatusText = "SAM konnte Bogen nicht segmentieren — Enter speichert mit BBox.";
        }
    }

    private void ClearArcOverlay()
    {
        var toRemove = DisplayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => "arc_overlay".Equals(e.Tag) || "arc_pt".Equals(e.Tag)).ToList();
        foreach (var el in toRemove) DisplayCanvas.Children.Remove(el);
        _arcPath = null;
    }

    private void ClearRingOverlay()
    {
        if (_outerEllipse is not null) { DisplayCanvas.Children.Remove(_outerEllipse); _outerEllipse = null; }
        if (_innerEllipse is not null) { DisplayCanvas.Children.Remove(_innerEllipse); _innerEllipse = null; }
        if (_ringFill is not null) { DisplayCanvas.Children.Remove(_ringFill); _ringFill = null; }

        // Zentrum-Marker entfernen
        var markers = DisplayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => "ring_center".Equals(e.Tag)).ToList();
        foreach (var m in markers) DisplayCanvas.Children.Remove(m);
    }

    private void AnnotationCanvas_RightClick(object sender, MouseButtonEventArgs e)
    {
        // Ring verschieben: Rechtsklick gehalten im Complete-State
        if (_ringState == RingState.Complete)
        {
            _ringDragging = true;
            _ringDragStart = e.GetPosition(AnnotationCanvas);
            AnnotationCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void AnnotationCanvas_RightUp(object sender, MouseButtonEventArgs e)
    {
        if (!_ringDragging) return;
        _ringDragging = false;
        AnnotationCanvas.ReleaseMouseCapture();
        e.Handled = true;

        // BBox + SAM neu berechnen mit verschobenem Ring
        if (DataContext is ImageAnnotationViewModel vm && _ringState == RingState.Complete)
        {
            var w = AnnotationCanvas.ActualWidth;
            var h = AnnotationCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // BBox aktualisieren
            vm.CurrentBbox = new NormalizedBoundingBox
            {
                XCenter = _ringCenter.X / w,
                YCenter = _ringCenter.Y / h,
                Width = Math.Min((_ringOuterRadius * 2) / w, 1.0),
                Height = Math.Min((_ringOuterRadius * 2) / h, 1.0)
            };

            SamMaskRenderer.ClearMasks(DisplayCanvas);
            vm.ClearPointPrompts();
            vm.StatusText = "Ring verschoben — Klick auf Schaden zum Segmentieren.";
        }
    }

    // --- Ganzes Bild markieren ---

    private void SelectFullImage_Click(object sender, RoutedEventArgs e) => SelectFullImage();

    private void SelectFullImage()
    {
        if (DataContext is not ImageAnnotationViewModel vm || vm.CurrentImage is null) return;

        // BBox = ganzes Bild (Zentrum 0.5/0.5, Breite/Hoehe 1.0)
        var bbox = new NormalizedBoundingBox
        {
            XCenter = 0.5,
            YCenter = 0.5,
            Width = 1.0,
            Height = 1.0
        };

        vm.CurrentBbox = bbox;
        ClearDisplayCanvas();
        // Kein Rechteck rendern — SAM segmentiert das ganze Bild
        _ = RunSamSegmentationAsync(vm);
    }

    // --- Maus-Handler fuer Rechteck-Zeichnung ---

    private void AnnotationCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_ringState != RingState.None)
        {
            HandleRingClick(e);
            return;
        }

        if (_arcState != ArcState.None)
        {
            HandleArcClick(e);
            return;
        }

        _drawStart = e.GetPosition(AnnotationCanvas);
        AnnotationCanvas.CaptureMouse();

        _previewRect = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([4, 2]),
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
        };
        Canvas.SetLeft(_previewRect, _drawStart.Value.X);
        Canvas.SetTop(_previewRect, _drawStart.Value.Y);
        AnnotationCanvas.Children.Add(_previewRect);
    }

    private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Ring verschieben (Rechtsklick gehalten)
        if (_ringDragging && _ringState == RingState.Complete)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double dx = pos.X - _ringDragStart.X;
            double dy = pos.Y - _ringDragStart.Y;
            _ringCenter = new Point(_ringCenter.X + dx, _ringCenter.Y + dy);
            _ringDragStart = pos;
            UpdateRingPreview();
            return;
        }

        // Bogen-Modus: Live-Vorschau bei Kruemmungspunkt und Endpunkt
        if (_arcState == ArcState.WaitMid)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            UpdateArcPreview(_arcStart, pos, pos); // Vorschau: Start → Maus (als Bogen mit Maus als Mitte+Ende)
            return;
        }
        if (_arcState == ArcState.WaitEnd)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            UpdateArcPreview(_arcStart, _arcMid, pos);
            return;
        }

        // Ring-Modus: Vorschau aktualisieren
        if (_ringState == RingState.SettingOuter || _ringState == RingState.SettingInner)
        {
            var pos = e.GetPosition(AnnotationCanvas);
            double radius = Math.Sqrt(
                Math.Pow(pos.X - _ringCenter.X, 2) + Math.Pow(pos.Y - _ringCenter.Y, 2));

            if (_ringState == RingState.SettingOuter)
            {
                _ringOuterRadius = radius;
                UpdateRingPreview();
            }
            else if (_ringState == RingState.SettingInner)
            {
                // Innerer Ring darf nicht groesser als aeusserer sein
                _ringInnerRadius = Math.Min(radius, _ringOuterRadius - 5);
                _ringInnerRadius = Math.Max(_ringInnerRadius, 5);
                UpdateRingPreview();
            }
            return;
        }

        if (_drawStart is null || _previewRect is null) return;

        var p = e.GetPosition(AnnotationCanvas);
        Canvas.SetLeft(_previewRect, Math.Min(_drawStart.Value.X, p.X));
        Canvas.SetTop(_previewRect, Math.Min(_drawStart.Value.Y, p.Y));
        _previewRect.Width = Math.Abs(p.X - _drawStart.Value.X);
        _previewRect.Height = Math.Abs(p.Y - _drawStart.Value.Y);
    }

    private void AnnotationCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        AnnotationCanvas.ReleaseMouseCapture();
        if (_drawStart is null || _previewRect is null) return;

        var end = e.GetPosition(AnnotationCanvas);
        AnnotationCanvas.Children.Remove(_previewRect);
        _previewRect = null;

        var w = AnnotationCanvas.ActualWidth;
        var h = AnnotationCanvas.ActualHeight;
        if (w <= 0 || h <= 0) { _drawStart = null; return; }

        var x1 = Math.Clamp(_drawStart.Value.X / w, 0, 1);
        var y1 = Math.Clamp(_drawStart.Value.Y / h, 0, 1);
        var x2 = Math.Clamp(end.X / w, 0, 1);
        var y2 = Math.Clamp(end.Y / h, 0, 1);

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var width = Math.Abs(x2 - x1);
        var height = Math.Abs(y2 - y1);
        _drawStart = null;

        if (width < 0.01 || height < 0.01) return;

        var bbox = new NormalizedBoundingBox
        {
            XCenter = left + width / 2,
            YCenter = top + height / 2,
            Width = width,
            Height = height
        };

        if (DataContext is ImageAnnotationViewModel vm)
        {
            vm.CurrentBbox = bbox;
            ClearDisplayCanvas();
            RenderAnnotationRect(bbox);

            // SAM-Segmentierung im Hintergrund starten
            _ = RunSamSegmentationAsync(vm);
        }
    }

    /// <summary>Ruft SAM auf und ersetzt das Rechteck durch die praezise Maske.</summary>
    private async Task RunSamSegmentationAsync(ImageAnnotationViewModel vm)
    {
        await vm.SegmentWithSamAsync();

        if (vm.CurrentSamResult is { Masks.Count: > 0 })
        {
            ClearDisplayCanvas();
            SamMaskRenderer.RenderMasks(
                DisplayCanvas,
                vm.CurrentSamResult,
                [], // Keine Quantifizierung noetig beim Annotieren
                DisplayCanvas.ActualWidth,
                DisplayCanvas.ActualHeight);
        }
    }

    // --- Anzeige-Hilfsmethoden ---

    private void RenderAnnotationRect(NormalizedBoundingBox bbox)
    {
        var w = DisplayCanvas.ActualWidth;
        var h = DisplayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rect = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([6, 3]),
            Fill = new SolidColorBrush(Color.FromArgb(25, 0, 255, 255)),
            Width = bbox.Width * w,
            Height = bbox.Height * h
        };
        Canvas.SetLeft(rect, (bbox.XCenter - bbox.Width / 2) * w);
        Canvas.SetTop(rect, (bbox.YCenter - bbox.Height / 2) * h);
        DisplayCanvas.Children.Add(rect);
    }

    private void ClearDisplayCanvas() => DisplayCanvas.Children.Clear();

    /// <summary>
    /// Passt Canvas auf die tatsaechliche Bildflaeche an (Aspect-Ratio-korrigiert).
    /// </summary>
    private void UpdateCanvasViewport()
    {
        if (FrameImage.Source is not BitmapSource bmp) return;

        double containerW = FrameImage.ActualWidth;
        double containerH = FrameImage.ActualHeight;
        if (containerW <= 0 || containerH <= 0) return;

        double imgAspect = (double)bmp.PixelWidth / bmp.PixelHeight;
        double containerAspect = containerW / containerH;

        double renderW, renderH;
        if (imgAspect > containerAspect)
        {
            renderW = containerW;
            renderH = containerW / imgAspect;
        }
        else
        {
            renderH = containerH;
            renderW = containerH * imgAspect;
        }

        DisplayCanvas.Width = renderW;
        DisplayCanvas.Height = renderH;
        AnnotationCanvas.Width = renderW;
        AnnotationCanvas.Height = renderH;
    }
}
