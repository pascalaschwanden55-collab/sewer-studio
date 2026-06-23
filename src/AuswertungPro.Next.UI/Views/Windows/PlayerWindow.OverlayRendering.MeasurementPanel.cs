using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void UpdateCodingOverlayInfo(OverlayGeometry? overlay)
    {
        var state = CodingOverlayMeasurementFormatter.BuildPanelState(overlay);
        TxtCodingQ1.Text = state.Q1Text;
        TxtCodingQ2.Text = state.Q2Text;
        TxtCodingClock.Text = state.ClockText;
        TxtCodingArc.Text = state.ArcText;
        TxtCodingMeasurement.Text = state.MeasurementText;
        CodingMeasurementPanel.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
