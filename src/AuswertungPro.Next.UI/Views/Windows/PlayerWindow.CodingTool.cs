using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Coding-Tool-Buttons + Calibration + Canvas-Mouse-Handler
// extrahiert aus PlayerWindow.xaml.cs.
//
// Enthaelt die Tool-Aktivierung (Bend/Level/Intrusion), das Pause-/Resume-
// Toggling der Maus-Eingabe, die Kalibrier-Routine (DN -> mm/Pixel) und
// die Canvas-Mouse-Handler fuer Schema-Konstruktion (Down/Move/Up/Wheel).
public partial class PlayerWindow
{
    private void CodingToolBend_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.PipeDirection, SchemaType.PipeDirection);

    private void CodingToolLevel_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.Level, SchemaType.FillLevel, LevelMode.Water);

    private void CodingToolIntrusion_Click(object sender, RoutedEventArgs e)
        => SetCodingTool(sender, OverlayToolType.Level, SchemaType.Intrusion, LevelMode.Obstacle);

    private string? _activeCodingToolName;

    private void SetCodingTool(
        object activeBtn,
        OverlayToolType tool,
        SchemaType? schemaType = null,
        LevelMode? levelMode = null)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        _codingIsCalibrating = false;
        _codingCalibStart = null;

        // Popup schliessen
        ToolsDropdownPopup.IsOpen = false;

        // Toggle: gleiches Tool nochmal → deaktivieren
        string btnName = (activeBtn as FrameworkElement)?.Name ?? "";
        bool activate = !string.Equals(_activeCodingToolName, btnName);
        _activeCodingToolName = activate ? btnName : null;

        if (activate && levelMode.HasValue)
            _codingOverlayService.ActiveLevelMode = levelMode.Value;

        _codingOverlayService.ActiveTool = activate ? tool : OverlayToolType.None;
        _codingSchemaType = activate ? schemaType : null;
        _codingSchemaManager.Cancel();

        // Aktives Tool-Label anzeigen
        string label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();
        TxtActiveToolLabel.Text = activate ? label : "";

        // Offene Zeichnung verwerfen, damit das naechste Tool sauber startet.
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        // Overlay-Canvas oeffnen/schliessen je nach Aktivierung
        if (activate && !CodingOverlayPopup.IsOpen)
        {
            _player.SetPause(true);
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
        }
        else if (!activate && CodingOverlayPopup.IsOpen)
        {
            CodingOverlayPopup.IsOpen = false;
        }

        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private void SuspendCodingOverlayInput()
    {
        _codingOverlaySuspendDepth++;
        if (_codingOverlaySuspendDepth > 1)
            return;

        if (CodingOverlayCanvas.IsMouseCaptured)
            CodingOverlayCanvas.ReleaseMouseCapture();
        _codingSchemaManager.EndDrag();
        _codingOverlayService?.CancelDraw();
        _codingOverlayWasOpenBeforeSuspend = CodingOverlayPopup.IsOpen;
        CodingOverlayCanvas.IsHitTestVisible = false;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
        if (_codingOverlayWasOpenBeforeSuspend)
            CodingOverlayPopup.IsOpen = false;
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        if (_codingOverlayWasOpenBeforeSuspend)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayCursor();
        _codingOverlayWasOpenBeforeSuspend = false;
    }

    private void UpdateCodingOverlayCursor()
    {
        if (!CodingOverlayPopup.IsOpen)
        {
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
            return;
        }

        var activeTool = _codingOverlayService?.ActiveTool ?? OverlayToolType.None;
        var isInteractive = _codingIsCalibrating || activeTool != OverlayToolType.None;
        CodingOverlayCanvas.Cursor = isInteractive ? Cursors.Cross : Cursors.Arrow;
    }

    private void CodingCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        ToolsDropdownPopup.IsOpen = false;
        _codingIsCalibrating = !_codingIsCalibrating;
        _codingCalibStart = null;
        _codingOverlayService.ActiveTool = OverlayToolType.None;
        _activeCodingToolName = _codingIsCalibrating ? "BtnCodingCalibrate" : null;
        TxtActiveToolLabel.Text = _codingIsCalibrating ? "Kalibrieren" : "";

        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        CodingCalibrationHint.Visibility = _codingIsCalibrating ? Visibility.Visible : Visibility.Collapsed;
        TxtCodingCalibHint.Text = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private bool IsCodingSchemaToolSelected()
        => _codingSchemaType.HasValue
           && _codingOverlayService?.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level or OverlayToolType.PipeDirection;

    private SchemaOverlayBase? CreateCodingSchemaOverlay()
    {
        if (_codingOverlayService == null || _codingSchemaType == null)
            return null;

        return _codingSchemaType.Value switch
        {
            SchemaType.PipeBend => new PipeBendSchema
            {
                SnapEnabled = _codingOverlayService.PipeBendSnapEnabled
            },
            SchemaType.FillLevel => new FillLevelSchema
            {
                Mode = _codingOverlayService.ActiveLevelMode
            },
            SchemaType.Intrusion => new IntrusionSchema(),
            SchemaType.PipeDirection => new PipeDirectionSchema(),
            _ => null
        };
    }

    private string GetDefaultCodingSchemaHandleId()
        => _codingSchemaType switch
        {
            SchemaType.PipeBend => "vertex",
            SchemaType.FillLevel => "level",
            SchemaType.Intrusion => "depth",
            SchemaType.PipeDirection => "center1",
            _ => "vertex"
        };

    private OverlayGeometry? BuildCodingSchemaGeometry()
    {
        if (_codingSchemaManager.Active is PipeBendSchema bend)
        {
            var (arm1, arm2) = bend.GetArmEndpoints();
            var angle = bend.SnapEnabled
                ? new[] { 15d, 30d, 45d, 90d }
                    .OrderBy(candidate => Math.Abs(candidate - bend.AngleDeg))
                    .First()
                : Math.Round(bend.AngleDeg, 1);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points = new List<NormalizedPoint> { arm1, bend.Center, arm2 },
                ArcDegrees = Math.Round(angle, 1)
            };
        }

        if (_codingSchemaManager.Active is FillLevelSchema fill)
        {
            double levelY = fill.GetLevelLineY();
            double dy = levelY - fill.PipeCenter.Y;
            double halfChord = Math.Sqrt(Math.Max(0, fill.PipeRadius * fill.PipeRadius - dy * dy));
            double pct = OverlayToolService.CircleSegmentPercent(fill.FillRatio);
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint>
                {
                    new(fill.PipeCenter.X - halfChord, levelY),
                    new(fill.PipeCenter.X + halfChord, levelY)
                },
                FillPercent = Math.Round(pct, 1),
                LevelSubMode = fill.Mode
            };
        }

        if (_codingSchemaManager.Active is IntrusionSchema intrusion)
        {
            var edge = intrusion.GetEdgePoint();
            var tip = intrusion.GetIntrusionTip();
            var (left, right) = intrusion.GetSpreadEdges();
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Level,
                Points = new List<NormalizedPoint> { edge, tip, intrusion.PipeCenter, left, right },
                FillPercent = Math.Round(intrusion.DepthRatio * 100.0, 1),
                LevelSubMode = LevelMode.Obstacle,
                ClockFrom = Math.Round(intrusion.ClockHour, 1)
            };
        }

        if (_codingSchemaManager.Active is PipeDirectionSchema pipeDir)
        {
            return pipeDir.BuildGeometry();
        }

        return null;
    }

    private void UpdateCodingSchemaOverlay(bool enableCreateEvent)
    {
        if (_codingVm == null) return;

        _codingVm.CurrentOverlay = BuildCodingSchemaGeometry();
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
        BtnCodingCreateEvent.IsEnabled = enableCreateEvent && _codingVm.CurrentOverlay != null;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderActiveCodingSchema();
    }

    private void ClearCodingSchemaOverlay(bool redraw)
    {
        _codingSchemaManager.Cancel();
        if (_codingVm != null)
            _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
        if (redraw)
            RedrawCodingCanvas(includeManualOverlay: false);
    }

    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker hat Vorrang: Rechteck ziehen
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseDown(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }
        // Input-Phase: Canvas-Klicks ignorieren (ComboBox ist aktiv)
        if (_eingabemarkerPhase == EingabemarkerPhase.Input ||
            _eingabemarkerPhase == EingabemarkerPhase.Analyzing)
        {
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating)
        {
            _codingCalibStart = norm;
            CodingOverlayCanvas.CaptureMouse();
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            return;
        }

        if (_codingOverlayService.ActiveTool == OverlayToolType.None) return;

        if (IsCodingSchemaToolSelected())
        {
            if (!_codingSchemaManager.IsActive)
            {
                // Schema noch nicht platziert oder wartet auf zweiten Klick (PipeDirection)
                if (_codingSchemaManager.Active is PipeDirectionSchema pd && pd.IsWaitingForSecondClick)
                {
                    // Zweiter Klick: Platziert die zweite Ellipse → Adjusting
                    _codingSchemaManager.Place(norm);
                    UpdateCodingSchemaOverlay(enableCreateEvent: true);
                    return;
                }

                var schema = CreateCodingSchemaOverlay();
                if (schema == null) return;
                _codingSchemaManager.Activate(schema, _codingOverlayService.Calibration);
                _codingSchemaManager.Place(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
                return;
            }

            var handleId = _codingSchemaManager.HitTest(norm, 0.035) ?? GetDefaultCodingSchemaHandleId();
            _codingSchemaManager.BeginDrag(handleId);
            _codingSchemaManager.UpdateDrag(norm);
            CodingOverlayCanvas.CaptureMouse();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        // Multi-Punkt-Werkzeug (Winkelmesser: 3 Klicks)
        if (_codingOverlayService.IsMultiPointTool)
        {
            // Beim ersten Klick Reset
            if (_codingOverlayService.DrawPointCount == 0)
            {
                _codingVm.CurrentOverlay = null;
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }

            bool complete = _codingVm.OnCanvasMultiPointClick(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();

            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: !complete);

            if (complete)
            {
                UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
                BtnCodingCreateEvent.IsEnabled = true;
                if (BtnCodingLiveAi.IsChecked == true && _codingVm.CurrentOverlay != null)
                    AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHint");
            }
            return; // Kein CaptureMouse bei Multi-Punkt
        }

        // Standard 2-Punkt-Werkzeug (Klick+Drag)
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);

        _codingVm.OnCanvasMouseDown(norm);
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Eingabemarker Rechteck-Drag
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing && _eingabemarkerPreviewRect != null)
        {
            EingabemarkerCanvas_MouseMove(e.GetPosition(CodingOverlayCanvas));
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();

            var p1 = CodingNormToPixel(_codingCalibStart);
            var p2 = CodingNormToPixel(norm);
            _codingPreviewLine = new System.Windows.Shapes.Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Magenta,
                StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Tag = "overlay_preview"
            };
            CodingOverlayCanvas.Children.Add(_codingPreviewLine);
            double pxLen = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            TxtCodingCalibHint.Text = $"Referenzlinie: {pxLen:F0} px";
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsActive)
        {
            if (_codingSchemaManager.IsDragging)
            {
                _codingSchemaManager.UpdateDrag(norm);
                UpdateCodingSchemaOverlay(enableCreateEvent: true);
            }
            return;
        }

        // Multi-Punkt-Vorschau (Winkelmesser: Mausbewegung zwischen Klicks)
        if (_codingOverlayService.IsMultiPointTool && _codingOverlayService.DrawPointCount > 0)
        {
            _codingVm.OnCanvasMultiPointMove(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            RenderAiOverlays();
            RenderReferenceDn();
            UpdateToolBadge();
            if (_codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseMove(norm);
        if (_codingVm.CurrentOverlay == null) return;

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: true, labelAnchor: norm);
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker Rechteck fertig
        if (_eingabemarkerPhase == EingabemarkerPhase.Drawing)
        {
            EingabemarkerCanvas_MouseUp(e.GetPosition(CodingOverlayCanvas));
            e.Handled = true;
            return;
        }

        if (_codingOverlayService == null || _codingVm == null) return;
        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        if (_codingIsCalibrating && _codingCalibStart != null)
        {
            CodingOverlayCanvas.ReleaseMouseCapture();
            ApplyCodingCalibration(_codingCalibStart, norm);
            return;
        }

        if (IsCodingSchemaToolSelected() && _codingSchemaManager.IsDragging)
        {
            _codingSchemaManager.UpdateDrag(norm);
            _codingSchemaManager.EndDrag();
            CodingOverlayCanvas.ReleaseMouseCapture();
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return;
        }

        if (!_codingOverlayService.IsDrawing) return;
        _codingVm.OnCanvasMouseUp(norm);
        CodingOverlayCanvas.ReleaseMouseCapture();

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();

        if (_codingVm.CurrentOverlay != null)
        {
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

            // Mark-Modus ODER Rectangle-Overlay: direkt VsaCodeExplorer oeffnen
            // + SAM + Training speichern via HandleMarkDrawingComplete.
            //
            // User-Wunsch 2026-04-26: "Im Codiermodus, waere es gut wenn ich
            // markiere BBox das Fenster zum Codieren jedesmal aufgeht."
            // Vorher: Rectangle-Overlay rief nur SAM auf, User musste manuell
            // auf "Befund erstellen"-Button klicken UND vorher Code waehlen.
            // Jetzt: Rectangle-Overlay = sofort VsaCodeExplorer wie im Mark-Modus.
            //
            // Andere Geometrien (Linie/Bogen/Stretch/Punkt/Level/PipeBend) bleiben
            // manuell — fuer Streckenschaeden/Bogenwinkel etc. ist der zweistufige
            // Workflow korrekt (zuerst messen, dann Code).
            if (_markToolType != OverlayToolType.None
                || _codingVm.CurrentOverlay.ToolType == OverlayToolType.Rectangle)
            {
                HandleMarkDrawingComplete();
                return;
            }

            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay).SafeFireAndForget("OverlayHintAutoAi");
        }
        else
        {
            UpdateCodingOverlayInfo(null);
            BtnCodingCreateEvent.IsEnabled = false;
        }
    }

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mausrad: Winkel der PipeBend-Schablone aendern (5° pro Schritt)
        if (_codingSchemaManager.Active is PipeBendSchema bend && _codingSchemaManager.IsActive)
        {
            double delta = e.Delta > 0 ? 5 : -5;
            bend.AdjustAngle(delta);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            e.Handled = true;
        }
    }

    private void ApplyCodingCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        if (_codingOverlayService == null) return;
        var p1 = CodingNormToPixel(start);
        var p2 = CodingNormToPixel(end);
        double pixelDiameter = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

        if (pixelDiameter < 10)
        {
            TxtCodingCalibHint.Text = "Linie zu kurz - bitte nochmal";
            _codingCalibStart = null;
            return;
        }

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double normDiameter = Math.Sqrt(dx * dx + dy * dy);
        int dn = _codingOverlayService.Calibration?.NominalDiameterMm ?? 300;

        var cal = new PipeCalibration
        {
            NominalDiameterMm = dn,
            PipePixelDiameter = pixelDiameter,
            NormalizedDiameter = normDiameter,
            PipeCenter = center,
            WasManuallyCalibrated = true
        };
        _codingOverlayService.SetCalibration(cal);
        _codingSchemaManager.Active?.ApplyCalibration(cal);

        TxtCodingCalibStatus.Text = $"Kalibriert: {cal.MmPerNormUnit:F1} mm/norm";
        TxtCodingCalibHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        if (string.Equals(_activeCodingToolName, "BtnCodingCalibrate"))
            _activeCodingToolName = null;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        UpdateCodingOverlayCursor();
        if (_codingSchemaManager.IsActive)
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
    }
}
