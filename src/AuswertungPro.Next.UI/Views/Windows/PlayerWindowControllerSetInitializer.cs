using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerWindowControllerSetInitializer
{
    public static PlayerWindowControllerSet Create(
        PlayerWindow window,
        PlayerWindowControllerSetDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(dependencies);

        return PlayerWindowControllerSetFactory.Create(
            new PlayerWindowControllerSetControls(
                DamageMarkerCanvas: window.DamageMarkerCanvas,
                PositionSlider: window.PositionSlider,
                HeatmapCanvas: window.HeatmapCanvas,
                QuickScanButton: window.QuickScanButton,
                QuickScanStatusText: window.QuickScanStatusText,
                CurrentTimeText: window.CurrentTimeText,
                DurationText: window.DurationText,
                RateText: window.RateText,
                Speed05Button: window.Speed05Button,
                Speed1Button: window.Speed1Button,
                Speed15Button: window.Speed15Button,
                Speed2Button: window.Speed2Button,
                Speed4Button: window.Speed4Button,
                Speed8Button: window.Speed8Button,
                MarkToolPopup: window.MarkToolPopup,
                CodingMarkToolPopup: window.CodingMarkToolPopup,
                ToolsDropdownPopup: window.ToolsDropdownPopup,
                MarkToolName: window.TxtMarkToolName,
                ActiveToolLabel: window.TxtActiveToolLabel,
                DetectionOverlayGrid: window.DetectionOverlayGrid,
                DetectionCanvas: window.DetectionCanvas,
                CodingOverlayPopup: window.CodingOverlayPopup,
                CodingOverlayCanvas: window.CodingOverlayCanvas),
            dependencies);
    }
}
