using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private DispatcherTimer CreateUpdateTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, __) => { if (_closing || _player is null) return; UpdateUi(); };
        return timer;
    }

    private DispatcherTimer CreateScrubTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        timer.Tick += (_, __) =>
        {
            timer.Stop();
            if (_closing || _player is null) return;
            if (_isDragging)
                ScrubSeekToSlider();
        };
        return timer;
    }

    private void WirePositionSliderEvents()
    {
        PositionSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(PositionSlider_DragStarted), true);
        PositionSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(PositionSlider_DragCompleted), true);
        PositionSlider.PreviewMouseLeftButtonUp += PositionSlider_PreviewMouseLeftButtonUp;
        PositionSlider.LostMouseCapture += PositionSlider_LostMouseCapture;
    }

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

    private void PositionSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _wasPlayingBeforeDrag = _player.IsPlaying;
        _isDragging = true;
        if (_wasPlayingBeforeDrag)
            _player.SetPause(true);
        ScrubSeekToSlider();
    }

    private void PositionSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _scrubTimer.Stop();
        SeekToSlider();
        _isDragging = false;
        if (_wasPlayingBeforeDrag)
            _player.SetPause(false);
    }

    private void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            SeekToSlider();
    }

    private void PositionSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        _scrubTimer.Stop();
        SeekToSlider();
        _isDragging = false;
        if (_wasPlayingBeforeDrag)
            _player.SetPause(false);
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
        if (ReferenceEquals(_lastOpened, this))
            _lastOpened = null;

        // Codier-Modus sauber beenden: Timer + Hintergrund-Tasks stoppen.
        // Cleanup() ist idempotent, weil OnClosing den VLC-Player bereits freigeben kann.
        _isCodingMode = false;
        StopCodingOsdTimer();
        DisposeCodingOsdMeterService();
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts?.Dispose();
        _codingAnalysisCts = null;
        _codingLiveDetection = null;
        StopCodingAiPulse();

        _quickScanController.Cancel();
        StopLiveDetection();
        StopPipelineHealthMonitor();
        Cleanup();

        var main = System.Windows.Application.Current?.MainWindow;
        if (main != null && !ReferenceEquals(main, this))
        {
            if (main.WindowState == WindowState.Minimized)
                main.WindowState = WindowState.Normal;
            main.Activate();
        }
    }
}
