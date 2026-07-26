using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowLiveDetectionLifecycleControls(
    Canvas DetectionCanvas,
    FrameworkElement DetectionOverlay,
    FrameworkElement StatusBadge,
    FrameworkElement FindingSummaryPanel,
    TextBlock DetectionStatusText,
    ToggleButton LiveDetectionToggle);

internal sealed record PlayerWindowLiveDetectionControllerSetDependencies(
    LiveDetectionController RuntimeController,
    PlayerWindowShutdownStateController ShutdownState,
    Func<int> GetTotalEvents,
    PlayerPlaybackControlHost PlaybackControlHost,
    ILiveDetectionStatusController StatusController,
    PlayerWindowLiveDetectionLifecycleControls Controls,
    EventHandler TimerTick,
    Action RunFirstDetection);

internal sealed record PlayerWindowLiveDetectionControllerSetFactoryActions(
    Func<LiveDetectionStartupActions, Task<bool>> StartWithDisplayAsync,
    Action<LiveDetectionHideStatusTimerDisplayActions> ScheduleHideStatusTimer);

internal sealed record PlayerWindowLiveDetectionControllerSet(
    ILiveDetectionStopController Stop,
    ILiveDetectionLifecycleController Lifecycle);

internal static class PlayerWindowLiveDetectionControllerSetFactory
{
    internal static PlayerWindowLiveDetectionControllerSet Create(
        PlayerWindowLiveDetectionControllerSetDependencies dependencies)
        => Create(
            dependencies,
            new PlayerWindowLiveDetectionControllerSetFactoryActions(
                StartWithDisplayAsync: LiveDetectionStartupDisplayWorkflow.StartAsync,
                ScheduleHideStatusTimer: actions =>
                    _ = LiveDetectionHideStatusTimerWorkflow.Schedule(actions)));

    internal static PlayerWindowLiveDetectionControllerSet Create(
        PlayerWindowLiveDetectionControllerSetDependencies dependencies,
        PlayerWindowLiveDetectionControllerSetFactoryActions factoryActions)
    {
        Validate(dependencies, factoryActions);

        var controls = dependencies.Controls;
        var stop = new LiveDetectionStopController(
            new LiveDetectionStopControllerSources(
                StopRuntime: dependencies.RuntimeController.Stop,
                ShouldUpdateUi: () => !dependencies.ShutdownState.IsUnavailable,
                HideOverlay: () => !dependencies.RuntimeController.IsManualMarkMode,
                GetTotalEvents: dependencies.GetTotalEvents,
                HasPlayer: () => !dependencies.ShutdownState.IsPlaybackDisposed,
                IsPlaybackDisposed: () => dependencies.ShutdownState.IsPlaybackDisposed,
                IsPlayerPlaying: () =>
                    !dependencies.ShutdownState.IsPlaybackDisposed
                    && dependencies.PlaybackControlHost.IsPlaying,
                IsDetecting: () => dependencies.RuntimeController.IsDetecting),
            new LiveDetectionStopControllerActions(
                SetStoppedStatus: () => dependencies.StatusController.SetYoloStatus(
                    "Gestoppt",
                    PlayerStatusColors.Muted),
                ClearOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                    controls.DetectionCanvas,
                    controls.DetectionOverlay,
                    hideOverlay),
                ShowStoppedDetectionStatus: totalEvents =>
                    LiveDetectionStatusControls.ShowStoppedDetectionStatus(
                        controls.StatusBadge,
                        controls.FindingSummaryPanel,
                        controls.DetectionStatusText,
                        totalEvents),
                SetPause: dependencies.PlaybackControlHost.SetPause,
                ScheduleHideStatusTimer: factoryActions.ScheduleHideStatusTimer,
                HideDetectionStatus: () =>
                    LiveDetectionStatusControls.HideDetectionStatus(controls.DetectionStatusText)));

        var lifecycle = new LiveDetectionLifecycleController(
            new LiveDetectionLifecycleControllerActions(
                IsDetecting: () => dependencies.RuntimeController.IsDetecting,
                StopLiveDetection: stop.Stop,
                UncheckToggle: () => LiveDetectionToggleControls.Uncheck(
                    controls.LiveDetectionToggle),
                StartWithDisplayAsync: factoryActions.StartWithDisplayAsync,
                StartRuntime: dependencies.RuntimeController.StartRuntime,
                ShowOverlay: () => LiveDetectionOverlayControls.Show(controls.DetectionOverlay),
                ApplyActiveStatus: status =>
                {
                    dependencies.StatusController.SetLiveDetectionBadge(
                        status.BadgeText,
                        status.StatusColor,
                        status.BadgeDetails);
                    dependencies.StatusController.SetYoloStatus(
                        status.YoloText,
                        status.StatusColor,
                        status.ModelLabel);
                },
                ShowWaitingForFrame: () =>
                    LiveDetectionStatusControls.ShowWaitingForFrame(controls.DetectionStatusText),
                TimerTick: dependencies.TimerTick,
                RunFirstDetection: dependencies.RunFirstDetection));

        return new PlayerWindowLiveDetectionControllerSet(stop, lifecycle);
    }

    private static void Validate(
        PlayerWindowLiveDetectionControllerSetDependencies dependencies,
        PlayerWindowLiveDetectionControllerSetFactoryActions factoryActions)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.RuntimeController);
        ArgumentNullException.ThrowIfNull(dependencies.ShutdownState);
        ArgumentNullException.ThrowIfNull(dependencies.GetTotalEvents);
        ArgumentNullException.ThrowIfNull(dependencies.PlaybackControlHost);
        ArgumentNullException.ThrowIfNull(dependencies.StatusController);
        ArgumentNullException.ThrowIfNull(dependencies.Controls);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.DetectionCanvas);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.DetectionOverlay);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.StatusBadge);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.FindingSummaryPanel);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.DetectionStatusText);
        ArgumentNullException.ThrowIfNull(dependencies.Controls.LiveDetectionToggle);
        ArgumentNullException.ThrowIfNull(dependencies.TimerTick);
        ArgumentNullException.ThrowIfNull(dependencies.RunFirstDetection);
        ArgumentNullException.ThrowIfNull(factoryActions);
        ArgumentNullException.ThrowIfNull(factoryActions.StartWithDisplayAsync);
        ArgumentNullException.ThrowIfNull(factoryActions.ScheduleHideStatusTimer);
    }
}
