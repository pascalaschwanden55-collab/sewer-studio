using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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

    private bool TryHandleCodingSchemaMouseDown(NormalizedPoint norm)
    {
        if (!IsCodingSchemaToolSelected())
            return false;

        if (!_codingSchemaManager.IsActive)
        {
            var schema = CreateCodingSchemaOverlay();
            if (schema == null) return true;
            _codingSchemaManager.Activate(schema, _codingOverlayService?.Calibration);
            _codingSchemaManager.Place(norm);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            return true;
        }

        var handleId = _codingSchemaManager.HitTest(norm, 0.035) ?? GetDefaultCodingSchemaHandleId();
        _codingSchemaManager.BeginDrag(handleId);
        _codingSchemaManager.UpdateDrag(norm);
        CodingOverlayCanvas.CaptureMouse();
        UpdateCodingSchemaOverlay(enableCreateEvent: true);
        return true;
    }

    private bool TryHandleCodingSchemaMouseMove(NormalizedPoint norm)
    {
        if (!IsCodingSchemaToolSelected() || !_codingSchemaManager.IsActive)
            return false;

        if (_codingSchemaManager.IsDragging)
        {
            _codingSchemaManager.UpdateDrag(norm);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
        }

        return true;
    }

    private bool TryHandleCodingSchemaMouseUp(NormalizedPoint norm)
    {
        if (!IsCodingSchemaToolSelected() || !_codingSchemaManager.IsDragging)
            return false;

        _codingSchemaManager.UpdateDrag(norm);
        _codingSchemaManager.EndDrag();
        CodingOverlayCanvas.ReleaseMouseCapture();
        UpdateCodingSchemaOverlay(enableCreateEvent: true);
        return true;
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

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mausrad: Winkel der PipeBend-Schablone aendern (5 Grad pro Schritt)
        if (_codingSchemaManager.Active is PipeBendSchema bend && _codingSchemaManager.IsActive)
        {
            double delta = e.Delta > 0 ? 5 : -5;
            bend.AdjustAngle(delta);
            UpdateCodingSchemaOverlay(enableCreateEvent: true);
            e.Handled = true;
        }
    }
}
