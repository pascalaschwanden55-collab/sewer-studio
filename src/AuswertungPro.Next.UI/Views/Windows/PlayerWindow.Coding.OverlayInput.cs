using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- Coding Canvas-Events ---

    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        NormalizedPoint? norm = null;
        NormalizedPoint GetNorm() => norm ??= CodingPixelToNorm(e.GetPosition(CodingOverlayCanvas));

        CodingOverlayInputMouseWorkflow.MouseDown(
            new CodingOverlayInputMouseDownRequest(
                CurrentCodingOverlayInputEingabemarkerState(),
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.ActiveTool == OverlayToolType.None,
                _codingOverlayToolHost.IsMultiPointTool),
            new CodingOverlayInputMouseDownActions(
                HandleEingabemarkerMouseDown: () => EingabemarkerCanvas_MouseDown(e.GetPosition(CodingOverlayCanvas)),
                MarkHandled: () => e.Handled = true,
                TryStartCalibration: () => TryStartCodingCalibration(GetNorm()),
                TryHandleSchemaMouseDown: () => TryHandleCodingSchemaMouseDown(GetNorm()),
                HandleMultiPointMouseDown: () => HandleCodingMultiPointMouseDown(GetNorm()),
                HandleStandardMouseDown: () => HandleCodingStandardMouseDown(GetNorm())));
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        NormalizedPoint? norm = null;
        NormalizedPoint GetNorm() => norm ??= CodingPixelToNorm(e.GetPosition(CodingOverlayCanvas));

        CodingOverlayInputMouseWorkflow.MouseMove(
            new CodingOverlayInputMouseMoveRequest(
                IsEingabemarkerDrawingWithPreview: _eingabemarkerPhase == EingabemarkerPhase.Drawing &&
                    _eingabemarkerPreviewRect != null,
                HasOverlayService: _codingOverlayToolHost.HasOverlayService,
                HasViewModel: _codingSessionHost.HasViewModel),
            new CodingOverlayInputMouseMoveActions(
                HandleEingabemarkerMouseMove: () => EingabemarkerCanvas_MouseMove(e.GetPosition(CodingOverlayCanvas)),
                TryPreviewCalibration: () => TryPreviewCodingCalibration(GetNorm()),
                TryHandleSchemaMouseMove: () => TryHandleCodingSchemaMouseMove(GetNorm()),
                TryHandleMultiPointMouseMove: () => TryHandleCodingMultiPointMouseMove(GetNorm()),
                TryHandleStandardMouseMove: () => TryHandleCodingStandardMouseMove(GetNorm())));
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        NormalizedPoint? norm = null;
        NormalizedPoint GetNorm() => norm ??= CodingPixelToNorm(e.GetPosition(CodingOverlayCanvas));

        CodingOverlayInputMouseWorkflow.MouseUp(
            new CodingOverlayInputMouseUpRequest(
                IsEingabemarkerDrawing: _eingabemarkerPhase == EingabemarkerPhase.Drawing,
                HasOverlayService: _codingOverlayToolHost.HasOverlayService,
                HasViewModel: _codingSessionHost.HasViewModel),
            new CodingOverlayInputMouseUpActions(
                HandleEingabemarkerMouseUp: () => EingabemarkerCanvas_MouseUp(e.GetPosition(CodingOverlayCanvas)),
                MarkHandled: () => e.Handled = true,
                TryFinishCalibration: () => TryFinishCodingCalibration(GetNorm()),
                TryHandleSchemaMouseUp: () => TryHandleCodingSchemaMouseUp(GetNorm()),
                TryHandleStandardMouseUp: () => TryHandleCodingStandardMouseUp(GetNorm())));
    }

    private CodingOverlayInputEingabemarkerState CurrentCodingOverlayInputEingabemarkerState()
        => _eingabemarkerPhase switch
        {
            EingabemarkerPhase.Drawing => CodingOverlayInputEingabemarkerState.Drawing,
            EingabemarkerPhase.Input or EingabemarkerPhase.Analyzing => CodingOverlayInputEingabemarkerState.InputBlocked,
            _ => CodingOverlayInputEingabemarkerState.Inactive
        };
}
