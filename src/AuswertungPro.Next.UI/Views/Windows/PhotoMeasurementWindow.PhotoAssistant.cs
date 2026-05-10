using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Ai.PhotoAssistant;
using AuswertungPro.Next.UI.Ai.PhotoAssistant;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Foto-Assistent (analog WinCan VX): drei Vermessungs-Werkzeuge
///   - Deformation (BAA): 16-Punkt-Ringkreis
///   - Bogen / Knick (BAJ): 3D-Lochkamera-Projektion eines Knickrohrs
///   - Anschluss (BCA): Mondsichel-Schablone
///
/// Implementiert als partial class - die existing 1966-Zeilen-Datei bleibt unangetastet.
/// Hooks: ToolButton_Checked-Erweiterung via SetActivePhotoAssistantTool, BtnOk_Click via ApplyPhotoAssistantToCodeMeta.
/// </summary>
public partial class PhotoMeasurementWindow
{
    // ── State ───────────────────────────────────────────────────────────

    private enum PaTool { None, Deformation, BendAngle, Lateral }
    private PaTool _paActive = PaTool.None;

    // Deformation: 16 Radien (0.2..1.0)
    private double[] _paDeformPoints = DeformationToolService.CreateDefaultRadii();
    // Bend
    private double _paBendAngleDeg = 30;
    private double _paBendScale = 1.0;
    private double _paBendOffsetX, _paBendOffsetY;
    /// <summary>Bogen-Richtung im Bildplan: 0=oben/12h, 90=rechts/3h, 180=unten/6h, 270=links/9h.</summary>
    private double _paBendDirectionDeg = 90;
    // Lateral
    private int _paLatHour = 3;
    private double _paLatAngleDeg = 90;
    private double _paLatDnPercent = 50;
    private double _paLatScale = 1.0;
    private double _paLatOffsetX, _paLatOffsetY;
    // Pipe-DN aus Slider (mm)
    private double _paPipeDnMm = 300;

    // Drag/Wheel
    private bool _paDragging;
    private Point _paDragStart;
    private bool _paDraggingDeformHandle;
    private int _paDeformDragIndex = -1;

    // Render-Layer (separate Children-Liste fuer einfaches Clearen)
    private readonly List<UIElement> _paLayer = new();

    // ── Activation ──────────────────────────────────────────────────────

    /// <summary>Wird aus ToolButton_Checked aufgerufen wenn ein Foto-Assistent-Tool aktiv wird.</summary>
    private void SetActivePhotoAssistantTool(PaTool tool)
    {
        _paActive = tool;
        ClearPaLayer();

        var on = tool != PaTool.None;
        PaLiveValuesBorder.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        PaSliderPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        PaVsaCodeBox.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        PaLatDnPanel.Visibility = tool == PaTool.Lateral ? Visibility.Visible : Visibility.Collapsed;

        if (on)
        {
            // Bei Bogen/Anschluss Camera-Slider sichtbar machen
            SliderCamera.Visibility = (tool == PaTool.BendAngle || tool == PaTool.Lateral)
                ? Visibility.Visible : Visibility.Collapsed;
            TxtCamLabel.Visibility = SliderCamera.Visibility;

            // Hour-Bar nur bei Anschluss
            PanelAngle.Visibility = (tool == PaTool.Lateral || tool == PaTool.BendAngle)
                ? Visibility.Visible : Visibility.Collapsed;

            PaRender();
            PaUpdateLiveValues();
            PaUpdateVsa();
        }
    }

    /// <summary>Liefert true wenn aktuell ein Foto-Assistent-Tool aktiv ist (steuert Mausrad/Drag-Override).</summary>
    private bool IsPhotoAssistantActive => _paActive != PaTool.None;

    private void ClearPaLayer()
    {
        foreach (var el in _paLayer)
            if (OverlayCanvas.Children.Contains(el))
                OverlayCanvas.Children.Remove(el);
        _paLayer.Clear();
    }

    // ── Rendering ───────────────────────────────────────────────────────

    private void PaRender()
    {
        ClearPaLayer();
        if (_paActive == PaTool.None) return;
        if (OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0) return;

        switch (_paActive)
        {
            case PaTool.Deformation: RenderDeformation(); break;
            case PaTool.BendAngle:   RenderBendAngle();   break;
            case PaTool.Lateral:     RenderLateral();     break;
        }
    }

    private void RenderDeformation()
    {
        var cx = OverlayCanvas.ActualWidth / 2.0;
        var cy = OverlayCanvas.ActualHeight / 2.0;
        var baseR = Math.Min(cx, cy) * 0.78;

        // Sollkreis (gestrichelt)
        var sollEll = new Ellipse
        {
            Width = baseR * 2, Height = baseR * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200)),
            StrokeDashArray = new DoubleCollection(new double[] { 4, 3 }),
            StrokeThickness = 1, Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(sollEll, cx - baseR); Canvas.SetTop(sollEll, cy - baseR);
        OverlayCanvas.Children.Add(sollEll); _paLayer.Add(sollEll);

        // Ist-Polygon
        var pts = DeformationToolService.ComputePoints(new Point2D(cx, cy), baseR, _paDeformPoints);
        var poly = new Polygon
        {
            Stroke = new SolidColorBrush(Color.FromArgb(220, 251, 191, 36)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(50, 251, 191, 36)),
            IsHitTestVisible = false
        };
        foreach (var p in pts) poly.Points.Add(p.ToWpfPoint());
        OverlayCanvas.Children.Add(poly); _paLayer.Add(poly);

        // Stuetzpunkte
        for (var i = 0; i < pts.Count; i++)
        {
            var dot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = Brushes.Yellow,
                Stroke = Brushes.Black, StrokeThickness = 1,
                Tag = $"deform-handle:{i}",
                Cursor = Cursors.SizeAll
            };
            Canvas.SetLeft(dot, pts[i].X - 5);
            Canvas.SetTop(dot, pts[i].Y - 5);
            OverlayCanvas.Children.Add(dot); _paLayer.Add(dot);
        }
    }

    private void RenderBendAngle()
    {
        var camPercent = SliderCamera != null ? SliderCamera.Value : 50.0;
        var (tube1, tube2, kink) = BendAngleToolService.BuildProjectedRings(
            bendAngleDegrees: _paBendAngleDeg,
            bendScale: _paBendScale,
            cameraHeightPercent: camPercent,
            canvasWidth: OverlayCanvas.ActualWidth,
            canvasHeight: OverlayCanvas.ActualHeight,
            dragOffsetX: _paBendOffsetX, dragOffsetY: _paBendOffsetY,
            bendDirectionDegrees: _paBendDirectionDeg);

        DrawTubeRings(tube1, baseHue: 195);   // cyan
        DrawTubeRings(tube2, baseHue: 50);    // gelb

        if (kink.HasValue)
        {
            var dot = new Ellipse
            {
                Width = 8, Height = 8, Fill = Brushes.Cyan,
                Stroke = Brushes.Black, StrokeThickness = 1, IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, kink.Value.X - 4); Canvas.SetTop(dot, kink.Value.Y - 4);
            OverlayCanvas.Children.Add(dot); _paLayer.Add(dot);
        }
    }

    private void DrawTubeRings(IReadOnlyList<BendAngleToolService.ProjectedRing> rings, int baseHue)
    {
        var maxDepth = rings.Max(r => r.DepthAtAxis);
        var minDepth = rings.Min(r => r.DepthAtAxis);
        foreach (var ring in rings)
        {
            if (ring.RingPoints.Count < 3) continue;
            // Opacity 0.95 vorne -> 0.40 hinten
            double t = maxDepth > minDepth
                ? (ring.DepthAtAxis - minDepth) / (maxDepth - minDepth)
                : 0;
            double opacity = 0.95 - t * 0.55;
            var color = HslToRgb(baseHue, 0.7, 0.55);
            var poly = new Polyline
            {
                Stroke = new SolidColorBrush(color) { Opacity = opacity },
                StrokeThickness = 1.4,
                IsHitTestVisible = false
            };
            foreach (var p in ring.RingPoints) poly.Points.Add(p.ToWpfPoint());
            // Schliessen
            poly.Points.Add(ring.RingPoints[0].ToWpfPoint());
            OverlayCanvas.Children.Add(poly); _paLayer.Add(poly);
        }
    }

    private void RenderLateral()
    {
        var cx = OverlayCanvas.ActualWidth / 2.0;
        var cy = OverlayCanvas.ActualHeight / 2.0;
        var baseR = Math.Min(cx, cy) * 0.78;

        // Hauptrohr (Sollkreis)
        var sollEll = new Ellipse
        {
            Width = baseR * 2, Height = baseR * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200)),
            StrokeDashArray = new DoubleCollection(new double[] { 4, 3 }),
            StrokeThickness = 1, Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(sollEll, cx - baseR); Canvas.SetTop(sollEll, cy - baseR);
        OverlayCanvas.Children.Add(sollEll); _paLayer.Add(sollEll);

        // Mondsichel
        var latRel = (_paLatDnPercent / 100.0) * _paLatScale;
        var pathData = LateralToolService.BuildSichelPathData(baseR, latRel, _paLatAngleDeg);
        var centerP2D = LateralToolService.ComputeSichelCenter(
            new Point2D(cx, cy), baseR, _paLatHour, _paLatOffsetX, _paLatOffsetY);
        var center = centerP2D.ToWpfPoint();

        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Stroke = new SolidColorBrush(Color.FromArgb(230, 52, 211, 153)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(70, 52, 211, 153)),
            IsHitTestVisible = false
        };
        var tg = new TransformGroup();
        tg.Children.Add(new RotateTransform(LateralToolService.RotationDegrees(_paLatHour)));
        tg.Children.Add(new TranslateTransform(center.X, center.Y));
        path.RenderTransform = tg;
        OverlayCanvas.Children.Add(path); _paLayer.Add(path);
    }

    // ── Live-Werte + VSA ─────────────────────────────────────────────────

    private void PaUpdateLiveValues()
    {
        if (PaLiveValuesText == null) return;
        var ic = CultureInfo.InvariantCulture;
        var camPercent = SliderCamera != null ? SliderCamera.Value : 50;

        PaLiveValuesText.Text = _paActive switch
        {
            PaTool.Deformation =>
                $"Querschnitt: {DeformationToolService.ComputeQuerschnittPercent(_paDeformPoints).ToString("F1", ic)} %\nØ {_paPipeDnMm:F0} mm",
            PaTool.BendAngle =>
                $"Knickwinkel: {_paBendAngleDeg:F0}°\nRichtung: {_paBendDirectionDeg:F0}°\nKamera: {camPercent:F0}%\nZoom: ×{_paBendScale:F2}\nØ {_paPipeDnMm:F0} mm",
            PaTool.Lateral =>
                $"Anschluss {_paLatHour}h\nLat-Winkel: {_paLatAngleDeg:F0}°\nLat-Ø: {(_paPipeDnMm * _paLatDnPercent / 100):F0} mm ({_paLatDnPercent:F0}%)\nKamera: {camPercent:F0}%\nZoom: ×{_paLatScale:F2}",
            _ => ""
        };
    }

    private void PaUpdateVsa()
    {
        if (PaVsaCodeText == null || PaVsaDescriptionText == null) return;

        VsaCodeSuggester.CodeSuggestion sug = _paActive switch
        {
            PaTool.Deformation => VsaCodeSuggester.ForDeformation(
                100 - DeformationToolService.ComputeQuerschnittPercent(_paDeformPoints)),
            PaTool.BendAngle => VsaCodeSuggester.ForBendAngle(_paBendAngleDeg),
            PaTool.Lateral => VsaCodeSuggester.ForLateral(
                _paLatHour, _paLatDnPercent, _paLatAngleDeg, (int)Math.Round(_paPipeDnMm)),
            _ => new VsaCodeSuggester.CodeSuggestion("—", "")
        };
        PaVsaCodeText.Text = sug.Code;
        PaVsaDescriptionText.Text = sug.Description;
    }

    // ── Slider-Handler ───────────────────────────────────────────────────

    private void PaSliderPipeDn_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _paPipeDnMm = e.NewValue;
        if (PaTxtPipeDn != null) PaTxtPipeDn.Text = $"{e.NewValue:F0} mm";
        PaUpdateLiveValues(); PaUpdateVsa();
    }

    private void PaSliderLatDn_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _paLatDnPercent = e.NewValue;
        if (PaTxtLatDn != null) PaTxtLatDn.Text = $"{e.NewValue:F0} %";
        if (_paActive == PaTool.Lateral) { PaRender(); PaUpdateLiveValues(); PaUpdateVsa(); }
    }

    /// <summary>Wird vom SliderAngle-Handler im Foto-Assistent-Modus aufgerufen.</summary>
    private void PaOnSliderAngleChanged(double value)
    {
        if (_paActive == PaTool.BendAngle) _paBendAngleDeg = Math.Clamp(value, 0, 90);
        else if (_paActive == PaTool.Lateral) _paLatAngleDeg = Math.Clamp(value, 30, 150);
        PaRender(); PaUpdateLiveValues(); PaUpdateVsa();
    }

    /// <summary>Wird vom Hour-Preset-Click im Foto-Assistent-Modus aufgerufen (degree → hour).</summary>
    private void PaOnHourSelected(int hourValue)
    {
        if (_paActive != PaTool.Lateral) return;
        _paLatHour = hourValue;
        PaRender(); PaUpdateLiveValues(); PaUpdateVsa();
    }

    /// <summary>Bogen-Richtung in Grad (stufenlos vom SliderPosition). 0=oben, 90=rechts, 180=unten, 270=links.</summary>
    private void PaOnBendDirectionChanged(double directionDeg)
    {
        if (_paActive != PaTool.BendAngle) return;
        _paBendDirectionDeg = directionDeg;
        PaRender(); PaUpdateLiveValues(); PaUpdateVsa();
    }

    /// <summary>Wird vom SliderCamera-Handler im Foto-Assistent-Modus aufgerufen.</summary>
    private void PaOnCameraChanged()
    {
        if (_paActive == PaTool.BendAngle || _paActive == PaTool.Lateral)
        { PaRender(); PaUpdateLiveValues(); }
    }

    // ── Mausrad: 1.08 / 0.926 ────────────────────────────────────────────

    private bool PaHandleMouseWheel(MouseWheelEventArgs e)
    {
        if (!IsPhotoAssistantActive) return false;
        var factor = e.Delta > 0 ? 1.08 : 1.0 / 1.08;

        if (_paActive == PaTool.BendAngle)
        {
            _paBendScale = BendAngleToolService.ClampScale(_paBendScale * factor);
        }
        else if (_paActive == PaTool.Lateral)
        {
            _paLatScale = LateralToolService.ClampScale(_paLatScale * factor);
        }
        else
        {
            return false; // Deformation: kein Zoom
        }

        PaRender(); PaUpdateLiveValues();
        e.Handled = true;
        return true;
    }

    // ── Drag ─────────────────────────────────────────────────────────────

    private bool PaHandleMouseDown(MouseButtonEventArgs e)
    {
        if (!IsPhotoAssistantActive) return false;
        var pos = e.GetPosition(OverlayCanvas);

        // Deform-Handle? -> Stuetzpunkt verschieben
        if (_paActive == PaTool.Deformation && e.OriginalSource is FrameworkElement fe
            && fe.Tag is string tag && tag.StartsWith("deform-handle:"))
        {
            if (int.TryParse(tag.Substring("deform-handle:".Length), out var idx))
            {
                _paDraggingDeformHandle = true;
                _paDeformDragIndex = idx;
                OverlayCanvas.CaptureMouse();
                e.Handled = true;
                return true;
            }
        }

        // Sonst: Schablone schieben (Bend / Lateral)
        if (_paActive == PaTool.BendAngle || _paActive == PaTool.Lateral)
        {
            _paDragging = true;
            _paDragStart = pos;
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
            return true;
        }
        return false;
    }

    private bool PaHandleMouseMove(MouseEventArgs e)
    {
        if (!IsPhotoAssistantActive) return false;
        var pos = e.GetPosition(OverlayCanvas);

        if (_paDraggingDeformHandle && _paDeformDragIndex >= 0)
        {
            var cx = OverlayCanvas.ActualWidth / 2.0;
            var cy = OverlayCanvas.ActualHeight / 2.0;
            var baseR = Math.Min(cx, cy) * 0.78;
            var dx = pos.X - cx;
            var dy = pos.Y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var rFactor = dist / baseR;
            _paDeformPoints[_paDeformDragIndex] = Math.Clamp(rFactor,
                DeformationToolService.MinRadius, DeformationToolService.MaxRadius);
            PaRender(); PaUpdateLiveValues(); PaUpdateVsa();
            return true;
        }

        if (_paDragging)
        {
            var dx = pos.X - _paDragStart.X;
            var dy = pos.Y - _paDragStart.Y;
            _paDragStart = pos;
            if (_paActive == PaTool.BendAngle)
            { _paBendOffsetX += dx; _paBendOffsetY += dy; }
            else if (_paActive == PaTool.Lateral)
            { _paLatOffsetX += dx; _paLatOffsetY += dy; }
            PaRender();
            return true;
        }
        return false;
    }

    private bool PaHandleMouseUp(MouseButtonEventArgs e)
    {
        if (!IsPhotoAssistantActive) return false;
        if (_paDragging || _paDraggingDeformHandle)
        {
            _paDragging = false;
            _paDraggingDeformHandle = false;
            _paDeformDragIndex = -1;
            OverlayCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return true;
        }
        return false;
    }

    // ── Persistenz ───────────────────────────────────────────────────────

    /// <summary>
    /// Schreibt die Foto-Assistent-Werte in einen ProtocolEntry / OverlayGeometry.
    /// Wird aus BtnOk_Click aufgerufen wenn ein Foto-Assistent-Tool aktiv ist.
    /// </summary>
    internal void ApplyPhotoAssistantToOverlay(OverlayGeometry geom)
    {
        if (geom is null || _paActive == PaTool.None) return;

        switch (_paActive)
        {
            case PaTool.Deformation:
                geom.ToolType = OverlayToolType.Deformation;
                geom.DeformationPoints = (double[])_paDeformPoints.Clone();
                geom.DeformationPercent = DeformationToolService.ComputeQuerschnittPercent(_paDeformPoints);
                break;
            case PaTool.BendAngle:
                geom.ToolType = OverlayToolType.BendAngle;
                geom.BendAngleDegrees = _paBendAngleDeg;
                geom.BendScale = _paBendScale;
                geom.BendOffsetX = _paBendOffsetX;
                geom.BendOffsetY = _paBendOffsetY;
                break;
            case PaTool.Lateral:
                geom.ToolType = OverlayToolType.LateralConnection;
                geom.LateralHour = _paLatHour;
                geom.LateralAngleDegrees = _paLatAngleDeg;
                geom.LateralDnPercent = _paLatDnPercent;
                geom.LatScale = _paLatScale;
                geom.LatOffsetX = _paLatOffsetX;
                geom.LatOffsetY = _paLatOffsetY;
                break;
        }
    }

    /// <summary>
    /// Liefert den vorgeschlagenen VSA-Code + Beschreibung fuer den aktuellen Zustand.
    /// </summary>
    internal (string Code, string Description, Dictionary<string, string> Parameters) GetPhotoAssistantSuggestion()
    {
        var p = new Dictionary<string, string>();
        var ic = CultureInfo.InvariantCulture;
        switch (_paActive)
        {
            case PaTool.Deformation:
                var perc = DeformationToolService.ComputeQuerschnittPercent(_paDeformPoints);
                p["deformation_percent"] = perc.ToString("F1", ic);
                p["deformation_points"] = string.Join(";", _paDeformPoints.Select(r => r.ToString("F3", ic)));
                var sugD = VsaCodeSuggester.ForDeformation(100 - perc);
                return (sugD.Code, sugD.Description, p);
            case PaTool.BendAngle:
                p["bend_angle_deg"] = _paBendAngleDeg.ToString("F1", ic);
                var sugB = VsaCodeSuggester.ForBendAngle(_paBendAngleDeg);
                return (sugB.Code, sugB.Description, p);
            case PaTool.Lateral:
                p["lateral_hour"] = _paLatHour.ToString(ic);
                p["lateral_angle_deg"] = _paLatAngleDeg.ToString("F1", ic);
                p["lateral_dn_percent"] = _paLatDnPercent.ToString("F1", ic);
                var sugL = VsaCodeSuggester.ForLateral(_paLatHour, _paLatDnPercent, _paLatAngleDeg, (int)Math.Round(_paPipeDnMm));
                return (sugL.Code, sugL.Description, p);
        }
        return ("", "", p);
    }

    /// <summary>Stellt Slider/Drag-Offsets aus einer gespeicherten OverlayGeometry wieder her.</summary>
    internal void RestoreFromOverlay(OverlayGeometry geom)
    {
        if (geom is null) return;

        if (geom.ToolType == OverlayToolType.Deformation && geom.DeformationPoints?.Length == 16)
        {
            _paDeformPoints = (double[])geom.DeformationPoints.Clone();
        }
        if (geom.ToolType == OverlayToolType.BendAngle)
        {
            if (geom.BendAngleDegrees.HasValue) _paBendAngleDeg = geom.BendAngleDegrees.Value;
            if (geom.BendScale.HasValue) _paBendScale = geom.BendScale.Value;
            if (geom.BendOffsetX.HasValue) _paBendOffsetX = geom.BendOffsetX.Value;
            if (geom.BendOffsetY.HasValue) _paBendOffsetY = geom.BendOffsetY.Value;
        }
        if (geom.ToolType == OverlayToolType.LateralConnection)
        {
            if (geom.LateralHour.HasValue) _paLatHour = geom.LateralHour.Value;
            if (geom.LateralAngleDegrees.HasValue) _paLatAngleDeg = geom.LateralAngleDegrees.Value;
            if (geom.LateralDnPercent.HasValue) _paLatDnPercent = geom.LateralDnPercent.Value;
            if (geom.LatScale.HasValue) _paLatScale = geom.LatScale.Value;
            if (geom.LatOffsetX.HasValue) _paLatOffsetX = geom.LatOffsetX.Value;
            if (geom.LatOffsetY.HasValue) _paLatOffsetY = geom.LatOffsetY.Value;
        }
    }

    /// <summary>
    /// Rendert das aktuelle Overlay (Foto + Schablone) und speichert es als
    /// &lt;originalPfad&gt;_measured.png. Liefert den Pfad oder null bei Fehler.
    /// </summary>
    internal string? CapturePhotoWithOverlay(string originalPath)
    {
        try
        {
            var w = (int)Math.Max(PhotoContainer.ActualWidth, 1);
            var h = (int)Math.Max(PhotoContainer.ActualHeight, 1);
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(PhotoContainer);
            var dir = System.IO.Path.GetDirectoryName(originalPath) ?? ".";
            var name = System.IO.Path.GetFileNameWithoutExtension(originalPath);
            var dst = System.IO.Path.Combine(dir, $"{name}_measured.png");
            using var fs = File.Create(dst);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            enc.Save(fs);
            return dst;
        }
        catch { return null; }
    }

    // ── Hilfen ───────────────────────────────────────────────────────────

    /// <summary>HSL nach RGB (h: 0..360, s/l: 0..1).</summary>
    private static Color HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hh = (h % 360) / 60.0;
        double x = c * (1 - Math.Abs(hh % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (hh < 1) { r = c; g = x; }
        else if (hh < 2) { r = x; g = c; }
        else if (hh < 3) { g = c; b = x; }
        else if (hh < 4) { g = x; b = c; }
        else if (hh < 5) { r = x; b = c; }
        else { r = c; b = x; }
        double m = l - c / 2;
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
