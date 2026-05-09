using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Tastatur-Shortcuts extrahiert aus PlayerWindow.xaml.cs.
// Behandelt PreviewKeyDown + die Notausstiege (ESC) sowie die
// Player-Steuertasten (Space, S, P, R, +/-, Pfeile, D, M).
//
// Slice 8a.3 Step 5b: Coding-Mode-spezifische Hotkey-Bloecke sind raus
// (BtnCodingPauseMode, ExitCodingMode, RedrawCodingCanvas, Mask-Triage,
// _codingAnalysisCts.Cancel auf 'P'). Live-Hotkeys (Play/Pause, Speed,
// MarkTool-Toggle) bleiben unveraendert.
public partial class PlayerWindow
{
    private void PlayerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Slice 1 (Operateur-Annotation): wenn der Submodus aktiv ist, hat er
        // Vorrang. ESC verlaesst dann nur den Submodus und nicht den ganzen
        // Trainings-Modus — sonst wuerde der Operator unbeabsichtigt
        // mehrere Klicks tief aussteigen.
        if (_isOperatorMode && Operator_TryHandleKey(e))
        {
            e.Handled = true;
            return;
        }

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

        // ESC mit aktivem MarkTool-Overlay: laufende Zeichnung verwerfen,
        // Canvas leeren. Nutzt _codingOverlayService weil MarkTool und
        // OperateurAnnotation diesen Service teilen.
        if (e.Key == Key.Escape && _codingOverlayService != null)
        {
            _codingOverlayService.CancelDraw();
            _codingSchemaManager.Cancel();
            if (CodingOverlayCanvas.IsMouseCaptured)
                CodingOverlayCanvas.ReleaseMouseCapture();
            if (_codingVm != null)
            {
                _codingVm.CurrentOverlay = null;
                UpdateCodingOverlayInfo(null);
            }
            if (CodingOverlayPopup.IsOpen)
                ClearTransientCodingCanvas(clearManualOverlay: true);
            e.Handled = true;
            return;
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
            LiveDetectionButton.IsChecked = !(LiveDetectionButton.IsChecked == true);
            LiveDetection_Click(LiveDetectionButton, new RoutedEventArgs());
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
        _videoPlayback.TogglePlayPause();
    }
}
