using System;
using System.Windows;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private PlayerWindowTimerSet CreatePlayerTimers()
        => PlayerWindowTimerSetFactory.Create(
            createRequest: () => new PlayerWindowTimerTickWorkflowRequest(
                _closing,
                _playbackDisposed,
                _positionSliderStateController.IsDragging),
            actions: new PlayerWindowTimerTickWorkflowActions(
                UpdateUi,
                ScrubSeekToSlider));

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
            new PlayerWindowDeactivationRequest(_codingOverlayInputVisibilityState.SuspendDepth),
            CreateWindowActivationActions());

    private void PlayerWindow_Activated(object? sender, EventArgs e)
        => PlayerWindowActivationWorkflow.Activate(
            new PlayerWindowActivationRequest(_codingOverlayInputVisibilityState.DeactivatedByExternalWindow),
            CreateWindowActivationActions());

    private PlayerWindowActivationWorkflowActions CreateWindowActivationActions()
        => new(
            SetDeactivatedByExternalWindow: _codingOverlayInputVisibilityState.SetDeactivatedByExternalWindow,
            HideCodingOverlayForExternalWindow,
            RestoreCodingOverlayAfterExternalWindow);

    private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        => PlayerWindowLoadedWorkflow.Execute(
            new PlayerWindowLoadedWorkflowRequest(
                _initialOverlayText,
                TimeSpan.FromSeconds(6)),
            new PlayerWindowLoadedWorkflowActions(
                Play: () => Play(_videoPath),
                UpdateCodingOverlayViewport,
                ScheduleLoadedViewportUpdate: () => PlayerDispatcherScheduler.ScheduleLoaded(
                    Dispatcher,
                    UpdateCodingOverlayViewport),
                ShowOverlay,
                BuildDamageMarkerTimeline: () => _damageMarkerController.Build(),
                EnableFocusable: () => Focusable = true,
                ScheduleFocusWindow: () => PlayerDispatcherScheduler.ScheduleInput(
                    Dispatcher,
                    () =>
                    {
                        PlayerFocusControls.ActivateWindow(this);
                        PlayerFocusControls.FocusWindowKeyboard(this);
                    })));

    private void PlayerWindow_Closed(object? sender, EventArgs e)
    {
        var main = System.Windows.Application.Current?.MainWindow;
        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: ReferenceEquals(_lastOpened, this),
                HasMainWindow: main is not null,
                IsMainWindowCurrentWindow: ReferenceEquals(main, this),
                IsMainWindowMinimized: main?.WindowState == WindowState.Minimized),
            new PlayerWindowClosedWorkflowActions(
                ClearLastOpened: () => _lastOpened = null,
                ExitCodingMode: () => _codingModeState.Set(false),
                StopCodingOsdTimer: StopCodingOsdTimer,
                DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                DisposeCodingAnalysisCancellation: _codingAiRuntimeOwner.Controller.DisposeAnalysisCancellation,
                StopCodingAiPulse: StopCodingAiPulse,
                CancelQuickScan: _quickScanController.Cancel,
                StopLiveDetection: StopLiveDetection,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                Cleanup: Cleanup,
                RestoreMainWindow: () => main!.WindowState = WindowState.Normal,
                ActivateMainWindow: () => PlayerFocusControls.ActivateWindow(main!)));
    }
}
