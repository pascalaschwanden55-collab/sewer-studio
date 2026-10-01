using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PhotoAnnotations;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.PhotoMeasurement;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// PhotoAssistant: Messwerkzeuge auf statischem Foto (WinCan-Stil).
/// Alle Overlays verwenden normierte Koordinaten (0–1) mit Letterbox-Korrektur.
/// </summary>
public partial class PhotoMeasurementWindow : Window
{
    // --- Zustand ---
    private readonly string _photoPath;
    private readonly OverlayToolService _overlayService;
    private readonly IPhotoMeasurementOverlayExporter _overlayExporter;
    private PipeCalibration _calibration;
    private PhotoTool _activeTool = PhotoTool.None;
    private LevelMode _activeLevelMode = LevelMode.Water;

    // Canvas-Tags fuer selektives Loeschen
    private const string TagPipeCircle = "pipe";
    private const string TagOverlay = "overlay";
    private const string TagPreview = "preview";
    private const string TagFill = "fill";

    // Statische gefrorene Brushes (vermeidet Allokationen bei Slider-Updates)
    private static readonly Brush WaterFillBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(100, 65, 105, 225)));
    private static readonly Brush DepositFillBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(100, 210, 105, 30)));
    private static readonly Brush ObstacleFillBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(100, 220, 20, 60)));
    private static readonly Brush LateralFillBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(50, 255, 0, 0)));
    private static readonly Brush PolygonFillBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(80, 147, 112, 219)));
    private static readonly Brush LabelBgBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)));
    private static Brush FreezeBrush(Brush b) { b.Freeze(); return b; }

    // Drag-Zustand
    private bool _isDragging;
    private Point _dragStart;       // Canvas-Koordinaten
    private Point _dragStartNorm;   // Normierte Koordinaten
    private bool _isDraggingPipe;   // Pipe-Kreis wird verschoben

    // Multi-Punkt (Deformation: 4 Punkte, Polygon: N Punkte)
    private readonly List<NormalizedPoint> _clickPoints = new();
    private readonly List<UIElement> _clickMarkers = new();
    // Undo-Stack: jeder Frame ist eine Liste von Elementen die zusammen entfernt werden
    private readonly Stack<List<UIElement>> _undoFrames = new();

    // Polygon (Querschnitt)
    private bool _polygonClosed;

    // Bild-Seitenverhaeltnis (Breite/Hoehe) fuer Aspect-Ratio-Korrektur
    private double _imageAspect = 1.0;

    // Ergebnis
    private OverlayGeometry? _currentGeometry;

    /// <summary>Messergebnis (nach OK).</summary>
    public PhotoMeasurementResult Result { get; private set; } = new();

    // Kamera-Hoehe in % (50 = mittig)
    private double CameraHeightPercent => SliderCamera.Value;

    public PhotoMeasurementWindow(
        string photoPath,
        PipeCalibration? calibration,
        OverlayToolService? overlayService = null)
        : this(
            photoPath,
            calibration,
            overlayService,
            photoAnnotationUseCase: null,
            photoAnnotationContext: null)
    {
    }

    public PhotoMeasurementWindow(
        string photoPath,
        PipeCalibration? calibration,
        OverlayToolService? overlayService,
        IPhotoAnnotationUseCase? photoAnnotationUseCase,
        PhotoAnnotationCaptureContext? photoAnnotationContext)
    {
        InitializeComponent();
        _overlayExporter = new PhotoMeasurementOverlayExporter();
        _photoAnnotationUseCase = photoAnnotationUseCase;
        _photoAnnotationContext = photoAnnotationContext;

        _photoPath = photoPath;
        _calibration = calibration ?? new PipeCalibration
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.7,
            PipeCenter = new NormalizedPoint(0.5, 0.5)
        };
        _overlayService = overlayService ?? new OverlayToolService();
        _overlayService.SetCalibration(_calibration);

        // Foto laden
        LoadPhoto(photoPath);

        // DN anzeigen
        UpdateDnInfo();

        // Tool-Buttons Radio-Verhalten
        foreach (var btn in GetToolButtons())
        {
            btn.Checked += ToolButton_Checked;
            btn.Unchecked += ToolButton_Unchecked;
        }

        Closed += (_, _) => CancelPhotoAnnotationWork();
    }

    // ═══════════════════════════════════════════════
    // Koordinaten-System (Letterbox-Korrektur)
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Berechnet das tatsaechlich gerenderte Bild-Rechteck innerhalb des Image-Controls.
    /// Stretch="Uniform" erzeugt Letterboxing — Overlays muessen auf diesen Bereich
    /// normiert werden, nicht auf die gesamte Control-Groesse.
    /// </summary>
    public static Rect GetImageRenderedRect(Image imageControl)
    {
        if (imageControl.Source is not BitmapSource src)
            return new Rect(imageControl.RenderSize);

        double controlW = imageControl.ActualWidth;
        double controlH = imageControl.ActualHeight;
        double imgW = src.PixelWidth;
        double imgH = src.PixelHeight;

        // Reine Letterbox-Mathe an PhotoMeasurementGeometryService delegieren
        var (offsetX, offsetY, renderedW, renderedH) =
            PhotoMeasurementGeometryService.LetterboxRect(controlW, controlH, imgW, imgH);

        return new Rect(offsetX, offsetY, renderedW, renderedH);
    }

    /// <summary>Normierte Koordinate (0–1) → Canvas-Pixel.</summary>
    private Point NormToCanvas(double nx, double ny)
    {
        var r = GetImageRenderedRect(PhotoImage);
        return new Point(r.X + nx * r.Width, r.Y + ny * r.Height);
    }

    /// <summary>Canvas-Pixel → Normierte Koordinate.</summary>
    private Point CanvasToNorm(double cx, double cy)
    {
        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return new Point(0.5, 0.5);
        return new Point((cx - r.X) / r.Width, (cy - r.Y) / r.Height);
    }

    /// <summary>Prueft ob Canvas-Punkt im gerenderten Bild-Bereich liegt.</summary>
    private bool IsInsideImage(double cx, double cy)
    {
        var r = GetImageRenderedRect(PhotoImage);
        return r.Contains(new Point(cx, cy));
    }

    // ═══════════════════════════════════════════════
    // Foto laden
    // ═══════════════════════════════════════════════

    private void LoadPhoto(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            PhotoImage.Source = bmp;
            // Seitenverhaeltnis berechnen (fuer Aspect-Ratio-Korrektur bei Distanzen/Flaechen)
            if (bmp.PixelHeight > 0)
                _imageAspect = (double)bmp.PixelWidth / bmp.PixelHeight;
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Fehler beim Laden: "
                             + UserError.DescribeAndReport(ex, "Messfoto laden");
        }
    }

    // ═══════════════════════════════════════════════
    // Tool-Button Verwaltung
    // ═══════════════════════════════════════════════

    private IEnumerable<ToggleButton> GetToolButtons() => new ToggleButton[]
    {
        BtnToolCalib, BtnToolMarkRect, BtnToolRuler,
        BtnToolWater, BtnToolDeposit, BtnToolObstacle,
        BtnToolDeform, BtnToolCrossSection,
        BtnToolLateral, BtnToolBend, BtnToolConnection
    };

    private void ToolButton_Checked(object sender, RoutedEventArgs e)
    {
        // Radio-Verhalten: alle anderen unchecken
        foreach (var btn in GetToolButtons())
            if (btn != sender) btn.IsChecked = false;

        // Zustand zuruecksetzen
        ResetPhotoAnnotation();
        ClearOverlay();
        _clickPoints.Clear();
        _clickMarkers.Clear();
        _undoFrames.Clear();
        _polygonClosed = false;
        _currentGeometry = null;

        // Werkzeug bestimmen
        _activeTool = sender switch
        {
            var b when b == BtnToolCalib => PhotoTool.Calibration,
            var b when b == BtnToolMarkRect => PhotoTool.MarkRect,
            var b when b == BtnToolWater => PhotoTool.LevelWater,
            var b when b == BtnToolDeposit => PhotoTool.LevelDeposit,
            var b when b == BtnToolObstacle => PhotoTool.LevelObstacle,
            var b when b == BtnToolDeform => PhotoTool.Deformation,
            var b when b == BtnToolRuler => PhotoTool.Ruler,
            var b when b == BtnToolCrossSection => PhotoTool.CrossSection,
            var b when b == BtnToolLateral => PhotoTool.Lateral,
            var b when b == BtnToolBend => PhotoTool.Bend,
            var b when b == BtnToolConnection => PhotoTool.Connection,
            _ => PhotoTool.None
        };

        var presentation = PhotoMeasurementToolPresentationPolicy.Build(
            _activeTool,
            _activeLevelMode,
            _calibration.IsCalibrated);
        _activeLevelMode = presentation.LevelMode;
        PanelFillSlider.Visibility = presentation.ShowLevelControls ? Visibility.Visible : Visibility.Collapsed;
        SliderCamera.Visibility = presentation.ShowLevelControls ? Visibility.Visible : Visibility.Collapsed;
        TxtCamLabel.Visibility = presentation.ShowLevelControls ? Visibility.Visible : Visibility.Collapsed;
        PanelAngle.Visibility = presentation.ShowAngleControls ? Visibility.Visible : Visibility.Collapsed;
        BtnUndo.Visibility = presentation.ShowUndo ? Visibility.Visible : Visibility.Collapsed;
        BtnDelete.Visibility = presentation.ShowDelete ? Visibility.Visible : Visibility.Collapsed;
        BtnOk.IsEnabled = presentation.IsOkEnabled;
        OverlayCanvas.Cursor = presentation.UseCrossCursor ? Cursors.Cross : Cursors.Arrow;

        // Rohrkreis zeichnen
        DrawPipeCircle();

        // Level-Slider initialisieren
        if (presentation.ResetLevelSliders)
        {
            SliderFill.Value = 0;
            SliderCamera.Value = 50;
        }

        // Winkel-Slider initialisieren
        if (presentation.ResetAngleSliders)
        {
            SliderPosition.Value = 0;
            SliderAngle.Value = 45;
            UpdateAngleOverlay();
        }

        UpdateStatus();
    }

    private void ToolButton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (GetToolButtons().All(b => b.IsChecked != true))
        {
            _activeTool = PhotoTool.None;
            PanelFillSlider.Visibility = Visibility.Collapsed;
            SliderCamera.Visibility = Visibility.Collapsed;
            TxtCamLabel.Visibility = Visibility.Collapsed;
            PanelAngle.Visibility = Visibility.Collapsed;
            BtnUndo.Visibility = Visibility.Collapsed;
            BtnDelete.Visibility = Visibility.Collapsed;
            OverlayCanvas.Cursor = Cursors.Arrow;
            ResetPhotoAnnotation();
            ClearOverlay();
            UpdateStatus();
        }
    }

    // ═══════════════════════════════════════════════
    // Canvas-Maus-Handler
    // ═══════════════════════════════════════════════

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_activeTool == PhotoTool.None) return;
        var pos = e.GetPosition(OverlayCanvas);
        if (!IsInsideImage(pos.X, pos.Y)) return;

        var norm = CanvasToNorm(pos.X, pos.Y);

        // --- Pipe-Kreis verschieben: bei ALLEN Tools, wenn Klick nahe am Center ---
        if (_activeTool != PhotoTool.Calibration)
        {
            var imgRect = GetImageRenderedRect(PhotoImage);
            var pipePlan = PhotoMeasurementGeometryService.BuildPipeCirclePlan(
                _calibration,
                imgRect.X,
                imgRect.Y,
                imgRect.Width,
                imgRect.Height);
            // Nahe am Center (< 20% des Radius) = Pipe verschieben
            if (pipePlan is not null &&
                PhotoMeasurementGeometryService.IsInsidePipeCenterHitArea(pipePlan, pos.X, pos.Y))
            {
                _isDraggingPipe = true;
                OverlayCanvas.CaptureMouse();
                return;
            }
        }

        // --- Tool-spezifische Aktionen ---
        switch (_activeTool)
        {
            case PhotoTool.Calibration:
            case PhotoTool.MarkRect:
            case PhotoTool.Ruler:
            case PhotoTool.Connection:
                if (_activeTool == PhotoTool.MarkRect)
                {
                    // Eine neue Hand-Box entwertet die vorherige Maske sofort.
                    ResetPhotoAnnotation();
                    ClearByTag(TagOverlay);
                }
                _isDragging = true;
                _dragStart = pos;
                _dragStartNorm = norm;
                OverlayCanvas.CaptureMouse();
                break;

            case PhotoTool.Deformation:
                AddDeformationPoint(new NormalizedPoint(norm.X, norm.Y));
                break;

            case PhotoTool.CrossSection:
                if (!_polygonClosed)
                    AddPolygonPoint(new NormalizedPoint(norm.X, norm.Y));
                break;

            // Level/Lateral/Bend: kein Klick-Aktion (nur Slider + Pipe-Drag)
            default:
                break;
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingPipe)
        {
            _isDraggingPipe = false;
            OverlayCanvas.ReleaseMouseCapture();
            return;
        }

        if (!_isDragging) return;
        _isDragging = false;
        OverlayCanvas.ReleaseMouseCapture();

        var pos = e.GetPosition(OverlayCanvas);
        var norm = CanvasToNorm(pos.X, pos.Y);

        ClearByTag(TagPreview);

        switch (_activeTool)
        {
            case PhotoTool.Calibration:
                FinalizeCalibration(
                    new NormalizedPoint(_dragStartNorm.X, _dragStartNorm.Y),
                    new NormalizedPoint(norm.X, norm.Y));
                break;

            case PhotoTool.MarkRect:
                FinalizeMarkRect(
                    new NormalizedPoint(_dragStartNorm.X, _dragStartNorm.Y),
                    new NormalizedPoint(norm.X, norm.Y));
                break;

            case PhotoTool.Ruler:
            case PhotoTool.Connection:
                FinalizeLine(
                    new NormalizedPoint(_dragStartNorm.X, _dragStartNorm.Y),
                    new NormalizedPoint(norm.X, norm.Y));
                break;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);

        if (_isDraggingPipe)
        {
            var norm = CanvasToNorm(pos.X, pos.Y);
            _overlayService.MovePipeCircle(new NormalizedPoint(norm.X, norm.Y));
            _calibration = _overlayService.Calibration!;
            DrawPipeCircle();
            // Aktives Overlay aktualisieren (egal welches Tool)
            bool isLevel = _activeTool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
            if (isLevel) UpdateLevelOverlay();
            bool isAngle = _activeTool is PhotoTool.Lateral or PhotoTool.Bend;
            if (isAngle) UpdateAngleOverlay();
            return;
        }

        if (!_isDragging) return;

        // Drag-Vorschau zeichnen
        ClearByTag(TagPreview);

        if (_activeTool == PhotoTool.MarkRect)
        {
            // Rechteck-Vorschau
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Abs(pos.X - _dragStart.X),
                Height = Math.Abs(pos.Y - _dragStart.Y),
                Stroke = Brushes.LimeGreen,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
                Tag = TagPreview
            };
            Canvas.SetLeft(rect, Math.Min(_dragStart.X, pos.X));
            Canvas.SetTop(rect, Math.Min(_dragStart.Y, pos.Y));
            OverlayCanvas.Children.Add(rect);
        }
        else
        {
            var line = new Line
            {
                X1 = _dragStart.X, Y1 = _dragStart.Y,
                X2 = pos.X, Y2 = pos.Y,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Tag = TagPreview
            };
            OverlayCanvas.Children.Add(line);
        }

        // Live-Laenge anzeigen (Aspect-Ratio-korrigiert)
        var normStart = new NormalizedPoint(_dragStartNorm.X, _dragStartNorm.Y);
        var normEndPt = CanvasToNorm(pos.X, pos.Y);
        var normEnd = new NormalizedPoint(normEndPt.X, normEndPt.Y);
        double normLen = PipeCalibration.AspectCorrectedDistance(normStart, normEnd, _imageAspect);

        if (_activeTool == PhotoTool.Calibration)
        {
            TxtMeasureInfo.Text = $"Linie: {normLen:F3}";
        }
        else if (_calibration.IsCalibrated)
        {
            double mm = _calibration.NormToMm(normLen);
            TxtMeasureInfo.Text = $"{mm:F1} mm";
        }
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mausrad: Rohrkreis-Groesse aendern (bei Level, Deformation, Querschnitt)
        if (_activeTool == PhotoTool.None || _activeTool == PhotoTool.Calibration) return;

        double delta = e.Delta > 0 ? 0.02 : -0.02;
        _overlayService.ResizePipeCircle(delta);
        _calibration = _overlayService.Calibration!;
        DrawPipeCircle();

        bool isLevel = _activeTool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
        if (isLevel) UpdateLevelOverlay();

        bool isAngle = _activeTool is PhotoTool.Lateral or PhotoTool.Bend;
        if (isAngle) UpdateAngleOverlay();
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        // Drag abbrechen wenn Maus den Canvas verlaesst
        if (_isDragging)
        {
            _isDragging = false;
            OverlayCanvas.ReleaseMouseCapture();
            ClearByTag(TagPreview);
        }
        if (_isDraggingPipe)
        {
            _isDraggingPipe = false;
            OverlayCanvas.ReleaseMouseCapture();
        }
    }

    // Doppelklick fuer Polygon-Schluss
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_activeTool == PhotoTool.CrossSection && !_polygonClosed && _clickPoints.Count >= 3)
        {
            ClosePolygon();
        }
    }

    // ═══════════════════════════════════════════════
    // Kalibrierung
    // ═══════════════════════════════════════════════

    private void FinalizeCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        var geometry = PhotoMeasurementGeometryService.BuildCalibrationGeometry(start, end, _imageAspect);
        if (geometry is null) return;

        _calibration.NormalizedDiameter = geometry.NormalizedDiameter;
        _calibration.PipeCenter = geometry.PipeCenter;
        _calibration.WasManuallyCalibrated = true;
        _calibration.Source = CalibrationSource.Manual;   // manuelle Referenzlinie = verlaesslich

        _overlayService.SetCalibration(_calibration);
        DrawPipeCircle();
        UpdateDnInfo();

        // OK bei mm-Werkzeugen aktivieren
        BtnOk.IsEnabled = true;

        TxtMeasureInfo.Text = $"Kalibriert: {geometry.NormalizedDiameter:F3}";
        TxtStatus.Text = "Kalibrierung abgeschlossen. Rohrkreis angepasst.";
    }

    // ═══════════════════════════════════════════════
    // Markierung (Rechteck fuer KI-Training)
    // ═══════════════════════════════════════════════

    private void FinalizeMarkRect(NormalizedPoint start, NormalizedPoint end)
    {
        var geometry = PhotoMeasurementGeometryService.BuildMarkRectangleGeometry(start, end);
        if (geometry is null) return;

        _currentGeometry = geometry;

        RenderPhotoMarkRectangle(geometry);
        TxtMeasureInfo.Text = "Markiert";
        TxtStatus.Text = "Bereich markiert. OK = übernehmen.";
        _ = SegmentPhotoMarkAsync(geometry);
    }

    // ═══════════════════════════════════════════════
    // Lineal / Anschluss (Drag-Linie)
    // ═══════════════════════════════════════════════

    private void FinalizeLine(NormalizedPoint start, NormalizedPoint end)
    {
        var toolType = _activeTool == PhotoTool.Connection
            ? OverlayToolType.Line
            : OverlayToolType.Ruler;
        var lineGeometry = PhotoMeasurementGeometryService.BuildLineGeometry(
            toolType,
            start,
            end,
            _calibration,
            _imageAspect);
        if (lineGeometry is null) return;

        _currentGeometry = lineGeometry.Geometry;
        double mm = lineGeometry.Millimeters;

        // Overlay zeichnen
        ClearByTag(TagOverlay);
        var p1 = NormToCanvas(start.X, start.Y);
        var p2 = NormToCanvas(end.X, end.Y);

        var line = new Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
            Stroke = Brushes.Lime, StrokeThickness = 2,
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(line);

        // Label
        AddCanvasLabel($"{mm:F1} mm", (p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2 - 16, TagOverlay);

        TxtMeasureInfo.Text = $"{mm:F1} mm";
        TxtStatus.Text = _activeTool == PhotoTool.Connection
            ? $"Anschluss: {mm:F1} mm" : $"Distanz: {mm:F1} mm";
    }

    // ═══════════════════════════════════════════════
    // OK / Abbrechen / Undo / Loeschen
    // ═══════════════════════════════════════════════

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!CanCompletePhotoAnnotation())
            return;

        Result = PhotoMeasurementCompletionWorkflow.Execute(
            new PhotoMeasurementCompletionRequest(_currentGeometry, _calibration),
            new PhotoMeasurementCompletionActions(
                ExportOverlayPhoto: () => _overlayExporter.Export(
                    PhotoImage.Source as BitmapSource,
                    OverlayCanvas,
                    GetImageRenderedRect(PhotoImage),
                    _photoPath),
                DescribeExportError: ex => UserError.DescribeAndReport(
                    ex,
                    "Messfoto-Overlay exportieren"),
                ShowStatus: status => TxtStatus.Text = status));
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        ResetPhotoAnnotation();
        DialogResult = false;
        Close();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => UndoLastPoint();

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        ResetPhotoAnnotation();
        ClearOverlay();
        _clickPoints.Clear();
        _clickMarkers.Clear();
        _undoFrames.Clear();
        _polygonClosed = false;
        _currentGeometry = null;
        TxtMeasureInfo.Text = "";
        DrawPipeCircle();
        UpdateStatus();
    }

    private void UndoLastPoint()
    {
        if (_undoFrames.TryPop(out var frame))
        {
            foreach (var el in frame)
                OverlayCanvas.Children.Remove(el);
        }

        if (_clickPoints.Count > 0)
            _clickPoints.RemoveAt(_clickPoints.Count - 1);
        if (_clickMarkers.Count > 0)
            _clickMarkers.RemoveAt(_clickMarkers.Count - 1);
    }

    // ═══════════════════════════════════════════════
    // Keyboard
    // ═══════════════════════════════════════════════

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                BtnCancel_Click(sender, e);
                break;
            case Key.Enter:
                if (BtnOk.IsEnabled)
                    BtnOk_Click(sender, e);
                break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                UndoLastPoint();
                break;
            case Key.Delete:
                BtnDelete_Click(sender, e);
                break;
        }
    }

    // ═══════════════════════════════════════════════
    // Resize / Status
    // ═══════════════════════════════════════════════

    private void PhotoContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Alle Overlays neu zeichnen nach Resize
        DrawPipeCircle();

        bool isLevel = _activeTool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
        if (isLevel) UpdateLevelOverlay();

        bool isAngle = _activeTool is PhotoTool.Lateral or PhotoTool.Bend;
        if (isAngle) UpdateAngleOverlay();

        // Klick-Punkte (Deformation/Polygon) neu positionieren
        if (_clickPoints.Count > 0 && _activeTool is PhotoTool.Deformation or PhotoTool.CrossSection)
            RedrawClickPointOverlays();

        RenderPhotoAnnotationAfterResize();
    }

    /// <summary>Zeichnet Klick-Punkt-Marker und -Linien nach Resize neu.</summary>
    private void RedrawClickPointOverlays()
    {
        // Alte Marker + Linien entfernen
        ClearByTag(TagOverlay);
        _clickMarkers.Clear();
        _undoFrames.Clear();

        for (int i = 0; i < _clickPoints.Count; i++)
        {
            var pt = _clickPoints[i];
            var canvasPos = NormToCanvas(pt.X, pt.Y);

            if (_activeTool == PhotoTool.Deformation)
            {
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
                AddCanvasLabel($"{i + 1}", canvasPos.X + 8, canvasPos.Y - 14, TagOverlay);
            }
            else // CrossSection
            {
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

                if (i > 0)
                {
                    var prev = _clickPoints[i - 1];
                    var prevCanvas = NormToCanvas(prev.X, prev.Y);
                    OverlayCanvas.Children.Add(new Line
                    {
                        X1 = prevCanvas.X, Y1 = prevCanvas.Y,
                        X2 = canvasPos.X, Y2 = canvasPos.Y,
                        Stroke = Brushes.MediumPurple, StrokeThickness = 1.5,
                        Tag = TagOverlay
                    });
                }
            }
        }
    }

    private void UpdateDnInfo()
    {
        TxtDnInfo.Text = _calibration.NominalDiameterMm > 0
            ? $"DN {_calibration.NominalDiameterMm}"
            : "DN —";
        if (_calibration.IsCalibrated)
            TxtDnInfo.Text += $"\n\u2300 {_calibration.NormalizedDiameter:F3}";
    }

    private void UpdateStatus()
    {
        TxtStatus.Text = PhotoMeasurementToolPresentationPolicy.Build(
            _activeTool,
            _activeLevelMode,
            _calibration.IsCalibrated).StatusText;
    }
}

/// <summary>Werkzeug-Typen im PhotoMeasurementWindow.</summary>
internal enum PhotoTool
{
    None,
    Calibration,
    MarkRect,
    LevelWater,
    LevelDeposit,
    LevelObstacle,
    Deformation,
    Ruler,
    CrossSection,
    Lateral,
    Bend,
    Connection
}
