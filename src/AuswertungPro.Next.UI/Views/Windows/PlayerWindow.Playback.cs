using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    public static bool TryShowOverlayOnLast(string text, TimeSpan duration)
    {
        if (_lastOpened is null)
            return false;
        _lastOpened.ShowOverlay(text, duration);
        return true;
    }

    public static bool TryGetCurrentTime(out TimeSpan time)
    {
        time = default;
        if (_lastOpened is null)
            return false;

        return _lastOpened.TryGetCurrentTimeInternal(out time);
    }

    public static bool TrySeekTo(TimeSpan time)
    {
        if (_lastOpened is null)
            return false;

        return _lastOpened.TrySeekToInternal(time);
    }

    /// <summary>
    /// Erstellt einen Snapshot vom aktuellen Video-Frame als PNG.
    /// Funktioniert mit jeder Aufloesung (auch FullHD 1920x1080).
    /// </summary>
    public static bool TryTakeSnapshot(out string snapshotPath)
    {
        snapshotPath = string.Empty;
        var playerWindow = _lastOpened;
        if (playerWindow is null || playerWindow._closing || playerWindow._playbackDisposed)
            return false;
        if (playerWindow._player is null || !playerWindow._player.IsPlaying && playerWindow._player.Time <= 0)
            return false;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SewerStudio_Snapshots");
            Directory.CreateDirectory(tempDir);
            snapshotPath = Path.Combine(tempDir, $"snap_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            // VLC Snapshot: 0 = original Aufloesung (FullHD etc.)
            return playerWindow.TakeSnapshotSafe(snapshotPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// TakeSnapshot mit kurzem Pause-Trick, um D3D11-Deadlock zu vermeiden.
    /// D3D11 haelt die Video-Surface exklusiv gesperrt; kurzes Pausieren gibt sie frei.
    /// </summary>
    private bool TakeSnapshotSafe(string filePath, uint width = 0, uint height = 0)
    {
        if (_closing || _playbackDisposed)
            return false;

        var wasPlaying = false;
        try
        {
            wasPlaying = _player.IsPlaying;
            if (wasPlaying)
            {
                _player.SetPause(true);
                System.Threading.Thread.Sleep(60);
            }
            if (_closing || _playbackDisposed)
                return false;

            // VLC-OSD-Anzeige (Dateipfad) vorher deaktivieren, damit der Pfad
            // nicht als Text auf dem Videobild erscheint
            AuswertungPro.Next.Application.Common.BestEffort.Try(() => _player.SetMarqueeInt(VideoMarqueeOption.Enable, 0), "VLC: Marquee deaktivieren");
            return _player.TakeSnapshot(0, filePath, width, height);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (wasPlaying && !_closing && !_playbackDisposed)
            {
                AuswertungPro.Next.Application.Common.BestEffort.Try(() => _player.SetPause(false), "VLC: Pause aufheben");
            }
        }
    }

    private void ShowOverlay(string text, TimeSpan duration)
    {
        if (_player is null)
            return;

        try
        {
            _player.SetMarqueeInt(VideoMarqueeOption.Enable, 1);
            _player.SetMarqueeInt(VideoMarqueeOption.X, 16);
            _player.SetMarqueeInt(VideoMarqueeOption.Y, 16);
            _player.SetMarqueeInt(VideoMarqueeOption.Size, 24);
            _player.SetMarqueeInt(VideoMarqueeOption.Color, 0xFFFFFF);
            _player.SetMarqueeInt(VideoMarqueeOption.Opacity, 200);
            _player.SetMarqueeString(VideoMarqueeOption.Text, text);

            var t = new DispatcherTimer { Interval = duration };
            t.Tick += (_, __) =>
            {
                t.Stop();
                AuswertungPro.Next.Application.Common.BestEffort.Try(() => _player.SetMarqueeInt(VideoMarqueeOption.Enable, 0), "VLC: Marquee deaktivieren");
            };
            t.Start();
        }
        catch
        {
            // ignore overlay errors
        }
    }

    private bool TryGetCurrentTimeInternal(out TimeSpan time)
    {
        time = default;
        try
        {
            var ms = Math.Max(0, _player.Time);
            time = TimeSpan.FromMilliseconds(ms);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySeekToInternal(TimeSpan time)
    {
        try
        {
            EnsurePlaying();
            _player.Time = PlayerPlaybackState.ResolveSeekTargetMs(time, _player.Length);
            UpdateUi();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var action = PlayerKeyboardShortcutPolicy.Resolve(e.Key, _codingOverlayService != null);
        if (action == null)
            return;

        switch (action.Value)
        {
            case PlayerKeyboardAction.CancelCodingOverlay:
                _codingOverlayService?.CancelDraw();
                _codingSchemaManager.Cancel();
                if (CodingOverlayCanvas.IsMouseCaptured)
                    CodingOverlayCanvas.ReleaseMouseCapture();
                if (_codingVm != null)
                {
                    _codingVm.CurrentOverlay = null;
                    BtnCodingCreateEvent.IsEnabled = false;
                    UpdateCodingOverlayInfo(null);
                }
                if (CodingOverlayPopup.IsOpen)
                    RedrawCodingCanvas(includeManualOverlay: false);
                break;

            case PlayerKeyboardAction.TogglePlayPause:
                TogglePlayPause();
                break;

            case PlayerKeyboardAction.Stop:
                _player.Stop();
                break;

            case PlayerKeyboardAction.Pause:
                _player.SetPause(true);
                break;

            case PlayerKeyboardAction.Resume:
                EnsurePlaying();
                _player.SetPause(false);
                break;

            case PlayerKeyboardAction.SpeedUp:
                ChangeSpeed(+0.25f);
                break;

            case PlayerKeyboardAction.SpeedDown:
                ChangeSpeed(-0.25f);
                break;

            case PlayerKeyboardAction.JumpForward:
                JumpSeconds(5);
                break;

            case PlayerKeyboardAction.JumpBackward:
                JumpSeconds(-5);
                break;

            case PlayerKeyboardAction.ToggleDetection:
                if (_isCodingMode)
                {
                    BtnCodingLiveAi.IsChecked = !(BtnCodingLiveAi.IsChecked == true);
                    CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
                }
                else
                {
                    LiveDetectionButton.IsChecked = !(LiveDetectionButton.IsChecked == true);
                    LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs());
                }
                break;

            case PlayerKeyboardAction.ToggleMarkTool:
                if (_markToolType != OverlayToolType.None)
                    DeactivateMarkTool();
                else
                    MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
                break;
        }

        e.Handled = true;
    }

    private void TogglePlayPause()
    {
        EnsurePlaying();
        _player.SetPause(_player.IsPlaying);
    }

    private void EnsurePlaying()
    {
        var state = _player.State;
        if (state == VLCState.Stopped || state == VLCState.Ended)
            Play(_videoPath);
    }

    private void ChangeSpeed(float delta)
    {
        SetSpeed(AuswertungPro.Next.UI.Player.PlayerPlaybackState.ApplyRateDelta(_player.Rate, delta));
    }

    private void JumpSeconds(int seconds)
    {
        if (_player.Length <= 0)
            return;

        _player.Time = AuswertungPro.Next.UI.Player.PlayerPlaybackState.AddSeconds(_player.Time, _player.Length, seconds);
        ClearDetectionOverlays(); // Alte Overlays bei Navigation entfernen
        UpdateUi();
    }

    private void Play(string path)
    {
        using var media = new Media(_libVlc, path, FromType.FromPath);
        _player.Play(media);
        _timer.Start();
        UpdateRateLabel();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closing)
            return;

        if (!ConfirmUnappliedCodingChangesOnClose())
        {
            e.Cancel = true;
            return;
        }

        // 1. Guard setzen: alle laufenden Tick-Handler prufen _closing und kehren sofort zurueck.
        _closing = true;
        if (ReferenceEquals(_lastOpened, this))
            _lastOpened = null;

        // 2. Alle DispatcherTimer stoppen bevor der MediaPlayer freigegeben wird.
        //    So koennen keine in-flight Ticks mehr _player.IsPlaying aufrufen.
        StopPlayerTimers();
        _quickScanController.Cancel();
        _detectionCts?.Cancel();
        _codingAnalysisCts?.Cancel();
        StopLiveDetection();
        StopPipelineHealthMonitor();

        // 3. Player vom VideoView trennen (verhindert D3D-Zugriff nach Dispose).
        AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (VideoView != null) VideoView.MediaPlayer = null; }, "VLC: VideoView trennen");

        // 4. Player sauber stoppen bevor Dispose (Cleanup macht dann nur noch Dispose).
        AuswertungPro.Next.Application.Common.BestEffort.Try(() => _player.Stop(), "VLC: Player stoppen");

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
        if (_playbackDisposed)
            return;

        _playbackDisposed = true;
        StopPlayerTimers();
        AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (VideoView != null) VideoView.MediaPlayer = null; }, "VLC: VideoView trennen");
        try { _player.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PlayerWindow] MediaPlayer Dispose error: {ex.Message}"); }
        try { _libVlc.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PlayerWindow] LibVLC Dispose error: {ex.Message}"); }
    }

    private void StopPlayerTimers()
    {
        try { _timer.Stop(); } catch { }
        try { _scrubTimer.Stop(); } catch { }
        try { _detectionTimer?.Stop(); } catch { }
        try { _codingLiveAiTimer?.Stop(); } catch { }
        try { _codingLiveAiBlinkTimer?.Stop(); } catch { }
        try { _codingOsdTimer?.Stop(); } catch { }
    }

    private void UpdateUi()
    {
        if (_isDragging)
            return;

        var length = _player.Length;
        var time = Math.Max(0, _player.Time);

        if (length > 0)
        {
            var pos = (double)time / length;
            PositionSlider.Value = pos * PositionSlider.Maximum;
            CurrentTimeText.Text = PlayerPlaybackState.FormatMilliseconds(time);
            DurationText.Text = PlayerPlaybackState.FormatMilliseconds(length);
        }
        else
        {
            CurrentTimeText.Text = PlayerPlaybackState.FormatMilliseconds(time);
            DurationText.Text = "--:--";
        }

        UpdateRateLabel();

        // Im Codier-Modus: Echtzeit-Code am Zeitstempel aktualisieren
        if (_isCodingMode)
            UpdateCodingCurrentCode();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        EnsurePlaying();
        _player.SetPause(false);
        UpdateRateLabel();
        // Overlays aufraeumen; beim Abspielen sind alte Markierungen irrelevant.
        ClearDetectionOverlays();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _player.SetPause(true);
        UpdateRateLabel();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _player.Stop();
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
        if (_isDragging)
            UpdateSeekPreview();
    }

    private void SeekToSlider()
    {
        if (!PlayerPlaybackState.TryResolveSliderRatio(PositionSlider.Value, PositionSlider.Maximum, out var targetPos))
            return;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        UpdateUi();
    }

    private void UpdateSeekPreview()
    {
        if (!PlayerPlaybackState.TryResolveSliderRatio(PositionSlider.Value, PositionSlider.Maximum, out var targetPos))
            return;

        var length = _player.Length;
        var preview = PlayerPlaybackState.BuildSeekPreviewText(targetPos, length);
        CurrentTimeText.Text = preview.CurrentTimeText;
        DurationText.Text = preview.DurationText;

        // Throttled live seek: schedule scrub if not already pending
        if (_isDragging && !_scrubTimer.IsEnabled)
            _scrubTimer.Start();
    }

    private void ScrubSeekToSlider()
    {
        if (!PlayerPlaybackState.TryResolveSliderRatio(PositionSlider.Value, PositionSlider.Maximum, out var targetPos))
            return;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        CurrentTimeText.Text = PlayerPlaybackState
            .BuildSeekPreviewText(targetPos, length)
            .CurrentTimeText;
    }

    private void SetSpeed(float rate)
    {
        var clamped = AuswertungPro.Next.UI.Player.PlayerPlaybackState.ClampRate(rate);
        var result = _player.SetRate(clamped);
        if (result != 0)
        {
            DialogHost.Current.Info($"SetRate({clamped:0.##}) nicht unterstützt für dieses Video.", "Video");
        }

        UpdateRateLabel();
    }

    private void UpdateRateLabel()
    {
        var rate = PlayerPlaybackState.NormalizeRate(_player.Rate);
        RateText.Text = PlayerPlaybackState.FormatRateLabel(rate);
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

    // Damage marker overlay

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

    // Quick-Scan
}
