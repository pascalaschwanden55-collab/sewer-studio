using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        CodingAiOverlayRenderCommandWorkflow.Execute(
            new CodingAiOverlayRenderCommandRequest(_codingSessionHost.HasViewModel),
            new CodingAiOverlayRenderCommandActions(
                RenderAiOverlays: () => _codingOverlayRenderController.RenderAiOverlays(
                    _codingSessionHost.Events,
                    _codingOverlayToolHost.Calibration)));
    }
}
