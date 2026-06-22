using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;
using Rectangle = System.Windows.Shapes.Rectangle;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- Coding Werkzeuge ---

    // Vereinfachte Werkzeuge: nur Kalibrieren + Rechteck (Rest im PhotoAssistant)
    // Rechteck nutzt ActivateMarkTool → nach Zeichnen oeffnet sich automatisch der Code-Katalog
    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Markieren");

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
        string label = (activeBtn as ContentControl)?.Content?.ToString() ?? tool.ToString();
        var selection = CodingToolSelectionPolicy.Build(
            _activeCodingToolName,
            btnName,
            label,
            tool,
            schemaType,
            levelMode);

        _activeCodingToolName = selection.ActiveToolName;
        if (selection.LevelModeToApply.HasValue)
            _codingOverlayService.ActiveLevelMode = selection.LevelModeToApply.Value;

        _codingOverlayService.ActiveTool = selection.ActiveTool;
        _codingSchemaType = selection.ActiveSchemaType;
        _codingSchemaManager.Cancel();

        // Aktives Tool-Label anzeigen
        TxtActiveToolLabel.Text = selection.LabelText;

        // Offene Zeichnung verwerfen, damit das naechste Tool sauber startet.
        _codingVm.CurrentOverlay = null;
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
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
        // Das Popup ist ein eigenes transparentes Top-Level-HWND und liegt grafisch
        // UEBER eigenen Dialogen (Loeschen-Bestaetigung, VsaCodeExplorer). IsHitTestVisible=false
        // nimmt nur die Maus weg, der #01000000-Schleier + Kreise bleiben sichtbar und stoeren.
        // Canvas-Inhalt zusaetzlich ausblenden (NICHT Popup.IsOpen togglen -> kein HWND-Flicker,
        // depth-gezaehlt reentrant-sicher, kein Doppel-Redraw). Resume macht es wieder sichtbar.
        CodingOverlayCanvas.Visibility = Visibility.Hidden;
        CodingOverlayCanvas.Cursor = Cursors.Arrow;
    }

    private void ResumeCodingOverlayInput()
    {
        if (_codingOverlaySuspendDepth <= 0)
            return;

        _codingOverlaySuspendDepth--;
        if (_codingOverlaySuspendDepth > 0)
            return;

        // Canvas-Inhalt wieder einblenden (Gegenstueck zum Ausblenden in SuspendCodingOverlayInput).
        CodingOverlayCanvas.Visibility = Visibility.Visible;

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

    private void HideCodingOverlayForExternalWindow()
    {
        _codingOverlayWasOpenBeforeExternalHide = CodingOverlayPopup.IsOpen;
        SuspendCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
            CodingOverlayPopup.IsOpen = false;
    }

    private void CodingScreenshot_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (WindowClipboardCaptureService.TryCopyWindowToClipboard(this))
            ShowCodingScreenshotToast("Fenster in Zwischenablage kopiert");
    }

    private void ShowCodingScreenshotToast(string msg)
    {
        try
        {
            LiveDetectionStatusText.Text = msg;
            LiveDetectionStatusText.Visibility = System.Windows.Visibility.Visible;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2.5) };
            t.Tick += (s, ev) => { LiveDetectionStatusText.Visibility = System.Windows.Visibility.Collapsed; t.Stop(); };
            t.Start();
        }
        catch { }
    }

    private void RestoreCodingOverlayAfterExternalWindow()
    {
        ResumeCodingOverlayInput();
        if (_codingOverlayWasOpenBeforeExternalHide)
        {
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            RedrawCodingCanvas(includeManualOverlay: _codingVm?.CurrentOverlay != null);
        }

        _codingOverlayWasOpenBeforeExternalHide = false;
    }

    private void UpdateCodingOverlayCursor()
    {
        var activeTool = _codingOverlayService?.ActiveTool ?? OverlayToolType.None;
        CodingOverlayCanvas.Cursor = CodingOverlayCursorPolicy.ShouldUseCrossCursor(
            CodingOverlayPopup.IsOpen,
            _codingIsCalibrating,
            activeTool)
            ? Cursors.Cross
            : Cursors.Arrow;
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
        TxtCodingCalibHint.Text = "Linie �ber den sichtbaren Rohrdurchmesser zeichnen";
        UpdateCodingOverlayCursor();
        RedrawCodingCanvas(includeManualOverlay: false);
    }

    private bool IsCodingSchemaToolSelected()
        => _codingSchemaType.HasValue
           && _codingOverlayService?.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level;

    private SchemaOverlayBase? CreateCodingSchemaOverlay()
    {
        if (_codingOverlayService == null)
            return null;

        return CodingSchemaOverlayBuilder.Create(
            _codingSchemaType,
            _codingOverlayService.PipeBendSnapEnabled,
            _codingOverlayService.ActiveLevelMode);
    }

    private string GetDefaultCodingSchemaHandleId()
        => CodingSchemaOverlayBuilder.GetDefaultHandleId(_codingSchemaType);

    private OverlayGeometry? BuildCodingSchemaGeometry()
        => CodingSchemaOverlayBuilder.BuildGeometry(_codingSchemaManager.Active);

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
                Tag = OverlayTags.Preview
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

            // Mark-Modus: direkt VsaCodeExplorer oeffnen + Training speichern
            if (_markToolType != OverlayToolType.None)
            {
                HandleMarkDrawingComplete();
                return;
            }

            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            BtnCodingCreateEvent.IsEnabled = true;

            // Wenn Auto-KI aktiv: Overlay-Zeichnung -> KI analysiert markierte Stelle
            if (BtnCodingLiveAi.IsChecked == true)
                _ = AnalyzeWithOverlayHintAsync(_codingVm.CurrentOverlay);
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
        int dn = _codingOverlayService.Calibration?.NominalDiameterMm ?? 300;
        var result = CodingManualCalibrationPolicy.Build(start, end, p1, p2, dn);
        if (!result.IsValid || result.Calibration == null)
        {
            TxtCodingCalibHint.Text = result.HintText;
            _codingCalibStart = null;
            return;
        }

        var cal = result.Calibration;
        _codingOverlayService.SetCalibration(cal);
        _codingSchemaManager.Active?.ApplyCalibration(cal);

        TxtCodingCalibStatus.Text = result.StatusText;
        TxtCodingCalibHint.Text = result.HintText;

        _codingIsCalibrating = false;
        _codingCalibStart = null;
        if (string.Equals(_activeCodingToolName, "BtnCodingCalibrate"))
            _activeCodingToolName = null;
        CodingCalibrationHint.Visibility = Visibility.Collapsed;
        UpdateCodingOverlayCursor();
        if (_codingSchemaManager.IsActive)
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
    }

    // Breite/Hoehe des sichtbaren Videobildes (aus dem Analyse-Frame), 0 = unbekannt.
    private double _codingVideoAspect;

    // Das tatsaechlich sichtbare Video-Rechteck im Overlay-Canvas: VLC zeigt das Video
    // formattreu (Letterbox/Pillarbox). Overlays muessen in DIESES Rechteck gerechnet werden,
    // nicht in die volle Flaeche - sonst werden z.B. 4:3-Befunde in einer 16:9-Flaeche verzerrt.
    private Rect GetCodingContentRect()
        => CodingOverlayViewportMapper.GetContentRect(
            CodingOverlayCanvas.ActualWidth,
            CodingOverlayCanvas.ActualHeight,
            _codingVideoAspect);

    private NormalizedPoint CodingPixelToNorm(Point pixel)
    {
        if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)
            UpdateCodingOverlayViewport();
        var r = GetCodingContentRect();
        return CodingOverlayViewportMapper.PixelToNorm(pixel, r);
    }

    private Point CodingNormToPixel(NormalizedPoint norm)
    {
        var r = GetCodingContentRect();
        return CodingOverlayViewportMapper.NormToPixel(norm, r);
    }

    private void ClearTransientCodingCanvas(bool clearManualOverlay)
    {
        var remove = CodingOverlayCanvas.Children
            .OfType<FrameworkElement>()
            .Where(el => CodingOverlayCleanupPolicy.ShouldRemoveTransientTag(el.Tag, clearManualOverlay))
            .ToList();

        foreach (var el in remove)
            CodingOverlayCanvas.Children.Remove(el);
    }

    private void RedrawCodingCanvas(bool includeManualOverlay)
    {
        UpdateCodingOverlayViewport();
        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();

        if (_codingSchemaManager.IsActive)
            RenderActiveCodingSchema();
        else if (includeManualOverlay && _codingVm?.CurrentOverlay != null)
            RenderOverlayGeometry(_codingVm.CurrentOverlay, isPreview: false);

        UpdateToolBadge();
    }
}
