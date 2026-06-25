using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool IsCodingSchemaToolSelected()
        => _codingSchemaType.HasValue
           && _codingOverlayToolHost.ActiveTool is OverlayToolType.PipeBend or OverlayToolType.Level;

    private SchemaOverlayBase? CreateCodingSchemaOverlay()
    {
        if (!_codingOverlayToolHost.HasOverlayService)
            return null;

        return CodingSchemaOverlayBuilder.Create(
            _codingSchemaType,
            _codingOverlayToolHost.PipeBendSnapEnabled,
            _codingOverlayToolHost.ActiveLevelMode);
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
            _codingSchemaManager.Activate(schema, _codingOverlayToolHost.Calibration);
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
        if (!_codingSessionHost.HasViewModel) return;

        var overlay = BuildCodingSchemaGeometry();
        _codingSessionHost.SetCurrentOverlay(overlay);
        UpdateCodingOverlayInfo(overlay);
        CodingOverlayInputControls.SetCreateEventEnabled(
            BtnCodingCreateEvent,
            enableCreateEvent && overlay != null);

        ClearTransientCodingCanvas(clearManualOverlay: true);
        RenderAiOverlays();
        RenderReferenceDn();
        UpdateToolBadge();
        RenderActiveCodingSchema();
    }

    private void ClearCodingSchemaOverlay(bool redraw)
    {
        _codingSchemaManager.Cancel();
        _codingSessionHost.ClearCurrentOverlay();
        CodingOverlayInputControls.SetCreateEventEnabled(BtnCodingCreateEvent, false);
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
