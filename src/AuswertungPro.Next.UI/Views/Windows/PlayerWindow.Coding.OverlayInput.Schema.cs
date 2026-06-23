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
