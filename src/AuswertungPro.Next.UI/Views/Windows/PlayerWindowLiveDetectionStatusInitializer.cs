using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowLiveDetectionStatusControls(
    UIElement PulseRing,
    FrameworkElement Badge,
    TextBlock BadgeStatusText,
    Shape BadgeDot,
    FrameworkElement YoloStatusBar,
    TextBlock YoloStatusText,
    Shape YoloDot,
    TextBlock YoloModelText,
    TextBlock CodingAiStatusText,
    TextBlock CodingAiStageText,
    Shape CodingAiDot,
    TextBlock DetectionStatusText,
    FrameworkElement FindingSummaryPanel,
    TextBlock FindingSummaryText);

internal sealed record PlayerWindowLiveDetectionStatusControllerSet(
    ILiveDetectionPulseController Pulse,
    ILiveDetectionStatusController Status);

internal static class PlayerWindowLiveDetectionStatusInitializer
{
    internal static PlayerWindowLiveDetectionStatusControllerSet Create(
        PlayerWindowLiveDetectionStatusControls controls,
        LiveDetectionPulseStateController pulseState,
        Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(pulseState);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var pulse = new LiveDetectionPulseController(
            pulseState,
            new LiveDetectionPulseControllerActions(
                StartAnimation: () => LiveDetectionPulseControls.Start(controls.PulseRing),
                StopAnimation: () => LiveDetectionPulseControls.Stop(controls.PulseRing)));
        var status = new LiveDetectionStatusController(
            new LiveDetectionStatusControllerActions(
                HasDispatcherAccess: () => PlayerDispatcherScheduler.HasAccess(dispatcher),
                DispatchToUi: action => PlayerDispatcherScheduler.Invoke(dispatcher, action),
                ShowLiveDetectionBadge: (text, color, stage) =>
                    LiveDetectionStatusControls.ShowLiveDetectionBadge(
                        controls.Badge,
                        controls.BadgeStatusText,
                        controls.BadgeDot,
                        text,
                        color,
                        stage),
                ShowYoloStatus: (text, color, model) => LiveDetectionStatusControls.ShowYoloStatus(
                    controls.YoloStatusBar,
                    controls.YoloStatusText,
                    controls.YoloDot,
                    controls.YoloModelText,
                    text,
                    color,
                    model),
                ShowCodingAiState: (text, color, stage) => LiveDetectionStatusControls.ShowCodingAiState(
                    controls.CodingAiStatusText,
                    controls.CodingAiStageText,
                    controls.CodingAiDot,
                    text,
                    color,
                    stage),
                StartPulse: pulse.Start,
                StopPulse: pulse.Stop,
                ShowDetectionStatus: result => LiveDetectionStatusControls.ShowDetectionStatus(
                    controls.DetectionStatusText,
                    controls.FindingSummaryPanel,
                    controls.FindingSummaryText,
                    result)));

        return new PlayerWindowLiveDetectionStatusControllerSet(pulse, status);
    }
}
