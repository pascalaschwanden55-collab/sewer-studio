using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderReferenceDn()
    {
        _codingOverlayRenderController.RenderReferenceDn(
            _codingOverlayToolHost.Calibration,
            _codingOverlayRenderState.ShowReferenceDn);
    }
}
