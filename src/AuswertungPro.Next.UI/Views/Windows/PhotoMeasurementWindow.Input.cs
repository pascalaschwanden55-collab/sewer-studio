using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// PhotoMeasurementWindow Eingabe-Behandlung: Tool-Button-Routing,
// Maus-Down/Up/Move/Wheel/Leave fuer alle Werkzeuge (Calibration, MarkRect,
// Line/PipeSurface/Joint, Lateral/Bend, Deformation, Polygon, RingBBox).
// Aus dem Hauptdatei extrahiert (Slice 20a).
public partial class PhotoMeasurementWindow
{
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
            var b when b == BtnToolPipeSurface => PhotoTool.PipeSurface,
            var b when b == BtnToolCrack => PhotoTool.CrackWidth,
            var b when b == BtnToolJointOffset => PhotoTool.JointOffset,
            var b when b == BtnToolCrossSection => PhotoTool.CrossSection,
            var b when b == BtnToolLateral => PhotoTool.Lateral,
            var b when b == BtnToolBend => PhotoTool.Bend,
            var b when b == BtnToolConnection => PhotoTool.Connection,
            var b when b == BtnToolRingBBox => PhotoTool.RingBBox,
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
        bool needsCalib = _activeTool is PhotoTool.Ruler or PhotoTool.PipeSurface
            or PhotoTool.CrackWidth or PhotoTool.JointOffset or PhotoTool.Connection;
        BtnOk.IsEnabled = !needsCalib || _calibration.IsCalibrated;

        // Cursor
        OverlayCanvas.Cursor = _activeTool switch
        {
            PhotoTool.None => Cursors.Arrow,
            PhotoTool.Deformation or PhotoTool.CrossSection => Cursors.Cross,
            _ => Cursors.Cross
        };

        // Foto-Assistent: drei Werkzeuge (Deformation/Bend/Connection als Mondsichel-Anschluss)
        var paTool = _activeTool switch
        {
            PhotoTool.Deformation => PaTool.Deformation,
            PhotoTool.Bend => PaTool.BendAngle,
            PhotoTool.Connection => PaTool.Lateral,
            _ => PaTool.None
        };
        SetActivePhotoAssistantTool(paTool);

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
            // Foto-Assistent (PaTool.BendAngle/Lateral) rendert eigene Geometrie -
            // dann Legacy unterdruecken um Doppel-Overlay zu vermeiden.
            if (!IsPhotoAssistantActive)
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
        // Foto-Assistent-Modus: spezielle Drag/Handle-Logik vorrangig
        if (PaHandleMouseDown(e)) return;
        if (_activeTool == PhotoTool.None) return;
        var pos = e.GetPosition(OverlayCanvas);
        if (!IsInsideImage(pos.X, pos.Y)) return;

        var norm = CanvasToNorm(pos.X, pos.Y);

        // --- Pipe-Kreis verschieben: bei ALLEN Tools, wenn Klick nahe am Center ---
        if (_activeTool != PhotoTool.Calibration)
        {
            var pipeCenterCanvas = NormToCanvas(
                _calibration.PipeCenter.X, _calibration.PipeCenter.Y);
            double distToCenter = Math.Sqrt(
                Math.Pow(pos.X - pipeCenterCanvas.X, 2) +
                Math.Pow(pos.Y - pipeCenterCanvas.Y, 2));
            var imgRect = GetImageRenderedRect(PhotoImage);
            double pipeRCanvas = (_calibration.NormalizedDiameter / 2.0) * Math.Min(imgRect.Width, imgRect.Height);
            // Nahe am Center (< 20% des Radius) = Pipe verschieben
            if (distToCenter < pipeRCanvas * 0.2)
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
            case PhotoTool.PipeSurface:
            case PhotoTool.CrackWidth:
            case PhotoTool.JointOffset:
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

            case PhotoTool.RingBBox:
                HandleRingBBoxClick(new NormalizedPoint(norm.X, norm.Y));
                break;

            // Level/Lateral/Bend: kein Klick-Aktion (nur Slider + Pipe-Drag)
            default:
                break;
        }
    }

    // ═══════════════════════════════════════════════
    // Ring-BBox (V4.3): 2 Klicks → 12 BBoxes entlang Umfang
    // ═══════════════════════════════════════════════

    /// <summary>1. Klick: Mittelpunkt. 2. Klick: Punkt auf Ring (definiert Radius).</summary>
    private NormalizedPoint? _ringCenter;

    private void HandleRingBBoxClick(NormalizedPoint p)
    {
        if (_ringCenter is null)
        {
            _ringCenter = p;
            ClearByTag(TagOverlay);
            // Marker fuer Center
            var canvas = NormToCanvas(p.X, p.Y);
            var marker = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = Brushes.HotPink,
                Stroke = Brushes.White, StrokeThickness = 1,
                Tag = TagOverlay
            };
            Canvas.SetLeft(marker, canvas.X - 5);
            Canvas.SetTop(marker, canvas.Y - 5);
            OverlayCanvas.Children.Add(marker);
            TxtStatus.Text = "Ringriss: jetzt einen Punkt AUF dem Riss klicken (definiert den Radius).";
        }
        else
        {
            FinalizeRingBBox(_ringCenter, p);
            _ringCenter = null;
        }
    }

    private void FinalizeRingBBox(NormalizedPoint center, NormalizedPoint ringPt)
    {
        double dx = ringPt.X - center.X;
        double dy = ringPt.Y - center.Y;
        double radius = Math.Sqrt(dx*dx + dy*dy);
        if (radius < 0.05) { TxtStatus.Text = "Radius zu klein, nochmal klicken."; return; }

        // 12 BBoxes entlang des Umfangs (alle 30°)
        const int BBOX_COUNT = 12;
        const double BBOX_REL_SIZE = 0.12; // 12% des Radius als BBox-Kantenlaenge
        double bboxSize = radius * BBOX_REL_SIZE;

        var points = new List<NormalizedPoint>();
        ClearByTag(TagOverlay);

        // Ring-Umriss als gestrichelter Kreis (optisch)
        var rImg = GetImageRenderedRect(PhotoImage);
        double refMin = Math.Min(rImg.Width, rImg.Height);
        var centerCanvas = NormToCanvas(center.X, center.Y);
        double radiusCanvas = radius * refMin;
        var circle = new Ellipse
        {
            Width = radiusCanvas * 2, Height = radiusCanvas * 2,
            Stroke = Brushes.HotPink, StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        Canvas.SetLeft(circle, centerCanvas.X - radiusCanvas);
        Canvas.SetTop(circle, centerCanvas.Y - radiusCanvas);
        OverlayCanvas.Children.Add(circle);

        for (int i = 0; i < BBOX_COUNT; i++)
        {
            double angle = 2 * Math.PI * i / BBOX_COUNT - Math.PI / 2; // Start bei 12 Uhr
            double cx = center.X + Math.Cos(angle) * radius;
            double cy = center.Y + Math.Sin(angle) * radius;
            points.Add(new NormalizedPoint(cx, cy));

            // BBox-Rechteck zeichnen
            var canvas = NormToCanvas(cx, cy);
            double sizePx = bboxSize * refMin;
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = sizePx, Height = sizePx,
                Stroke = Brushes.HotPink, StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(60, 244, 114, 182)),
                Tag = TagOverlay
            };
            Canvas.SetLeft(rect, canvas.X - sizePx / 2);
            Canvas.SetTop(rect, canvas.Y - sizePx / 2);
            OverlayCanvas.Children.Add(rect);
        }

        _currentGeometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.RingBBoxes,
            Points = points,
            // Radius als FillPercent (% des Rohrquerschnitts fuer spaetere Auswertung)
            FillPercent = Math.Round(radius * 100, 1)
        };

        TxtMeasureInfo.Text = $"Ring: {BBOX_COUNT} BBoxes";
        TxtStatus.Text = $"Ringriss: {BBOX_COUNT} BBoxes generiert (Radius {radius:F2} norm).";
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (PaHandleMouseUp(e)) return;
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
            case PhotoTool.PipeSurface:
            case PhotoTool.CrackWidth:
            case PhotoTool.JointOffset:
            case PhotoTool.Connection:
                FinalizeLine(
                    new NormalizedPoint(_dragStartNorm.X, _dragStartNorm.Y),
                    new NormalizedPoint(norm.X, norm.Y));
                break;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (PaHandleMouseMove(e)) return;
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
            // Wenn Foto-Assistent (PaTool) aktiv ist, unterdrueckt seine eigene Render-
            // Methode das Legacy-UpdateAngleOverlay - sonst sieht der User zwei Overlays
            // gleichzeitig (linke alte Krempel + rechte neue Schablone).
            bool isAngle = (_activeTool is PhotoTool.Lateral or PhotoTool.Bend) && !IsPhotoAssistantActive;
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
        // Foto-Assistent: Mausrad mit Faktor 1.08
        if (PaHandleMouseWheel(e)) return;
        // Mausrad: Rohrkreis-Groesse aendern (bei Level, Deformation, Querschnitt)
        if (_activeTool == PhotoTool.None || _activeTool == PhotoTool.Calibration) return;

        double delta = e.Delta > 0 ? 0.02 : -0.02;
        _overlayService.ResizePipeCircle(delta);
        _calibration = _overlayService.Calibration!;
        DrawPipeCircle();

        bool isLevel = _activeTool is PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle;
        if (isLevel) UpdateLevelOverlay();

        bool isAngle = (_activeTool is PhotoTool.Lateral or PhotoTool.Bend) && !IsPhotoAssistantActive;
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
}
