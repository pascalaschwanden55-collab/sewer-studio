using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

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
                _codingEingabemarkerInteractionController.OverlayInputState,
                _codingOverlayToolHost.HasOverlayService,
                _codingSessionHost.HasViewModel,
                _codingOverlayToolHost.ActiveTool == OverlayToolType.None,
                _codingOverlayToolHost.IsMultiPointTool),
            new CodingOverlayInputMouseDownActions(
                HandleEingabemarkerMouseDown: () =>
                    _codingEingabemarkerInteractionController.MouseDown(e.GetPosition(CodingOverlayCanvas)),
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
                IsEingabemarkerDrawingWithPreview:
                    _codingEingabemarkerInteractionController.IsDrawingWithPreview,
                HasOverlayService: _codingOverlayToolHost.HasOverlayService,
                HasViewModel: _codingSessionHost.HasViewModel),
            new CodingOverlayInputMouseMoveActions(
                HandleEingabemarkerMouseMove: () =>
                    _codingEingabemarkerInteractionController.MouseMove(e.GetPosition(CodingOverlayCanvas)),
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
                IsEingabemarkerDrawing: _codingEingabemarkerInteractionController.IsDrawing,
                HasOverlayService: _codingOverlayToolHost.HasOverlayService,
                HasViewModel: _codingSessionHost.HasViewModel),
            new CodingOverlayInputMouseUpActions(
                HandleEingabemarkerMouseUp: () =>
                    _codingEingabemarkerInteractionController.MouseUp(e.GetPosition(CodingOverlayCanvas)),
                MarkHandled: () => e.Handled = true,
                TryFinishCalibration: () => TryFinishCodingCalibration(GetNorm()),
                TryHandleSchemaMouseUp: () => TryHandleCodingSchemaMouseUp(GetNorm()),
                TryHandleStandardMouseUp: () => TryHandleCodingStandardMouseUp(GetNorm())));
    }

}
