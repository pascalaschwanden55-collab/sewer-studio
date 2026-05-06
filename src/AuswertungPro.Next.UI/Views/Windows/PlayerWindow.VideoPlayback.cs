using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Player;
using AuswertungPro.Next.Infrastructure.Player;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.E: PlayerWindow delegiert Playback/Lifecycle an VideoPlaybackController.
// UI-Elemente, Slider-Text und Overlay-Koordination bleiben bewusst im Window.
public partial class PlayerWindow
{
    object? IVlcSurface.MediaPlayer
    {
        get => VideoView.MediaPlayer;
        set => VideoView.MediaPlayer = (MediaPlayer?)value;
    }

    private IVideoPlaybackController CreatePlaybackController(string videoPath, PlayerWindowOptions options)
    {
        return new VideoPlaybackController(
            videoPath,
            options,
            this,
            new WpfPlaybackDispatcher(Dispatcher),
            new WpfPlaybackTimer(Dispatcher, TimeSpan.FromMilliseconds(250)),
            new WpfPlaybackTimer(Dispatcher, TimeSpan.FromMilliseconds(60)));
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        _videoPlayback.Resume();
        UpdateRateLabel();
        ClearDetectionOverlays();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _videoPlayback.Pause();
        UpdateRateLabel();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _videoPlayback.Stop();
        UpdateRateLabel();
    }

    private void Speed05_Click(object sender, RoutedEventArgs e) => SetSpeed(0.5f);

    private void Speed1_Click(object sender, RoutedEventArgs e) => SetSpeed(1.0f);

    private void Speed15_Click(object sender, RoutedEventArgs e) => SetSpeed(1.5f);

    private void Speed2_Click(object sender, RoutedEventArgs e) => SetSpeed(2.0f);

    private void Speed4_Click(object sender, RoutedEventArgs e) => SetSpeed(4.0f);

    private void Speed8_Click(object sender, RoutedEventArgs e) => SetSpeed(8.0f);

    private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_videoPlayback.IsDragging)
            UpdateSeekPreview();
    }

    private void SeekToSlider()
    {
        ApplySeekSnapshot(_videoPlayback.SeekToSlider(PositionSlider.Value, PositionSlider.Maximum));
        UpdateUi();
    }

    private void UpdateSeekPreview()
    {
        ApplySeekSnapshot(_videoPlayback.PreviewSeek(PositionSlider.Value, PositionSlider.Maximum));
    }

    private void ScrubSeekToSlider()
    {
        ApplySeekSnapshot(_videoPlayback.ScrubSeekToSlider(PositionSlider.Value, PositionSlider.Maximum));
    }

    private void ApplySeekSnapshot(PlaybackSeekSnapshot snapshot)
    {
        if (snapshot.CurrentTimeMs.HasValue && snapshot.DurationMs.HasValue)
        {
            CurrentTimeText.Text = FormatMs(snapshot.CurrentTimeMs.Value);
            DurationText.Text = FormatMs(snapshot.DurationMs.Value);
            return;
        }

        CurrentTimeText.Text = $"{snapshot.Position:P0}";
        DurationText.Text = "--:--";
    }

    private void SetSpeed(float rate)
    {
        var change = _videoPlayback.SetSpeed(rate);
        if (!change.IsSupported)
        {
            var clamped = Math.Clamp(rate, MinRate, MaxRate);
            MessageBox.Show($"SetRate({clamped:0.##}) nicht unterstuetzt fuer dieses Video.",
                "Video", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        UpdateRateLabel();
    }

    private void UpdateRateLabel()
    {
        var rate = _videoPlayback.CurrentRate;
        RateText.Text = $"{rate:0.##}x";
        UpdateSpeedButtons(rate);
    }

    private void UpdateSpeedButtons(float rate)
    {
        SetSpeedButtonState(Speed05Button, rate, 0.5f);
        SetSpeedButtonState(Speed1Button, rate, 1.0f);
        SetSpeedButtonState(Speed15Button, rate, 1.5f);
        SetSpeedButtonState(Speed2Button, rate, 2.0f);
        SetSpeedButtonState(Speed4Button, rate, 4.0f);
        SetSpeedButtonState(Speed8Button, rate, 8.0f);
    }

    private static void SetSpeedButtonState(ToggleButton button, float currentRate, float targetRate)
    {
        button.IsChecked = Math.Abs(currentRate - targetRate) < 0.01f;
    }

    private void EnsurePlaying()
    {
        _videoPlayback.EnsurePlaying();
    }

    private void ChangeSpeed(float delta)
    {
        HandleRateChange(_videoPlayback.ChangeSpeed(delta));
    }

    private void HandleRateChange(PlaybackRateChange change)
    {
        if (!change.IsSupported)
        {
            var clamped = Math.Clamp(change.RequestedRate, MinRate, MaxRate);
            MessageBox.Show($"SetRate({clamped:0.##}) nicht unterstuetzt fuer dieses Video.",
                "Video", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        UpdateRateLabel();
    }

    private void JumpSeconds(int seconds)
    {
        if (!_videoPlayback.JumpSeconds(seconds))
            return;

        ClearDetectionOverlays();
        UpdateUi();
    }

    private void Play(string path)
    {
        _videoPlayback.Play(path);
        UpdateRateLabel();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _isWindowClosed = true;
        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] OnClosing error: {ex.Message}");
        }
    }

    private void Cleanup()
    {
        _videoPlayback.Cleanup();
    }

    private void UpdateUi()
    {
        if (_videoPlayback.IsDragging)
            return;

        var snapshot = _videoPlayback.GetPositionSnapshot();
        var length = snapshot.LengthMs;
        var time = Math.Max(0, snapshot.TimeMs);

        if (length > 0)
        {
            var pos = (double)time / length;
            PositionSlider.Value = pos * PositionSlider.Maximum;
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = FormatMs(length);
        }
        else
        {
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = "--:--";
        }

        UpdateRateLabel();

        if (_isCodingMode)
            UpdateCodingCurrentCode();
    }

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
    }

    private sealed class WpfPlaybackTimer : IPlaybackTimer
    {
        private readonly DispatcherTimer _timer;

        public WpfPlaybackTimer(Dispatcher dispatcher, TimeSpan interval)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = interval
            };
        }

        public event EventHandler? Tick
        {
            add => _timer.Tick += value;
            remove => _timer.Tick -= value;
        }

        public bool IsEnabled => _timer.IsEnabled;

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();
    }

    private sealed class WpfPlaybackDispatcher : IPlaybackDispatcher
    {
        private readonly Dispatcher _dispatcher;

        public WpfPlaybackDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void BeginInvoke(Action action, PlaybackDispatcherPriority priority)
        {
            _dispatcher.BeginInvoke(action, MapPriority(priority));
        }

        private static DispatcherPriority MapPriority(PlaybackDispatcherPriority priority) => priority switch
        {
            PlaybackDispatcherPriority.ApplicationIdle => DispatcherPriority.ApplicationIdle,
            _ => DispatcherPriority.Normal
        };
    }
}
