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
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

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

    /// <summary>V4.3 — Subject-Auswahl fuer Querschnitt-Werkzeug (Wurzel/Abplatzung/Fehlstelle/Sonstige).</summary>
    private string? _crossSectionSubject;

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
    private static readonly Brush RulerBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(0, 255, 0)));
    private static readonly Brush CrackBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(255, 51, 51)));
    private static readonly Brush JointOffsetBrush = FreezeBrush(new SolidColorBrush(Color.FromRgb(255, 105, 180)));
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

        if (controlW <= 0 || controlH <= 0 || imgW <= 0 || imgH <= 0)
            return new Rect(imageControl.RenderSize);

        double scaleX = controlW / imgW;
        double scaleY = controlH / imgH;
        double scale = Math.Min(scaleX, scaleY);

        double renderedW = imgW * scale;
        double renderedH = imgH * scale;
        double offsetX = (controlW - renderedW) / 2.0;
        double offsetY = (controlH - renderedH) / 2.0;

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
        BtnToolPipeSurface, BtnToolCrack, BtnToolJointOffset,
        BtnToolWater, BtnToolDeposit, BtnToolObstacle,
        BtnToolDeform, BtnToolCrossSection,
        BtnToolLateral, BtnToolBend, BtnToolConnection,
        BtnToolRingBBox
    };


    // ═══════════════════════════════════════════════
    // Kalibrierung
    // ═══════════════════════════════════════════════

    private void FinalizeCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        // Aspect-Ratio-korrigierte Distanz fuer korrekte Kalibrierung
        double normLen = PipeCalibration.AspectCorrectedDistance(start, end, _imageAspect);
        if (normLen < 0.01) return;

        _calibration.NormalizedDiameter = normLen;
        _calibration.PipeCenter = new NormalizedPoint(
            (start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
        _calibration.WasManuallyCalibrated = true;

        _overlayService.SetCalibration(_calibration);
        DrawPipeCircle();
        UpdateDnInfo();

        // OK bei mm-Werkzeugen aktivieren
        BtnOk.IsEnabled = true;

        TxtMeasureInfo.Text = $"Kalibriert: {normLen:F3}";
        TxtStatus.Text = "Kalibrierung abgeschlossen. Rohrkreis angepasst.";
    }

    // ═══════════════════════════════════════════════
    // Markierung (Rechteck fuer KI-Training)
    // ═══════════════════════════════════════════════

    private void FinalizeMarkRect(NormalizedPoint start, NormalizedPoint end)
    {
        double minX = Math.Min(start.X, end.X), maxX = Math.Max(start.X, end.X);
        double minY = Math.Min(start.Y, end.Y), maxY = Math.Max(start.Y, end.Y);
        if (maxX - minX < 0.01 || maxY - minY < 0.01) return;

        _currentGeometry = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                new(minX, minY), new(maxX, minY),
                new(maxX, maxY), new(minX, maxY)
            }
        };

        // Overlay zeichnen
        ClearByTag(TagOverlay);
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
        TxtStatus.Text = "Bereich markiert. SAM + Qwen werden gestartet...";

        // SAM + Qwen-Klassifikation auf das markierte Region (asynchron, blockiert UI nicht).
        // PhotoAssistant nutzt das Foto-File direkt - kein VLC-Airspace-Problem wie im Codiermodus.
        _ = AnalyzeMarkRectAsync(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Schickt die markierte BBox-Region an SAM + Qwen und rendert das Ergebnis
    /// als gruene Maske + Klassifikations-Label. Funktioniert weil das Foto eine
    /// statische Datei ist (anders als im Codiermodus mit laufendem VLC-Video).
    /// </summary>
    private async Task AnalyzeMarkRectAsync(double normX1, double normY1, double normX2, double normY2)
    {
        if (string.IsNullOrWhiteSpace(_photoPath) || !File.Exists(_photoPath))
        {
            TxtStatus.Text = "Foto-Datei nicht gefunden - SAM uebersprungen.";
            return;
        }

        try
        {
            byte[] pngBytes = await File.ReadAllBytesAsync(_photoPath);

            // Bild-Aufloesung ermitteln
            int imgW = 1920, imgH = 1080;
            try
            {
                using var ms = new System.IO.MemoryStream(pngBytes);
                var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    ms,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                if (dec.Frames.Count > 0)
                {
                    imgW = dec.Frames[0].PixelWidth;
                    imgH = dec.Frames[0].PixelHeight;
                }
            }
            catch { }

            // BBox-Pixel in Image-Koordinaten
            double pxX1 = normX1 * imgW, pxY1 = normY1 * imgH;
            double pxX2 = normX2 * imgW, pxY2 = normY2 * imgH;

            // Sidecar lazy initialisieren
            var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                ?? "http://localhost:8100";
            var sidecar = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(new Uri(sidecarUrl));

            var samBox = new AuswertungPro.Next.Application.Ai.Pipeline.SamBoundingBox(pxX1, pxY1, pxX2, pxY2, "manual", 1.0);
            var samReq = new AuswertungPro.Next.Application.Ai.Pipeline.SamRequest(Convert.ToBase64String(pngBytes), [samBox]);

            AuswertungPro.Next.Application.Ai.Pipeline.SamResponse samResp;
            try
            {
                samResp = await sidecar.SegmentSamAsync(samReq);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"SAM-Fehler: {ex.Message}";
                return;
            }

            if (samResp is null || samResp.Masks.Count == 0)
            {
                TxtStatus.Text = "SAM lieferte keine Maske - BBox bleibt aktiv.";
                return;
            }

            // Maske rendern - sicheres OverlayCanvas-Render (kein VLC, daher kein Airspace-Problem)
            var firstMask = samResp.Masks[0];
            await Dispatcher.InvokeAsync(() => RenderSamMaskOnPhoto(samResp, firstMask, imgW, imgH));

            TxtStatus.Text = $"SAM-Maske: {firstMask.MaskAreaPixels} px in {samResp.InferenceTimeMs:F0} ms";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"SAM-Pipeline-Fehler: {ex.Message}";
        }
    }

    private void RenderSamMaskOnPhoto(AuswertungPro.Next.Application.Ai.Pipeline.SamResponse samResp,
        AuswertungPro.Next.Application.Ai.Pipeline.SamMaskResult mask, int imgW, int imgH)
    {
        double cw = OverlayCanvas.ActualWidth;
        double ch = OverlayCanvas.ActualHeight;
        if (cw <= 0 || ch <= 0) return;

        try
        {
            var decoded = Ai.Pipeline.SamMaskRenderer.DecodeRle(mask.MaskRle, imgW, imgH);

            // Fuellung
            var fillGeom = Ai.Pipeline.SamMaskRenderer.ExtractFillGeometry(decoded, imgW, imgH, cw, ch, targetWidth: 720);
            var fill = new System.Windows.Shapes.Path
            {
                Data = fillGeom,
                Fill = new SolidColorBrush(Color.FromArgb(140, 57, 255, 20)),
                Tag = "sam_photo_mask",
                IsHitTestVisible = false
            };
            OverlayCanvas.Children.Add(fill);

            // Konturlinien (Moore-Trace)
            var polylines = Ai.Pipeline.SamMaskRenderer.ExtractContourPolylines(decoded, imgW, imgH, cw, ch);
            int contoursDrawn = 0;
            foreach (var poly in polylines)
            {
                if (poly.Count < 3) continue;
                contoursDrawn++;
                var outer = new System.Windows.Shapes.Polyline
                {
                    Stroke = Brushes.White, StrokeThickness = 5,
                    Tag = "sam_photo_mask", IsHitTestVisible = false
                };
                foreach (var p in poly) outer.Points.Add(p);
                outer.Points.Add(poly[0]);
                OverlayCanvas.Children.Add(outer);

                var inner = new System.Windows.Shapes.Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                    StrokeThickness = 3,
                    Tag = "sam_photo_mask", IsHitTestVisible = false,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 16, ShadowDepth = 0,
                        Color = Color.FromRgb(0, 255, 0), Opacity = 0.95
                    }
                };
                foreach (var p in poly) inner.Points.Add(p);
                inner.Points.Add(poly[0]);
                OverlayCanvas.Children.Add(inner);
            }

            // Fallback wenn keine Polylines: alte Treppen-Kontur
            if (contoursDrawn == 0)
            {
                var fb = Ai.Pipeline.SamMaskRenderer.ExtractContourGeometry(decoded, imgW, imgH, cw, ch);
                OverlayCanvas.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = fb,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                    StrokeThickness = 3,
                    Tag = "sam_photo_mask", IsHitTestVisible = false
                });
            }

            // Centroid-Label
            double labelX = mask.CentroidX / imgW * cw;
            double labelY = mask.CentroidY / imgH * ch;
            var label = new TextBlock
            {
                Text = $"SAM · {mask.MaskAreaPixels} px · Konf {mask.Confidence:P0}",
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromRgb(57, 255, 20)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(4, 1, 4, 1),
                Tag = "sam_photo_mask",
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, Math.Max(0, labelX - 80));
            Canvas.SetTop(label, Math.Max(0, labelY - 18));
            OverlayCanvas.Children.Add(label);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PhotoAssistant SAM] Render-Fehler: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════
    // Lineal / WinCan-PhotoAssistant 2-Punkt-Werkzeuge (Drag-Linie)
    // ═══════════════════════════════════════════════

    private void FinalizeLine(NormalizedPoint start, NormalizedPoint end)
    {
        double normLen = PipeCalibration.AspectCorrectedDistance(start, end, _imageAspect);
        if (normLen < 0.005) return;

        var measurement = BuildLineMeasurement(normLen);

        var geometry = new OverlayGeometry
        {
            ToolType = _activeTool is PhotoTool.Ruler or PhotoTool.PipeSurface
                ? OverlayToolType.Ruler : OverlayToolType.Line,
            Points = new List<NormalizedPoint> { start, end },
            Q1Mm = Math.Round(measurement.PrimaryMm, 1),
            ClockFrom = _calibration.PointToClockHour(start),
            ClockTo = _calibration.PointToClockHour(end)
        };
        if (_activeTool == PhotoTool.JointOffset && measurement.SecondaryValue.HasValue)
        {
            geometry.FillPercent = Math.Round(measurement.SecondaryValue.Value, 1);
        }
        else if (measurement.SecondaryValue.HasValue)
        {
            geometry.Q2Mm = Math.Round(measurement.SecondaryValue.Value, 1);
        }

        _currentGeometry = geometry;

        // Overlay zeichnen
        ClearByTag(TagOverlay);
        var p1 = NormToCanvas(start.X, start.Y);
        var p2 = NormToCanvas(end.X, end.Y);

        var line = new Line
        {
            X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
            Stroke = measurement.Stroke, StrokeThickness = 3,
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(line);
        AddHandle(p1, measurement.Stroke, TagOverlay);
        AddHandle(p2, measurement.Stroke, TagOverlay);

        // Label
        AddCanvasLabel(measurement.Label, (p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2 - 16, TagOverlay);

        TxtMeasureInfo.Text = measurement.Label;
        TxtStatus.Text = measurement.Status;
    }

    private LineMeasurement BuildLineMeasurement(double normLen)
    {
        double mm = _calibration.NormToMm(normLen);
        return _activeTool switch
        {
            PhotoTool.PipeSurface => BuildPipeSurfaceMeasurement(mm),
            PhotoTool.CrackWidth => new LineMeasurement(mm, null, $"Rissbreite: {mm:F1} mm", $"Rissbreite: {mm:F1} mm", CrackBrush),
            PhotoTool.JointOffset => BuildJointOffsetMeasurement(mm),
            PhotoTool.Connection => new LineMeasurement(mm, null, $"{mm:F1} mm", $"Anschluss: {mm:F1} mm", RulerBrush),
            _ => new LineMeasurement(mm, null, $"{mm:F1} mm", $"Distanz: {mm:F1} mm", RulerBrush)
        };
    }

    private LineMeasurement BuildPipeSurfaceMeasurement(double chordMm)
    {
        double pipeDiameterMm = Math.Max(_calibration.NormToMm(_calibration.NormalizedDiameter), 0.001);
        double radiusMm = pipeDiameterMm / 2.0;
        double clampedChord = Math.Min(Math.Max(chordMm, 0), radiusMm * 2.0);
        double arcMm = 2.0 * radiusMm * Math.Asin(clampedChord / (2.0 * radiusMm));
        return new LineMeasurement(
            arcMm,
            chordMm,
            $"Distanz: {arcMm:F1} mm (Oberflaeche)",
            $"Rohroberflaeche: {arcMm:F1} mm",
            RulerBrush);
    }

    private LineMeasurement BuildJointOffsetMeasurement(double mm)
    {
        double pipeDiameterMm = Math.Max(_calibration.NormToMm(_calibration.NormalizedDiameter), 0.001);
        double percent = mm / pipeDiameterMm * 100.0;
        return new LineMeasurement(
            mm,
            percent,
            $"Versatz: {mm:F1} mm ({percent:F1}%)",
            $"Muffenversatz: {mm:F1} mm ({percent:F1}%)",
            JointOffsetBrush);
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
        // Foto-Assistent: Bend/Lateral re-rendern auf Camera-Hoehe
        if (IsPhotoAssistantActive) { PaOnCameraChanged(); return; }
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

        double refSize = Math.Min(r.Width, r.Height);
        double normDiam = _calibration.NormalizedDiameter;
        double pipeR = (normDiam / 2.0) * refSize;

        // Guard: Kein negativer/leerer Radius (tritt auf wenn Kalibrierung fehlt oder Zoom=0)
        if (pipeR <= 0) return;

        // Bugfix: Kamerahoehe-Korrektur darf den Fuell-Mittelpunkt NICHT verschieben,
        // sonst leckt das Fuell-Polygon nach unten/oben aus dem sichtbaren Rohrkreis
        // (Clipping-Ellipse versetzt vs. visuelles Rohr). Die Kameraperspektive wird
        // ueber die Verschiebung des Sohle/Scheitel-Bezugs gehandhabt - separat in
        // BuildLevelGeometryFromSlider.
        double normCx = _calibration.PipeCenter.X;
        double normCy = _calibration.PipeCenter.Y;
        var center = NormToCanvas(normCx, normCy);

        // Fuellfarbe (statisch gefroren, keine Allokation)
        Brush fillBrush = _activeLevelMode switch
        {
            LevelMode.Water => WaterFillBrush,
            LevelMode.Deposit => DepositFillBrush,
            LevelMode.Obstacle => ObstacleFillBrush,
            _ => Brushes.Transparent
        };

        // Level-Linie Y-Position
        if (geo.Points.Count >= 2)
        {
            var levelCanvas = NormToCanvas(geo.Points[0].X, geo.Points[0].Y);
            double levelY = levelCanvas.Y;

            // CombinedGeometry: Fuellung geclippt am Rohrkreis
            var pipeEllipse = new EllipseGeometry(center, pipeR, pipeR);

            Geometry fillRect;
            if (_activeLevelMode == LevelMode.Obstacle)
            {
                // Von oben nach levelY
                var h = Math.Max(0, levelY - (center.Y - pipeR));
                fillRect = new RectangleGeometry(new Rect(
                    center.X - pipeR, center.Y - pipeR,
                    pipeR * 2, h));
            }
            else
            {
                // Von levelY nach unten
                var h = Math.Max(0, (center.Y + pipeR) - levelY);
                fillRect = new RectangleGeometry(new Rect(
                    center.X - pipeR, levelY,
                    pipeR * 2, h));
            }

            var combined = new CombinedGeometry(GeometryCombineMode.Intersect, pipeEllipse, fillRect);
            var fillPath = new System.Windows.Shapes.Path
            {
                Data = combined,
                Fill = fillBrush,
                Tag = TagFill
            };
            OverlayCanvas.Children.Add(fillPath);

            // Level-Linie
            // Chord am Kreis berechnen (Schnittpunkte der horizontalen Linie mit dem Kreis)
            double relY = levelY - center.Y;
            double chordHalf = Math.Sqrt(Math.Max(0, pipeR * pipeR - relY * relY));

            var levelLine = new Line
            {
                X1 = center.X - chordHalf, Y1 = levelY,
                X2 = center.X + chordHalf, Y2 = levelY,
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
            AddCanvasLabel(labelText, center.X, levelY - 18, TagOverlay);
        }

        TxtMeasureInfo.Text = $"{fillPercent:F1}%";
        TxtStatus.Text = $"{_activeLevelMode}: {fillPercent:F1}% | Mausrad: Kreis | Drag: Position";
    }


    // ═══════════════════════════════════════════════
    // Abzweig / Bogen (Slider-basiert)
    // ═══════════════════════════════════════════════

    private void SliderPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtPosition.Text = $"{SliderPosition.Value:F0}°";
        // Bei aktivem Foto-Assistenten: nur dessen Render aufrufen, nicht die Legacy-Geometrie.
        if (IsPhotoAssistantActive)
        {
            // Position-Slider als Richtung verwenden:
            //  - PaTool.Lateral: Stunde 1..12 fuer Sichel-Position
            //  - PaTool.BendAngle: Richtung in Grad (0=oben, 90=rechts, 180=unten, 270=links)
            //    → komplett stufenlos in alle Richtungen
            int hour = ((int)Math.Round(SliderPosition.Value / 30.0)) % 12;
            if (hour == 0) hour = 12;
            PaOnHourSelected(hour);
            PaOnBendDirectionChanged(SliderPosition.Value);
            return;
        }
        UpdateAngleOverlay();
    }

    private void SliderAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Foto-Assistent (Bend / Lateral) hat Vorrang ueber den klassischen Pfad.
        if (IsPhotoAssistantActive)
        {
            TxtAngle.Text = $"{SliderAngle.Value:F0}°";
            PaOnSliderAngleChanged(SliderAngle.Value);
            return;
        }
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtAngle.Text = $"{SliderAngle.Value:F0}°";
        UpdateAngleOverlay();
    }

    private void PositionPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double deg))
        {
            SliderPosition.Value = deg;
            // Foto-Assistent: deg → hour (1..12)
            if (_paActive == PaTool.Lateral)
            {
                int hour = ((int)Math.Round(deg / 30.0)) % 12;
                if (hour == 0) hour = 12;
                PaOnHourSelected(hour);
            }
        }
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

        // Uhr-Position → Radiant (0° = 12 Uhr = oben)
        double posRad = (positionDeg - 90) * Math.PI / 180.0;

        if (_activeTool == PhotoTool.Lateral)
        {
            DrawLateralOverlay(center, pipeR, posRad, angleDeg);
        }
        else if (_activeTool == PhotoTool.Bend)
        {
            DrawBendOverlay(center, pipeR, posRad, angleDeg);
        }

        // Geometry
        double clockFrom = positionDeg / 30.0; // 360° / 12h = 30°/h
        _currentGeometry = new OverlayGeometry
        {
            ToolType = _activeTool == PhotoTool.Lateral
                ? OverlayToolType.LateralCircle : OverlayToolType.PipeBend,
            ArcDegrees = Math.Round(angleDeg, 1),
            ClockFrom = Math.Round(clockFrom, 1),
            Points = new List<NormalizedPoint>
            {
                _calibration.PipeCenter,
                new(_calibration.PipeCenter.X + Math.Cos(posRad) * normDiam / 2,
                    _calibration.PipeCenter.Y + Math.Sin(posRad) * normDiam / 2)
            }
        };

        TxtMeasureInfo.Text = $"{angleDeg:F0}° @ {clockFrom:F1}h";
        TxtStatus.Text = _activeTool == PhotoTool.Lateral
            ? $"Abzweig: {angleDeg:F0}° bei {clockFrom:F1} Uhr"
            : $"Bogen: {angleDeg:F0}° bei {clockFrom:F1} Uhr";
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

        bool isAngle = (_activeTool is PhotoTool.Lateral or PhotoTool.Bend) && !IsPhotoAssistantActive;
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
            PhotoTool.None => "Werkzeug waehlen, um mit der Messung zu beginnen.",
            PhotoTool.Calibration => "Referenzlinie ueber sichtbaren Rohrdurchmesser ziehen.",
            PhotoTool.MarkRect => "Rechteck um Schaden/Beobachtung ziehen (fuer KI-Training).",
            PhotoTool.LevelWater => "Wasserstand: Slider links | Mausrad: Kreis-Groesse | Drag: Position",
            PhotoTool.LevelDeposit => "Ablagerung: Slider links | Mausrad: Kreis-Groesse | Drag: Position",
            PhotoTool.LevelObstacle => "Hindernis: Slider links | Mausrad: Kreis-Groesse | Drag: Position",
            PhotoTool.Deformation => "4 Punkte auf Rohrwand klicken: Oben → Unten → Links → Rechts",
            PhotoTool.Ruler => "Linie ziehen fuer Distanzmessung (Kalibrierung noetig).",
            PhotoTool.PipeSurface => "2 Punkte auf der Rohroberflaeche ziehen (Bogenlaenge korrigiert).",
            PhotoTool.CrackWidth => "2 Punkte quer ueber den Riss ziehen.",
            PhotoTool.JointOffset => "2 Punkte zwischen alter und neuer Muffenkante ziehen.",
            PhotoTool.CrossSection => "Polygon-Punkte klicken, Doppelklick = schliessen.",
            PhotoTool.Lateral => "Position + Winkel per Slider einstellen.",
            PhotoTool.Bend => "Position + Winkel per Slider einstellen.",
            PhotoTool.Connection => "Massstab-Linie auf Rohroberflaeche ziehen.",
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
    PipeSurface,
    CrackWidth,
    JointOffset,
    CrossSection,
    Lateral,
    Bend,
    Connection,
    RingBBox   // V4.3: 12 BBoxes entlang Ring-Umfang fuer Ringrisse (BABBB)
}

internal sealed record LineMeasurement(
    double PrimaryMm,
    double? SecondaryValue,
    string Label,
    string Status,
    Brush Stroke);
