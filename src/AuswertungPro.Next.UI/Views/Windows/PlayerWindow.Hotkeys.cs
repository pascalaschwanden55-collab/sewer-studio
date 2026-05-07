using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Tastatur-Shortcuts extrahiert aus PlayerWindow.xaml.cs.
// Behandelt PreviewKeyDown + die Notausstiege (ESC, Doppel-ESC) sowie die
// Player-Steuertasten (Space, S, P, R, +/-, Pfeile, D, M).
public partial class PlayerWindow
{
    private DateTime _lastEscapePress = DateTime.MinValue;

    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // ESC im Trainings-Modus = Notausstieg. Ohne diese Zeile fing das Popup-
        // Fadenkreuz alle Mausklicks ab und der Toggle-Button war nicht mehr
        // erreichbar (UI-Trap).
        if (e.Key == Key.Escape && _isTrainingMode)
        {
            ExitTrainingMode();
            if (TrainingModeButton != null)
                TrainingModeButton.IsChecked = false;
            e.Handled = true;
            return;
        }

        // Doppel-ESC innerhalb 1.5s: harter Codier-Modus-Notausstieg
        // (R-C3). Tritt zur Wirkung wenn der normale CancelDraw nicht mehr
        // klappt weil Overlay-Service korrupt ist.
        if (e.Key == Key.Escape && _isCodingMode)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEscapePress).TotalSeconds < 1.5)
            {
                try
                {
                    ExitCodingMode();
                    System.Diagnostics.Debug.WriteLine("[PlayerWindow] Doppel-ESC: CodingMode hart beendet.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayerWindow] Doppel-ESC ExitCodingMode: {ex.Message}");
                }
                _lastEscapePress = DateTime.MinValue;
                e.Handled = true;
                return;
            }
            _lastEscapePress = now;
            // weiterreichen an normalen Cancel-Pfad unten
        }

        if (e.Key == Key.Escape && _codingOverlayService != null)
        {
            _codingOverlayService.CancelDraw();
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
            e.Handled = true;
            return;
        }

        // ── Pausenmodus-Tasten: Delete/O nur wenn pausiert + Masken sichtbar ──
        if (BtnCodingPauseMode?.IsChecked == true && _player is { IsPlaying: false } && HasVisibleMasks())
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                // Delete: selektierte Maske verwerfen, oder erste sichtbare
                var idx = _selectedMaskIndex >= 0 ? _selectedMaskIndex : FindFirstVisibleMaskIndex();
                if (idx >= 0) DeleteMaskAtIndex(idx);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                // O: selektierte Maske akzeptieren, oder erste sichtbare
                var idx = _selectedMaskIndex >= 0 ? _selectedMaskIndex : FindFirstVisibleMaskIndex();
                if (idx >= 0) AcceptMaskAtIndex(idx);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                // Tab: zur naechsten Maske wechseln
                var current = _selectedMaskIndex >= 0 ? _selectedMaskIndex : -1;
                var next = FindNextVisibleMaskIndex(current);
                if (next >= 0) OnMaskOverlayClicked(next);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Space)
        {
            TogglePlayPause();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S)
        {
            _videoPlayback.Stop();
            UpdateRateLabel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.P)
        {
            _videoPlayback.Pause();
            _codingAnalysisCts?.Cancel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            _videoPlayback.Resume();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Add || e.Key == Key.OemPlus)
        {
            ChangeSpeed(+0.25f);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
        {
            ChangeSpeed(-0.25f);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right)
        {
            JumpSeconds(5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            JumpSeconds(-5);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.D)
        {
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
            e.Handled = true;
            return;
        }

        if (e.Key == Key.M)
        {
            // Toggle: Wenn Mark-Tool aktiv → deaktivieren, sonst Popup oeffnen
            if (_markToolType != OverlayToolType.None)
                DeactivateMarkTool();
            else
                MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
            e.Handled = true;
        }
    }

    private void TogglePlayPause()
    {
        var willPause = _videoPlayback.TogglePlayPause();

        // Laufende KI-Analyse abbrechen wenn pausiert wird
        if (willPause)
            _codingAnalysisCts?.Cancel();
    }
}
