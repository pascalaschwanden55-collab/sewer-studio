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
        var result = CodingSchemaOverlayInputWorkflow.MouseDown(
            new CodingSchemaOverlayMouseDownRequest(
                IsCodingSchemaToolSelected(),
                _codingSchemaManager.IsActive),
            new CodingSchemaOverlayMouseDownActions(
                CreateAndActivateSchema: () =>
                {
                    var schema = CreateCodingSchemaOverlay();
                    if (schema == null)
                        return false;

                    _codingSchemaManager.Activate(schema, _codingOverlayToolHost.Calibration);
                    return true;
                },
                PlaceSchema: () => _codingSchemaManager.Place(norm),
                ResolveHandleId: () => _codingSchemaManager.HitTest(norm, 0.035) ?? GetDefaultCodingSchemaHandleId(),
                BeginDrag: _codingSchemaManager.BeginDrag,
                UpdateDrag: () => _codingSchemaManager.UpdateDrag(norm),
                CaptureMouse: () => { CodingOverlayCanvas.CaptureMouse(); },
                UpdateOverlay: () => UpdateCodingSchemaOverlay(enableCreateEvent: true)));

        return result.Handled;
    }

    private bool TryHandleCodingSchemaMouseMove(NormalizedPoint norm)
    {
        return CodingSchemaOverlayInputWorkflow.MouseMove(
            new CodingSchemaOverlayMouseMoveRequest(
                IsCodingSchemaToolSelected(),
                _codingSchemaManager.IsActive,
                _codingSchemaManager.IsDragging),
            new CodingSchemaOverlayMouseMoveActions(
                UpdateDrag: () => _codingSchemaManager.UpdateDrag(norm),
                UpdateOverlay: () => UpdateCodingSchemaOverlay(enableCreateEvent: true)))
            .Handled;
    }

    private bool TryHandleCodingSchemaMouseUp(NormalizedPoint norm)
    {
        return CodingSchemaOverlayInputWorkflow.MouseUp(
            new CodingSchemaOverlayMouseUpRequest(
                IsCodingSchemaToolSelected(),
                _codingSchemaManager.IsDragging),
            new CodingSchemaOverlayMouseUpActions(
                UpdateDrag: () => _codingSchemaManager.UpdateDrag(norm),
                EndDrag: _codingSchemaManager.EndDrag,
                ReleaseMouseCapture: CodingOverlayCanvas.ReleaseMouseCapture,
                UpdateOverlay: () => UpdateCodingSchemaOverlay(enableCreateEvent: true)))
            .Handled;
    }

    private void UpdateCodingSchemaOverlay(bool enableCreateEvent)
    {
        OverlayGeometry? overlay = null;

        CodingSchemaOverlayUpdateWorkflow.Execute(
            new CodingSchemaOverlayUpdateRequest(
                _codingSessionHost.HasViewModel,
                enableCreateEvent),
            new CodingSchemaOverlayUpdateActions(
                BuildSetAndReportOverlay: () =>
                {
                    overlay = BuildCodingSchemaGeometry();
                    _codingSessionHost.SetCurrentOverlay(overlay);
                    return overlay != null;
                },
                UpdateOverlayInfo: () => UpdateCodingOverlayInfo(overlay),
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                ClearTransientCodingCanvas: () => ClearTransientCodingCanvas(clearManualOverlay: true),
                RenderAiOverlays: RenderAiOverlays,
                RenderReferenceDn: RenderReferenceDn,
                UpdateToolBadge: UpdateToolBadge,
                RenderActiveCodingSchema: RenderActiveCodingSchema));
    }

    private void ClearCodingSchemaOverlay(bool redraw)
    {
        CodingSchemaOverlayClearWorkflow.Execute(
            new CodingSchemaOverlayClearRequest(redraw),
            new CodingSchemaOverlayClearActions(
                CancelSchema: _codingSchemaManager.Cancel,
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                SetCreateEventEnabled: enabled => CodingOverlayInputControls.SetCreateEventEnabled(
                    BtnCodingCreateEvent,
                    enabled),
                ClearOverlayInfo: () => UpdateCodingOverlayInfo(null),
                RedrawCodingCanvas: includeManualOverlay => RedrawCodingCanvas(includeManualOverlay)));
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
