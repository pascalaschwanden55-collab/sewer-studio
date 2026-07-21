using System;
using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void WireWindowLifecycleEvents()
    {
        PlayerLifecycleEventBinder.Bind(
            this,
            PlayerWindow_EnsureVisibleOnLoaded,
            PlayerWindow_Deactivated,
            PlayerWindow_Activated,
            OnClosing,
            PlayerWindow_Loaded,
            PlayerWindow_Closed);
    }

    private void WireWindowSurfaceEvents()
    {
        PlayerSurfaceEventBinder.Bind(
            DamageMarkerCanvas,
            HeatmapCanvas,
            DetectionCanvas,
            VideoView,
            this,
            (_, __) => _damageMarkerController.Reposition(),
            (_, __) => _quickScanController.Reposition(),
            DetectionCanvas_MouseLeftButtonDown,
            (_, __) => UpdateCodingOverlayViewport(),
            (_, __) => UpdateCodingOverlayViewport(),
            (_, __) => UpdateCodingOverlayViewport());
    }

    private void WireKeyboardEvents()
    {
        PlayerKeyboardEventBinder.Bind(this, PlayerWindow_PreviewKeyDown);
    }

    private void PlayerWindow_EnsureVisibleOnLoaded(object sender, RoutedEventArgs e)
    {
        PlayerBoundsControls.EnsureVisibleOnScreen(this);
    }

    private void PlayerWindow_Deactivated(object? sender, EventArgs e)
        => PlayerWindowActivationWorkflow.Deactivate(
            new PlayerWindowDeactivationRequest(_codingOverlayInputVisibilityController.SuspendDepth),
            CreateWindowActivationActions());

    private void PlayerWindow_Activated(object? sender, EventArgs e)
        => PlayerWindowActivationWorkflow.Activate(
            new PlayerWindowActivationRequest(_codingOverlayInputVisibilityController.DeactivatedByExternalWindow),
            CreateWindowActivationActions());

    private PlayerWindowActivationWorkflowActions CreateWindowActivationActions()
        => new(
            SetDeactivatedByExternalWindow: _codingOverlayInputVisibilityController.SetDeactivatedByExternalWindow,
            HideCodingOverlayForExternalWindow: _codingOverlayInputVisibilityController.HideForExternalWindow,
            RestoreCodingOverlayAfterExternalWindow: _codingOverlayInputVisibilityController.RestoreAfterExternalWindow);

    private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        => PlayerWindowLoadedWorkflow.Execute(
            new PlayerWindowLoadedWorkflowRequest(
                _playbackContext.InitialOverlayText,
                TimeSpan.FromSeconds(6)),
            new PlayerWindowLoadedWorkflowActions(
                Play: () => Play(_playbackContext.VideoPath),
                UpdateCodingOverlayViewport,
                ScheduleLoadedViewportUpdate: () => PlayerDispatcherScheduler.ScheduleLoaded(
                    Dispatcher,
                    UpdateCodingOverlayViewport),
                ShowOverlay,
                BuildDamageMarkerTimeline: () => _damageMarkerController.Build(),
                EnableFocusable: () => PlayerChromeControls.EnableFocusable(this),
                ScheduleFocusWindow: () => PlayerDispatcherScheduler.ScheduleInput(
                    Dispatcher,
                    () =>
                    {
                        PlayerFocusControls.ActivateWindow(this);
                        PlayerFocusControls.FocusWindowKeyboard(this);
                    })));

    private void PlayerWindow_Closed(object? sender, EventArgs e)
    {
        var main = PlayerApplicationControls.CurrentMainWindow();
        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: LastOpenedWindow.IsCurrent(this),
                HasMainWindow: main is not null,
                IsMainWindowCurrentWindow: ReferenceEquals(main, this),
                IsMainWindowMinimized: PlayerChromeControls.IsMinimized(main)),
            new PlayerWindowClosedWorkflowActions(
                ClearLastOpened: LastOpenedWindow.Clear,
                ExitCodingMode: () => _codingModeState.Set(false),
                StopCodingOsdTimer: StopCodingOsdTimer,
                DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                DisposeCodingAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation,
                StopCodingAiPulse: _liveDetectionPulseController.Stop,
                CancelQuickScan: _quickScanController.Cancel,
                StopLiveDetection: _liveDetectionStopController.Stop,
                StopPipelineHealthMonitor: _codingPipelineHealthController.Stop,
                Cleanup: Cleanup,
                RestoreMainWindow: () => PlayerChromeControls.RestoreNormal(main!),
                ActivateMainWindow: () => PlayerFocusControls.ActivateWindow(main!)));
    }
}
