using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.D: VLC-Playback-Steuerung (Cluster B1, Sub-A) aus PlayerWindow.xaml.cs.
// Sub-A: Play/Pause/Stop/Speed-Click-Handler + Slider-Seek + Rate-Label.
//
// Felder/Members (im Hauptpartial): _player, _isDragging, _scrubTimer,
//   MinRate/MaxRate, PositionSlider, CurrentTimeText, DurationText,
//   RateText, Speed*Button, EnsurePlaying(), UpdateUi(), ClearDetectionOverlays(),
//   FormatMs() (in Helpers).
//
// VLC-Lifecycle (Cleanup, OnClosing, BuildPlayer) bleibt in 6.1.D Sub-B/C.
public partial class PlayerWindow
{
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        EnsurePlaying();
        _player.SetPause(false);
        UpdateRateLabel();
        // Overlays aufraumen — beim Abspielen sind alte Markierungen irrelevant
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
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = PositionSlider.Value / max;
        if (targetPos < 0)
            targetPos = 0;
        if (targetPos > 1)
            targetPos = 1;

        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        UpdateUi();
    }

    private void UpdateSeekPreview()
    {
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = PositionSlider.Value / max;
        if (targetPos < 0)
            targetPos = 0;
        if (targetPos > 1)
            targetPos = 1;

        var length = _player.Length;
        if (length > 0)
        {
            var targetMs = (long)(targetPos * length);
            CurrentTimeText.Text = FormatMs(targetMs);
            DurationText.Text = FormatMs(length);
        }
        else
        {
            CurrentTimeText.Text = $"{targetPos:P0}";
            DurationText.Text = "--:--";
        }

        // Throttled live seek: schedule scrub if not already pending
        if (_isDragging && !_scrubTimer.IsEnabled)
            _scrubTimer.Start();
    }

    private void ScrubSeekToSlider()
    {
        var max = PositionSlider.Maximum;
        if (max <= 0)
            return;

        var targetPos = Math.Clamp(PositionSlider.Value / max, 0.0, 1.0);
        var length = _player.Length;
        if (length > 0)
            _player.Time = (long)(targetPos * length);
        else
            _player.Position = (float)targetPos;

        CurrentTimeText.Text = length > 0 ? FormatMs((long)(targetPos * length)) : $"{targetPos:P0}";
    }

    private void SetSpeed(float rate)
    {
        var clamped = Math.Clamp(rate, MinRate, MaxRate);
        var result = _player.SetRate(clamped);
        if (result != 0)
        {
            MessageBox.Show($"SetRate({clamped:0.##}) nicht unterstuetzt fuer dieses Video.",
                "Video", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        UpdateRateLabel();
    }

    private void UpdateRateLabel()
    {
        var rate = _player.Rate <= 0f ? 1.0f : _player.Rate;
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

    // ─── Sub-B: Playback-Lifecycle (EnsurePlaying, Play, JumpSeconds, ChangeSpeed) ──

    private void EnsurePlaying()
    {
        var state = _player.State;
        if (state == VLCState.Stopped || state == VLCState.Ended)
            Play(_videoPath);
    }

    private void ChangeSpeed(float delta)
    {
        var current = _player.Rate <= 0f ? 1.0f : _player.Rate;
        SetSpeed(current + delta);
    }

    private void JumpSeconds(int seconds)
    {
        if (_player.Length <= 0)
            return;

        long newTime = _player.Time + seconds * 1000L;
        if (newTime < 0)
            newTime = 0;
        if (newTime > _player.Length)
            newTime = _player.Length;
        _player.Time = newTime;
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

    // ─── Sub-C: Window/VLC-Cleanup ────────────────────────────────────────────

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // SOFORT: Lifecycle-Flag setzen damit Background-Tasks ab jetzt
        // sofort returnen und nicht auf gleich disposed Felder zugreifen.
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
        // Defensives VLC-Cleanup: Race mit HwndHost vermeiden indem wir zuerst
        // stoppen + detachen, dann den nativen Disposal asynchron nachziehen.
        // Direktes Dispose auf dem UI-Thread kann AccessViolation ausloesen
        // (sichtbar als „App schliesst sich nach ein paar Sekunden", ohne Log).
        try { _timer.Stop(); } catch { }
        try { _scrubTimer.Stop(); } catch { }

        try
        {
            if (_player != null)
            {
                if (_player.IsPlaying)
                    _player.Stop();
                VideoView.MediaPlayer = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] MediaPlayer-Detach Fehler: {ex.Message}");
        }

        // Native Disposal auf den naechsten Dispatcher-Cycle verlagern — gibt
        // HwndHost Zeit seine Referenzen freizugeben, bevor LibVLC native-side
        // Speicher freigibt.
        var playerRef = _player;
        var libVlcRef = _libVlc;
        // KRITISCH 2026-04-26: _libVlc.Dispose() loest einen native AccessViolation
        // (0xc0000005) in LibVLCLogUnset aus, der die App killt — Windows-Event-Log
        // hat genau das nachgewiesen:
        //   "Description: The process was terminated due to an unhandled exception.
        //    at LibVLCSharp.Shared.LibVLC+Native.LibVLCLogUnset(IntPtr)
        //    at LibVLCSharp.Shared.LibVLC.Dispose(Boolean)"
        // C# try/catch faengt KEINE native AVs ohne legacy-Konfiguration.
        // Loesung: _libVlc NICHT mehr disposen. LibVLC ist eine Singleton-artige
        // Native-Lib (cVLC-Engine), die ohnehin nur einmal pro Prozess existiert
        // und beim Prozess-Exit vom OS aufgeraeumt wird. Der "Leak" ist 0 Byte
        // praktisch — derselbe Native-Speicher den der naechste PlayerWindow
        // sowieso wieder nutzen wuerde.
        // playerRef.Dispose() bleibt — der MediaPlayer hat keinen native-Crash-Pfad.
        // Felder sind readonly — GC raeumt die C#-Wrapper bei Window-Dispose,
        // wir muessen sie hier nicht null setzen.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { playerRef?.Dispose(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerWindow] player.Dispose Fehler: {ex.Message}");
            }
            // libVlcRef BEWUSST nicht disposed — siehe Kommentar oben.
            // GC.KeepAlive verhindert nur dass der Wrapper waehrend playerRef.Dispose
            // schon collected wird (damit das letzte Native-Handle gueltig bleibt).
            GC.KeepAlive(libVlcRef);
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ─── Sub-D: UI-Update + Visibility ─────────────────────────────────────────

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
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = FormatMs(length);
        }
        else
        {
            CurrentTimeText.Text = FormatMs(time);
            DurationText.Text = "--:--";
        }

        UpdateRateLabel();

        // Im Codier-Modus: Echtzeit-Code am Zeitstempel aktualisieren
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
}
