using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private DispatcherTimer CreateUpdateTimer()
        => PlayerWindowTimerFactory.CreateUpdateTimer(() =>
        {
            if (_closing || _player is null) return;
            UpdateUi();
        });

    private DispatcherTimer CreateScrubTimer()
        => PlayerWindowTimerFactory.CreateScrubTimer(() =>
        {
            if (_closing || _player is null) return;
            if (_isDragging)
                ScrubSeekToSlider();
        });

    private void WireWindowLifecycleEvents()
    {
        Loaded += PlayerWindow_EnsureVisibleOnLoaded;
        Deactivated += PlayerWindow_Deactivated;
        Activated += PlayerWindow_Activated;
        Closing += OnClosing;
        Loaded += PlayerWindow_Loaded;
        Closed += PlayerWindow_Closed;
    }

    private void WireWindowSurfaceEvents()
    {
        DamageMarkerCanvas.SizeChanged += (_, __) => _damageMarkerController.Reposition();
        HeatmapCanvas.SizeChanged += (_, __) => _quickScanController.Reposition();
        DetectionCanvas.MouseLeftButtonDown += DetectionCanvas_MouseLeftButtonDown;
        VideoView.SizeChanged += (_, __) => UpdateCodingOverlayViewport();
        SizeChanged += (_, __) => UpdateCodingOverlayViewport();
        LocationChanged += (_, __) => UpdateCodingOverlayViewport();
    }

    private void WireKeyboardEvents()
    {
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(PlayerWindow_PreviewKeyDown), true);
    }

    private void PlayerWindow_EnsureVisibleOnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureVisibleOnScreen();
    }

    private void PlayerWindow_Deactivated(object? sender, EventArgs e)
    {
        // Overlay-Popup schliessen, wenn ein fremdes Fenster den Fokus bekommt.
        // Eigene Child-Dialoge verwenden SuspendCodingOverlayInput/ResumeCodingOverlayInput direkt.
        if (_codingOverlaySuspendDepth > 0)
            return;

        _deactivatedByExternalWindow = true;
        HideCodingOverlayForExternalWindow();
    }

    private void PlayerWindow_Activated(object? sender, EventArgs e)
    {
        if (!_deactivatedByExternalWindow)
            return;

        _deactivatedByExternalWindow = false;
        RestoreCodingOverlayAfterExternalWindow();
    }

    private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Play(_videoPath);
        UpdateCodingOverlayViewport();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        if (!string.IsNullOrWhiteSpace(_initialOverlayText))
            ShowOverlay(_initialOverlayText!, TimeSpan.FromSeconds(6));

        _damageMarkerController.Build();

        Focusable = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            Activate();
            Focus();
            Keyboard.Focus(this);
        }));
    }

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
                ExitCodingMode: () => _isCodingMode = false,
                StopCodingOsdTimer: StopCodingOsdTimer,
                DisposeCodingOsdMeterService: DisposeCodingOsdMeterService,
                DisposeCodingAnalysisCancellation: _codingAiController.DisposeAnalysisCancellation,
                StopCodingAiPulse: StopCodingAiPulse,
                CancelQuickScan: _quickScanController.Cancel,
                StopLiveDetection: StopLiveDetection,
                StopPipelineHealthMonitor: StopPipelineHealthMonitor,
                Cleanup: Cleanup,
                RestoreMainWindow: () => main!.WindowState = WindowState.Normal,
                ActivateMainWindow: () => main!.Activate()));
    }
}
