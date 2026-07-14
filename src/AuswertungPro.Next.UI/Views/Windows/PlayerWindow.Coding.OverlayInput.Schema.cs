using AuswertungPro.Next.Domain.Models;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private OverlayGeometry? BuildCodingSchemaGeometry()
        => _codingSchemaOverlayController.BuildGeometry();

    private bool TryHandleCodingSchemaMouseDown(NormalizedPoint norm)
        => _codingSchemaOverlayController.MouseDown(norm);

    private bool TryHandleCodingSchemaMouseMove(NormalizedPoint norm)
        => _codingSchemaOverlayController.MouseMove(norm);

    private bool TryHandleCodingSchemaMouseUp(NormalizedPoint norm)
        => _codingSchemaOverlayController.MouseUp(norm);

    private void UpdateCodingSchemaOverlay(bool enableCreateEvent)
        => _codingSchemaOverlayController.Update(enableCreateEvent);

    private void ClearCodingSchemaOverlay(bool redraw)
        => _codingSchemaOverlayController.Clear(redraw);

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        => _codingSchemaOverlayController.MouseWheel(
            e.Delta,
            () => e.Handled = true);
}
