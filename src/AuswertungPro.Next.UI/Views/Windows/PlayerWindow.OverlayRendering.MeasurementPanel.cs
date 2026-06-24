using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void UpdateCodingOverlayInfo(OverlayGeometry? overlay)
    {
        var state = CodingOverlayMeasurementFormatter.BuildPanelState(overlay);
        CodingMeasurementPanelControls.Apply(
            TxtCodingQ1,
            TxtCodingQ2,
            TxtCodingClock,
            TxtCodingArc,
            TxtCodingMeasurement,
            CodingMeasurementPanel,
            state);
    }
}
