using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (!_codingSessionHost.HasViewModel) return;

        _codingOverlayRenderController.RenderAiOverlays(
            _codingSessionHost.Events,
            _codingOverlayToolHost.Calibration);
    }
}
