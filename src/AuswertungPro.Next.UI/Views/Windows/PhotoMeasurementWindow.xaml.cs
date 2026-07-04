using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

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

    public PhotoMeasurementWindow(string photoPath, PipeCalibration? calibration,
                                   OverlayToolService? overlayService = null)
    {
        InitializeComponent();

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
            TxtStatus.Text = $"Fehler beim Laden: {ex.Message}";
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

        // LevelMode setzen
        _activeLevelMode = _activeTool switch
        {
            PhotoTool.LevelWater => LevelMode.Water,
            PhotoTool.LevelDeposit => LevelMode.Deposit,
            PhotoTool.LevelObstacle => LevelMode.Obstacle,
            _ => _activeLevelMode
        };

        // Sichtbarkeiten
        bool isLevel = _activeTool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
        PanelFillSlider.Visibility = isLevel ? Visibility.Visible : Visibility.Collapsed;
        SliderCamera.Visibility = isLevel ? Visibility.Visible : Visibility.Collapsed;
        TxtCamLabel.Visibility = isLevel ? Visibility.Visible : Visibility.Collapsed;

        bool isAngle = _activeTool is PhotoTool.Lateral or PhotoTool.Bend;
        PanelAngle.Visibility = isAngle ? Visibility.Visible : Visibility.Collapsed;

        bool hasUndo = _activeTool is PhotoTool.Deformation or PhotoTool.CrossSection;
        BtnUndo.Visibility = hasUndo ? Visibility.Visible : Visibility.Collapsed;
        BtnDelete.Visibility = _activeTool != PhotoTool.None ? Visibility.Visible : Visibility.Collapsed;

        // OK-Button: bei mm-Werkzeugen nur wenn kalibriert
        bool needsCalib = _activeTool is PhotoTool.Ruler or PhotoTool.Connection;
        BtnOk.IsEnabled = !needsCalib || _calibration.IsCalibrated;

        // Cursor
        OverlayCanvas.Cursor = _activeTool switch
        {
            PhotoTool.None => Cursors.Arrow,
            PhotoTool.Deformation or PhotoTool.CrossSection => Cursors.Cross,
            _ => Cursors.Cross
        };

        // Rohrkreis zeichnen
        DrawPipeCircle();

        // Level-Slider initialisieren
        if (isLevel)
        {
            SliderFill.Value = 0;
            SliderCamera.Value = 50;
        }

        // Winkel-Slider initialisieren
        if (isAngle)
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

        // Overlay zeichnen
        ClearByTag(TagOverlay);
        double minX = geometry.Points[0].X, minY = geometry.Points[0].Y;
        double maxX = geometry.Points[2].X, maxY = geometry.Points[2].Y;
        var p1 = NormToCanvas(minX, minY);
        var p2 = NormToCanvas(maxX, maxY);

        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = p2.X - p1.X,
            Height = p2.Y - p1.Y,
            Stroke = Brushes.LimeGreen,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
            Tag = TagOverlay
        };
        Canvas.SetLeft(rect, p1.X);
        Canvas.SetTop(rect, p1.Y);
        OverlayCanvas.Children.Add(rect);

        TxtMeasureInfo.Text = "Markiert";
        TxtStatus.Text = "Bereich markiert. OK = übernehmen.";
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
    // Level-Werkzeuge (Slider-basiert)
    // ═══════════════════════════════════════════════

    private void SliderFill_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;
        UpdateLevelOverlay();
    }

    private void SliderCamera_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;
        UpdateLevelOverlay();
    }

    private void UpdateLevelOverlay()
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;

        double fillPercent = SliderFill.Value;
        var geo = _overlayService.BuildLevelGeometryFromSlider(fillPercent, _activeLevelMode);
        if (geo == null) return;

        _currentGeometry = geo;
        ClearByTag(TagFill);
        ClearByTag(TagOverlay);

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        var plan = PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geo,
            _calibration,
            r.X,
            r.Y,
            r.Width,
            r.Height,
            CameraHeightPercent);
        if (plan is null) return;

        // Fuellfarbe (statisch gefroren, keine Allokation)
        Brush fillBrush = _activeLevelMode switch
        {
            LevelMode.Water => WaterFillBrush,
            LevelMode.Deposit => DepositFillBrush,
            LevelMode.Obstacle => ObstacleFillBrush,
            _ => Brushes.Transparent
        };

        // CombinedGeometry: Fuellung geclippt am Rohrkreis
        var center = new Point(plan.Center.X, plan.Center.Y);
        var pipeEllipse = new EllipseGeometry(center, plan.PipeRadius, plan.PipeRadius);
        var fillRect = new RectangleGeometry(new Rect(
            plan.FillRect.X,
            plan.FillRect.Y,
            plan.FillRect.Width,
            plan.FillRect.Height));

        var combined = new CombinedGeometry(GeometryCombineMode.Intersect, pipeEllipse, fillRect);
        var fillPath = new System.Windows.Shapes.Path
        {
            Data = combined,
            Fill = fillBrush,
            Tag = TagFill
        };
        OverlayCanvas.Children.Add(fillPath);

        var levelLine = new Line
        {
            X1 = plan.LineStart.X, Y1 = plan.LineStart.Y,
            X2 = plan.LineEnd.X, Y2 = plan.LineEnd.Y,
            Stroke = _activeLevelMode switch
            {
                LevelMode.Water => Brushes.RoyalBlue,
                LevelMode.Deposit => Brushes.Chocolate,
                LevelMode.Obstacle => Brushes.Crimson,
                _ => Brushes.White
            },
            StrokeThickness = 2,
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(levelLine);

        // Label
        string labelText = $"{fillPercent:F1}%";
        AddCanvasLabel(labelText, plan.LabelPosition.X, plan.LabelPosition.Y, TagOverlay);

        TxtMeasureInfo.Text = $"{fillPercent:F1}%";
        TxtStatus.Text = $"{_activeLevelMode}: {fillPercent:F1}% | Mausrad: Kreis | Drag: Position";
    }

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
        var (top, bottom, right, left) = PhotoMeasurementGeometryService.SortDeformationPoints(
            _clickPoints, _calibration.PipeCenter.X, _calibration.PipeCenter.Y);

        double deformPct = PhotoMeasurementGeometryService.DeformationPercent(
            top, bottom, left, right, _imageAspect, _calibration.NormalizedDiameter);

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

        // Strecken fuer Statuszeile berechnen (Aspect-korrigiert)
        double dV = PipeCalibration.AspectCorrectedDistance(top, bottom, _imageAspect);
        double dH = PipeCalibration.AspectCorrectedDistance(left, right, _imageAspect);

        TxtMeasureInfo.Text = $"Deform: {deformPct:F1}%";
        TxtStatus.Text = $"Deformation: {deformPct:F1}% (V={dV:F3}, H={dH:F3})";
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

        TxtStatus.Text = $"Querschnitt: {_clickPoints.Count} Punkte | Doppelklick = schließen";
    }

    private void ClosePolygon()
    {
        if (_clickPoints.Count < 3) return;
        _polygonClosed = true;

        var r = GetImageRenderedRect(PhotoImage);
        var crossSection = PhotoMeasurementGeometryService.BuildCrossSectionGeometry(
            _clickPoints,
            r.Width,
            r.Height,
            _calibration.NormalizedDiameter);
        if (crossSection is null) return;

        _currentGeometry = crossSection.Geometry;
        double reductionPct = crossSection.ReductionPercent;

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
        var labelPos = NormToCanvas(crossSection.LabelPoint.X, crossSection.LabelPoint.Y);
        AddCanvasLabel($"Quersch: {reductionPct:F1}%", labelPos.X, labelPos.Y - 12, TagOverlay);

        TxtMeasureInfo.Text = $"Quersch: {reductionPct:F1}%";
        TxtStatus.Text = $"Querschnittsverminderung: {reductionPct:F1}%";
    }

    // ═══════════════════════════════════════════════
    // Abzweig / Bogen (Slider-basiert)
    // ═══════════════════════════════════════════════

    private void SliderPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtPosition.Text = $"{SliderPosition.Value:F0}°";
        UpdateAngleOverlay();
    }

    private void SliderAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtAngle.Text = $"{SliderAngle.Value:F0}°";
        UpdateAngleOverlay();
    }

    private void PositionPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double deg))
            SliderPosition.Value = deg;
    }

    private void AnglePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double deg))
            SliderAngle.Value = deg;
    }

    private void UpdateAngleOverlay()
    {
        ClearByTag(TagOverlay);
        ClearByTag(TagFill);

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        double refSize = Math.Min(r.Width, r.Height);
        double normDiam = _calibration.NormalizedDiameter;
        double pipeR = (normDiam / 2.0) * refSize;
        var center = NormToCanvas(_calibration.PipeCenter.X, _calibration.PipeCenter.Y);

        double positionDeg = SliderPosition.Value;
        double angleDeg = SliderAngle.Value;

        var toolType = _activeTool == PhotoTool.Lateral
            ? OverlayToolType.LateralCircle
            : OverlayToolType.PipeBend;
        var angleGeometry = PhotoMeasurementGeometryService.BuildAngleGeometry(
            toolType,
            _calibration.PipeCenter,
            normDiam,
            positionDeg,
            angleDeg);

        if (_activeTool == PhotoTool.Lateral)
        {
            DrawLateralOverlay(center, pipeR, angleGeometry.PositionRad, angleDeg);
        }
        else if (_activeTool == PhotoTool.Bend)
        {
            DrawBendOverlay(center, pipeR, angleGeometry.PositionRad, angleDeg);
        }

        _currentGeometry = angleGeometry.Geometry;

        TxtMeasureInfo.Text = $"{angleDeg:F0}° @ {angleGeometry.ClockHour:F1}h";
        TxtStatus.Text = _activeTool == PhotoTool.Lateral
            ? $"Abzweig: {angleDeg:F0}° bei {angleGeometry.ClockHour:F1} Uhr"
            : $"Bogen: {angleDeg:F0}° bei {angleGeometry.ClockHour:F1} Uhr";
    }

    private void DrawLateralOverlay(Point center, double pipeR, double posRad, double angleDeg)
    {
        var plan = PhotoMeasurementGeometryService.BuildLateralOverlayPlan(
            center.X,
            center.Y,
            pipeR,
            posRad,
            angleDeg);

        var circle = new Ellipse
        {
            Width = plan.OpeningRadius * 2, Height = plan.OpeningRadius * 2,
            Stroke = Brushes.Red, StrokeThickness = 2,
            Fill = LateralFillBrush,
            Tag = TagOverlay
        };
        Canvas.SetLeft(circle, plan.OpeningCenter.X - plan.OpeningRadius);
        Canvas.SetTop(circle, plan.OpeningCenter.Y - plan.OpeningRadius);
        OverlayCanvas.Children.Add(circle);

        OverlayCanvas.Children.Add(new Line
        {
            X1 = plan.OpeningCenter.X, Y1 = plan.OpeningCenter.Y, X2 = plan.Arm1End.X, Y2 = plan.Arm1End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });
        OverlayCanvas.Children.Add(new Line
        {
            X1 = plan.OpeningCenter.X, Y1 = plan.OpeningCenter.Y, X2 = plan.Arm2End.X, Y2 = plan.Arm2End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });

        // Winkelbogen
        DrawArc(plan.OpeningCenter.X, plan.OpeningCenter.Y, plan.ArcRadius, plan.ArcStartRad, plan.ArcEndRad,
            Brushes.Yellow, 1.5, TagOverlay);

        // Label
        AddCanvasLabel($"{angleDeg:F0}°", plan.LabelPosition.X, plan.LabelPosition.Y, TagOverlay);
    }

    private void DrawBendOverlay(Point center, double pipeR, double posRad, double angleDeg)
    {
        var plan = PhotoMeasurementGeometryService.BuildBendOverlayPlan(
            center.X,
            center.Y,
            pipeR,
            posRad,
            angleDeg);

        // Clip am Rohrkreis
        var clipGeo = new EllipseGeometry(center, pipeR, pipeR);
        var bendContainer = new Canvas
        {
            Clip = clipGeo,
            Width = OverlayCanvas.ActualWidth,
            Height = OverlayCanvas.ActualHeight,
            Tag = TagOverlay
        };

        foreach (var ringPlan in plan.Rings)
        {
            var ring = new Ellipse
            {
                Width = ringPlan.RadiusX * 2, Height = ringPlan.RadiusY * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)(180 + 75 * ringPlan.PerspectiveScale), 255, 165, 0)),
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(ring, ringPlan.Center.X - ringPlan.RadiusX);
            Canvas.SetTop(ring, ringPlan.Center.Y - ringPlan.RadiusY);
            bendContainer.Children.Add(ring);
        }

        OverlayCanvas.Children.Add(bendContainer);

        // Bogenbahn-Achslinie (gestrichelt)
        var pathFig = new PathFigure();
        for (int i = 0; i < plan.AxisPoints.Count; i++)
        {
            var point = plan.AxisPoints[i];
            if (i == 0) pathFig.StartPoint = new Point(point.X, point.Y);
            else pathFig.Segments.Add(new LineSegment(new Point(point.X, point.Y), true));
        }

        var pathGeo = new PathGeometry(new[] { pathFig });
        var axisLine = new System.Windows.Shapes.Path
        {
            Data = pathGeo,
            Stroke = Brushes.Orange,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(axisLine);
    }

    // ═══════════════════════════════════════════════
    // Rohrkreis zeichnen
    // ═══════════════════════════════════════════════

    private void DrawPipeCircle()
    {
        ClearByTag(TagPipeCircle);

        // Rohrkreis nur bei Messwerkzeugen die ihn brauchen
        if (_activeTool is PhotoTool.None or PhotoTool.MarkRect
            or PhotoTool.Calibration or PhotoTool.Ruler or PhotoTool.Connection)
            return;

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        var plan = PhotoMeasurementGeometryService.BuildPipeCirclePlan(
            _calibration,
            r.X,
            r.Y,
            r.Width,
            r.Height);
        if (plan is null) return;

        var ellipse = new Ellipse
        {
            Width = plan.Radius * 2, Height = plan.Radius * 2,
            Stroke = Brushes.Cyan,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Fill = Brushes.Transparent,
            Tag = TagPipeCircle
        };
        Canvas.SetLeft(ellipse, plan.Center.X - plan.Radius);
        Canvas.SetTop(ellipse, plan.Center.Y - plan.Radius);
        OverlayCanvas.Children.Add(ellipse);

        // Fadenkreuz
        var hLine = new Line
        {
            X1 = plan.HorizontalStart.X, Y1 = plan.HorizontalStart.Y,
            X2 = plan.HorizontalEnd.X, Y2 = plan.HorizontalEnd.Y,
            Stroke = Brushes.Cyan, StrokeThickness = 1,
            Tag = TagPipeCircle
        };
        var vLine = new Line
        {
            X1 = plan.VerticalStart.X, Y1 = plan.VerticalStart.Y,
            X2 = plan.VerticalEnd.X, Y2 = plan.VerticalEnd.Y,
            Stroke = Brushes.Cyan, StrokeThickness = 1,
            Tag = TagPipeCircle
        };
        OverlayCanvas.Children.Add(hLine);
        OverlayCanvas.Children.Add(vLine);
    }

    // ═══════════════════════════════════════════════
    // Canvas-Helfer
    // ═══════════════════════════════════════════════

    private void ClearOverlay()
    {
        ClearByTag(TagPipeCircle);
        ClearByTag(TagOverlay);
        ClearByTag(TagPreview);
        ClearByTag(TagFill);
    }

    private void ClearByTag(string tag)
    {
        var toRemove = OverlayCanvas.Children.OfType<UIElement>()
            .Where(e => (e is FrameworkElement fe && fe.Tag as string == tag)).ToList();
        foreach (var el in toRemove)
            OverlayCanvas.Children.Remove(el);
    }

    private TextBlock AddCanvasLabel(string text, double x, double y, string tag)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Tag = tag
        };
        // Hintergrund-Border
        var border = new Border
        {
            Background = LabelBgBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = tb,
            Tag = tag
        };
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        OverlayCanvas.Children.Add(border);
        return tb;
    }

    private void DrawArc(double cx, double cy, double radius,
        double startRad, double endRad, Brush stroke, double thickness, string tag)
    {
        var pathFig = new PathFigure
        {
            StartPoint = new Point(
                cx + Math.Cos(startRad) * radius,
                cy + Math.Sin(startRad) * radius)
        };

        double sweep = endRad - startRad;
        bool isLargeArc = Math.Abs(sweep) > Math.PI;

        pathFig.Segments.Add(new ArcSegment(
            new Point(
                cx + Math.Cos(endRad) * radius,
                cy + Math.Sin(endRad) * radius),
            new Size(radius, radius),
            0,
            isLargeArc,
            sweep > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            true));

        var pathGeo = new PathGeometry(new[] { pathFig });
        var path = new System.Windows.Shapes.Path
        {
            Data = pathGeo,
            Stroke = stroke,
            StrokeThickness = thickness,
            Tag = tag
        };
        OverlayCanvas.Children.Add(path);
    }

    // ═══════════════════════════════════════════════
    // Overlay ins Foto einbrennen (DPI-korrekt)
    // ═══════════════════════════════════════════════

    private string? BurnOverlayToPhoto()
    {
        if (PhotoImage.Source is not BitmapSource bmpSrc) return null;

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return null;

        // In ORIGINALAUFLOESUNG rendern (nicht Display-Groesse)
        int outW = bmpSrc.PixelWidth;
        int outH = bmpSrc.PixelHeight;
        if (outW <= 0 || outH <= 0) return null; // Bild hat keine gueltige Groesse

        var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Original-Foto in voller Aufloesung
            dc.DrawImage(bmpSrc, new Rect(0, 0, outW, outH));

            // 2. Canvas-Overlay hochskalieren: Display-Bereich → Originalaufloesung
            double scaleX = outW / r.Width;
            double scaleY = outH / r.Height;

            // Nur den gerenderten Bildbereich des Canvas nehmen (Letterbox-Offset abziehen)
            var vb = new VisualBrush(OverlayCanvas)
            {
                Viewbox = new Rect(r.X, r.Y, r.Width, r.Height),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            dc.DrawRectangle(vb, null, new Rect(0, 0, outW, outH));
        }
        rtb.Render(dv);

        // PNG speichern
        var outPath = System.IO.Path.ChangeExtension(_photoPath, null) + "_overlay.png";
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(outPath);
        enc.Save(fs);
        return outPath;
    }

    // ═══════════════════════════════════════════════
    // OK / Abbrechen / Undo / Loeschen
    // ═══════════════════════════════════════════════

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string? overlayPath = null;
        if (_currentGeometry != null)
        {
            try
            {
                overlayPath = BurnOverlayToPhoto();
            }
            catch (Exception ex)
            {
                // Overlay-Export fehlgeschlagen → trotzdem Ergebnis zurueckgeben (ohne Overlay-Foto)
                TxtStatus.Text = $"Overlay-Export fehlgeschlagen: {ex.Message}";
            }
        }

        Result = new PhotoMeasurementResult
        {
            Geometry = _currentGeometry,
            OverlayPhotoPath = overlayPath,
            Confirmed = true,
            UpdatedCalibration = _calibration
        };
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => UndoLastPoint();

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
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
        TxtStatus.Text = _activeTool switch
        {
            PhotoTool.None => "Werkzeug wählen, um mit der Messung zu beginnen.",
            PhotoTool.Calibration => "Referenzlinie über sichtbaren Rohrdurchmesser ziehen.",
            PhotoTool.MarkRect => "Rechteck um Schaden/Beobachtung ziehen (für KI-Training).",
            PhotoTool.LevelWater => "Wasserstand: Slider links | Mausrad: Kreis-Größe | Drag: Position",
            PhotoTool.LevelDeposit => "Ablagerung: Slider links | Mausrad: Kreis-Größe | Drag: Position",
            PhotoTool.LevelObstacle => "Hindernis: Slider links | Mausrad: Kreis-Größe | Drag: Position",
            PhotoTool.Deformation => "4 Punkte auf Rohrwand klicken: Oben → Unten → Links → Rechts",
            PhotoTool.Ruler => "Linie ziehen für Distanzmessung (Kalibrierung nötig).",
            PhotoTool.CrossSection => "Polygon-Punkte klicken, Doppelklick = schließen.",
            PhotoTool.Lateral => "Position + Winkel per Slider einstellen.",
            PhotoTool.Bend => "Position + Winkel per Slider einstellen.",
            PhotoTool.Connection => "Massstab-Linie auf Rohroberfläche ziehen.",
            _ => ""
        };
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
