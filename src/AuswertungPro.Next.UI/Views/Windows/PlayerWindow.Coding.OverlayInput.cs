using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        if (TryStartCodingCalibration(norm))
            return;

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

        if (TryPreviewCodingCalibration(norm))
            return;

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

        if (TryFinishCodingCalibration(norm))
            return;

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
}
