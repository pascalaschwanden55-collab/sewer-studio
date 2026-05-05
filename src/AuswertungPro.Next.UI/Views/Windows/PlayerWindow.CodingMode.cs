using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.F Sub-A: Coding-Mode-State + Pulse-Animation + OSD-Timer.
//
// Cluster B2 (CodingMode) ist der groesste PlayerWindow-Cluster (~25%).
// Sub-A: kleinere klar abgegrenzte UI-State-Methoden:
//   - Status-Badge mit Pulsing-Ring
//   - "aktueller Code"-Anzeige am Meter
//   - Video-Sync zum CodingVm-Meter
//   - OSD-Timer (Meter aus VLC-Bild auslesen)
//
// Felder im Hauptpartial: _codingVm, _codingAiPulseRunning, _isCodingMode,
//   _codingOsdTimer, _codingOsdReading, _codingLiveDetection,
//   _codingLastOsdMeter, _player.
public partial class PlayerWindow
{
    // ─── KI-Status-Badge im Codier-Modus ──────────────────────────────────────

    private void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetCodingAiState(status, dotColor, stage, pulse));
            return;
        }

        TxtCodingAiStatus.Text = status;
        TxtCodingAiStage.Text = stage ?? string.Empty;
        CodingAiDot.Fill = new SolidColorBrush(dotColor);
        if (pulse)
            StartCodingAiPulse();
        else
            StopCodingAiPulse();
    }

    private void StartCodingAiPulse()
    {
        if (_codingAiPulseRunning)
            return;

        _codingAiPulseRunning = true;
        CodingAiPulseRing.Opacity = 1.0;
        if (CodingAiPulseRing.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            CodingAiPulseRing.RenderTransform = scale;
        }

        var scaleAnim = new DoubleAnimation
        {
            From = 1.0,
            To = 2.2,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };
        var opacityAnim = new DoubleAnimation
        {
            From = 0.75,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(900),
            RepeatBehavior = RepeatBehavior.Forever
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void StopCodingAiPulse()
    {
        _codingAiPulseRunning = false;

        if (CodingAiPulseRing.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }

        CodingAiPulseRing.BeginAnimation(UIElement.OpacityProperty, null);
        CodingAiPulseRing.Opacity = 0;
    }

    // ─── "Aktueller Code"-Badge am Meter ──────────────────────────────────────

    private void UpdateCodingCurrentCode()
    {
        if (_codingVm == null || _codingVm.Events.Count == 0)
        {
            CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        // Aktuellen Meter ermitteln: OSD-Wert bevorzugen, sonst Video-Position berechnen
        double currentMeter;
        if (_codingLastOsdMeter.HasValue)
        {
            currentMeter = _codingLastOsdMeter.Value;
        }
        else if (_player.Length > 0 && _codingVm.EndMeter > 0)
        {
            currentMeter = (_player.Time / (double)_player.Length) * _codingVm.EndMeter;
        }
        else
        {
            currentMeter = _codingVm.CurrentMeter;
        }

        // Naechsten Code innerhalb ±0.5m finden
        var nearestEvent = _codingVm.Events
            .Where(ev => Math.Abs(ev.MeterAtCapture - currentMeter) < 0.5)
            .OrderBy(ev => Math.Abs(ev.MeterAtCapture - currentMeter))
            .FirstOrDefault();

        if (nearestEvent != null)
        {
            TxtCodingCurrentCode.Text = $"▶ {nearestEvent.MeterAtCapture:F2}m {nearestEvent.Entry.Code} {nearestEvent.Entry.Beschreibung}";
            CodingCurrentCodeBadge.Visibility = Visibility.Visible;
        }
        else
        {
            // Naechsten bevorstehenden Code anzeigen
            var nextEvent = _codingVm.Events
                .Where(ev => ev.MeterAtCapture > currentMeter)
                .OrderBy(ev => ev.MeterAtCapture)
                .FirstOrDefault();

            if (nextEvent != null)
            {
                var distM = nextEvent.MeterAtCapture - currentMeter;
                TxtCodingCurrentCode.Text = $"→ in {distM:F1}m: {nextEvent.Entry.Code}";
                CodingCurrentCodeBadge.Visibility = Visibility.Visible;
            }
            else
            {
                CodingCurrentCodeBadge.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SyncVideoToCodingMeter()
    {
        if (_codingVm == null || _player.Length <= 0 || _codingVm.EndMeter <= 0) return;
        double fraction = _codingVm.CurrentMeter / _codingVm.EndMeter;
        long targetMs = (long)(fraction * _player.Length);
        _player.Time = Math.Clamp(targetMs, 0, _player.Length);
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
    }

    // ─── OSD-Timer (Meter aus VLC-Bild auslesen) ─────────────────────────────

    private void StartCodingOsdTimer()
    {
        _codingOsdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _codingOsdTimer.Tick += async (_, _) =>
        {
            if (!_isCodingMode || _codingOsdReading || _codingLiveDetection == null) return;
            _codingOsdReading = true;
            try
            {
                await CodingReadOsdMeterAsync();
            }
            finally
            {
                _codingOsdReading = false;
            }
        };
        _codingOsdTimer.Start();
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdTimer?.Stop();
        _codingOsdTimer = null;
        _codingOsdReading = false;
    }

    // ─── Coding-UI-Update ──────────────────────────────────────────────────────

    // Flag: wird true wenn Meter-Navigation (Next/Previous) ausloest
    private bool _codingNavPending;

    private void CodingModeExit_Click(object sender, RoutedEventArgs e) => ExitCodingMode();

    // Benannter Handler fuer sauberes Cleanup via -=
    private void CodingVm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => UpdateCodingUi(e.PropertyName));

    private void UpdateCodingUi(string? propertyName)
    {
        if (_codingVm == null) return;
        TxtCodingMeter.Text = $"{_codingVm.CurrentMeter:F2}m";
        PipeTimeline.CurrentMeter = _codingVm.CurrentMeter;
        // Video NUR synchronisieren wenn explizite Navigation (Next/Previous)
        // Verhindert Zurueckspringen beim normalen Abspielen
        if (propertyName is nameof(CodingSessionViewModel.CurrentMeter) && _codingNavPending)
        {
            _codingNavPending = false;
            SyncVideoToCodingMeter();
        }
        UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);

        // Aktuellen Code am Zeitstempel anzeigen (Echtzeit)
        UpdateCodingCurrentCode();

        // Statistiken aktualisieren (nur bei relevanten Property-Aenderungen)
        if (propertyName is nameof(CodingSessionViewModel.StatAutoAccepted) or
            nameof(CodingSessionViewModel.StatPending) or
            nameof(CodingSessionViewModel.StatReviewRequired) or
            nameof(CodingSessionViewModel.StatAverageConfidence) or
            nameof(CodingSessionViewModel.EventCount) or
            null)
        {
            UpdateCodingStatistics();
        }
    }

    // ─── Navigation: Next / Previous + OSD-Reset ──────────────────────────────

    private async void CodingNext_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MoveNextCommand.Execute(null);
            // Video pausieren bei Schritt-Navigation
            _player.SetPause(true);
            // OSD-Meter automatisch lesen nach Navigation
            _codingLastOsdMeter = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingNext_Click error: {ex.Message}");
        }
    }

    private async void CodingPrevious_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_codingVm == null) return;
            _codingNavPending = true;
            _codingVm.MovePreviousCommand.Execute(null);
            _player.SetPause(true);
            _codingLastOsdMeter = null;
            await CodingReadOsdMeterAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingPrevious_Click error: {ex.Message}");
        }
    }

    // ─── Coding-Werkzeuge (Toolbar-Buttons) ────────────────────────────────────

    private void CodingToolRect_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Markieren");

    // ─── Defect Accept / Edit / Reject (Click-Handler) ─────────────────────────

    private void CodingAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        ShrinkEnlargedListItem();

        // Fallback: wenn kein SelectedDefect, aber ListBox hat Auswahl → uebernehmen
        if (_codingVm?.SelectedDefect == null && LstCodingEvents.SelectedItem is CodingEvent fallback)
            _codingVm!.SelectedDefect = fallback;

        _codingVm?.AcceptDefectCommand.Execute(null);
        if (_codingVm?.SelectedDefect != null)
        {
            var ev = _codingVm.SelectedDefect;

            // Auf Sperrliste setzen → wird bei naechster Analyse nicht erneut erkannt
            _rejectedFindings.Add(MakeRejectionKey(ev.Entry.Code, ev.MeterAtCapture));

            // ALLE Overlays komplett raeumen — Befund ist erledigt
            CodingOverlayCanvas.Children.Clear();
            DetectionCanvas.Children.Clear();
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
            _currentMmResult = null;
            _previewMmResult = null;

            // Positiv-Feedback speichern (gleich wie O auf Maske)
            var label = ev.AiContext?.Reason ?? ev.Entry.Code ?? "";
            Task.Run(() => SavePositiveFeedbackAsync(label, ev.Entry.Code, ev.MeterAtCapture))
                .SafeFireAndForget("PositiveFeedbackEntry");

            UpdateCodingDefectDetailPanel(ev);
            RefreshCodingEventsList();

            // Wenn Pausenmodus → Video weiter
            if (BtnCodingPauseMode.IsChecked == true)
                ResumeAfterPause();
        }
    }

    private void CodingEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        var ev = _codingVm.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null) return;
        _codingVm.SelectedDefect = ev;
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var entry = ev.Entry;
            var explorerVm = new VsaCodeExplorerViewModel(
                entry, entry.MeterStart, entry.Zeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _codingVm.VideoPath, _codingVm.CurrentVideoTime)
            {
                Owner = this,
                LiveSnapshotProvider = () =>
                {
                    var snapPath = Path.Combine(Path.GetTempPath(),
                        $"coding_live_{System.Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };
            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                entry.Code = result.Code;
                entry.Beschreibung = result.Beschreibung;
                entry.CodeMeta = result.CodeMeta;
                entry.MeterStart = result.MeterStart;
                entry.MeterEnd = result.MeterEnd;
                entry.Zeit = result.Zeit;
                entry.IsStreckenschaden = result.IsStreckenschaden;
                entry.FotoPaths = result.FotoPaths;
                _codingSessionService?.UpdateEvent(ev.EventId, entry, ev.Overlay);
                ev.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? ev.MeterAtCapture;
                ev.VideoTimestamp = entry.Zeit ?? ev.VideoTimestamp;

                if (ev.AiContext != null)
                    _codingVm.EditDefectCommand.Execute(null);
                RefreshCodingEventsList();
                UpdateCodingDefectDetailPanel(ev);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }

    // ─── Sub-D: Coding-List-Display (Refresh, Detail-Panel, Statistik, Shrink) ─

    private void RefreshCodingEventsList()
    {
        if (_codingVm == null) return;

        // Nach Meter sortieren, dann nach Videozeit
        var sorted = _codingVm.Events
            .OrderBy(e => e.MeterAtCapture)
            .ThenBy(e => e.VideoTimestamp)
            .ToList();

        var selected = LstCodingEvents.SelectedItem;
        _codingVm.Events.Clear();
        foreach (var ev in sorted)
            _codingVm.Events.Add(ev);

        LstCodingEvents.ItemsSource = null;
        LstCodingEvents.ItemsSource = _codingVm.Events;
        if (selected != null)
            LstCodingEvents.SelectedItem = selected;

        // Verzoegert Einfaerbung nach Layout-Update
        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, DispatcherPriority.Loaded);
        UpdateCodingStatistics();
    }

    private void UpdateCodingDefectDetailPanel(CodingEvent ev)
    {
        // CodingDefectDetailPanel.Visibility = Visibility.Visible; // Deaktiviert: Details sind im oberen Panel

        TxtCodingDetailCode.Text = ev.Entry.Code;
        TxtCodingDetailDescription.Text = ev.Entry.Beschreibung;
        TxtCodingDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        // Uhrposition
        TxtCodingDetailClock.Text = ev.Overlay?.ClockFrom != null
            ? $"{ev.Overlay.ClockFrom:F0}h"
            : "–";

        // Schweregrad
        if (ev.Entry.CodeMeta?.Parameters != null &&
            ev.Entry.CodeMeta.Parameters.TryGetValue("vsa.schweregrad", out var sev))
            TxtCodingDetailSeverity.Text = sev;
        else
            TxtCodingDetailSeverity.Text = "–";

        // Konfidenz + Farbe
        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtCodingDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtCodingDetailConfidence.Foreground = CodingSessionViewModel.GetConfidenceBrush(conf);
            CodingDefectDetailBorderBrush.Color = ((SolidColorBrush)CodingSessionViewModel.GetZoneBrush(conf)).Color;
        }
        else
        {
            TxtCodingDetailConfidence.Text = "–";
            TxtCodingDetailConfidence.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            CodingDefectDetailBorderBrush.Color = Color.FromRgb(0x3B, 0x82, 0xF6);
        }

        // Status
        var status = CodingSessionViewModel.GetDefectStatus(ev);
        TxtCodingDetailStatus.Text = $"Status: {CodingStatusToDisplayText(status)}";

        CodingDefectActionGrid.Visibility = Visibility.Visible;
        BtnCodingAcceptDefect.Visibility = Visibility.Visible;
        BtnCodingEditDefect.Visibility = Visibility.Visible;
        BtnCodingRejectDefect.Visibility = Visibility.Visible;
    }

    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        for (int i = 0; i < LstCodingEvents.Items.Count; i++)
        {
            if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;

            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                zoneDot.Fill = status switch
                {
                    DefectStatus.Accepted or DefectStatus.AutoAccepted
                        => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                    DefectStatus.AcceptedWithEdit
                        => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
                    DefectStatus.Rejected
                        => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    _ => ev.AiContext != null
                        ? CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence)
                        : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
                };
            }

            var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
            if (confText != null && ev.AiContext != null)
            {
                confText.Text = $"{ev.AiContext.Confidence * 100:F0}%";
                confText.Foreground = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
            }
            else if (confText != null)
            {
                confText.Text = "";
            }

            var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = status switch
                {
                    DefectStatus.AutoAccepted      => "✓",
                    DefectStatus.Accepted           => "✓",
                    DefectStatus.AcceptedWithEdit   => "✎",
                    DefectStatus.Pending            => "⏳",
                    DefectStatus.ReviewRequired     => "⚠",
                    DefectStatus.Rejected           => "✗",
                    _ => ""
                };
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }
    }

    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindCodingChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindCodingChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Statistiken im Seitenpanel aktualisieren (direkt berechnet).</summary>
    private void UpdateCodingStatistics()
    {
        if (_codingVm == null) return;

        RunCodingDefectCount.Text = _codingVm.Events.Count.ToString();

        var aiEvents = _codingVm.Events.Where(e => e.AiContext != null).ToList();
        int autoAccepted = 0, pending = 0, reviewRequired = 0;

        foreach (var ev in aiEvents)
        {
            var status = CodingSessionViewModel.GetDefectStatus(ev);
            switch (status)
            {
                case DefectStatus.AutoAccepted:
                case DefectStatus.Accepted:
                case DefectStatus.AcceptedWithEdit:
                    autoAccepted++;
                    break;
                case DefectStatus.Pending:
                    pending++;
                    break;
                case DefectStatus.ReviewRequired:
                    reviewRequired++;
                    break;
            }
        }

        RunCodingOpenCount.Text = (pending + reviewRequired).ToString();
        TxtCodingStatAutoAccepted.Text = autoAccepted.ToString();
        TxtCodingStatPending.Text = pending.ToString();
        TxtCodingStatReviewRequired.Text = reviewRequired.ToString();
        TxtCodingStatAvgConfidence.Text = aiEvents.Count > 0
            ? $"{aiEvents.Average(e => e.AiContext!.Confidence) * 100:F0}%"
            : "–";
    }

    private void ShrinkEnlargedListItem()
    {
        if (_enlargedListItem == null) return;

        _enlargedListItem.ClearValue(Control.BackgroundProperty);
        _enlargedListItem.ClearValue(Control.FontWeightProperty);

        if (_enlargedListItem.RenderTransform is ScaleTransform st)
        {
            var shrink = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        _enlargedListItem = null;
    }

    private void CodingRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        ShrinkEnlargedListItem();
        var ev = _codingVm?.SelectedDefect ?? LstCodingEvents.SelectedItem as CodingEvent;
        if (ev == null || _codingVm == null) return;

        // Auf Sperrliste setzen → wird bei naechster Analyse nicht erneut erkannt
        _rejectedFindings.Add(MakeRejectionKey(ev.Entry.Code, ev.MeterAtCapture));

        // ALLE Overlays komplett raeumen
        CodingOverlayCanvas.Children.Clear();
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        _currentMmResult = null;
        _previewMmResult = null;

        // Negativ-Feedback speichern (gleich wie Delete auf Maske)
        var label = ev.AiContext?.Reason ?? ev.Entry.Code ?? "";
        Task.Run(() => SaveNegativeFeedbackAsync(label, ev.Entry.Code, ev.MeterAtCapture))
            .SafeFireAndForget("NegativeFeedbackEntry");

        // Ablehnen = Eintrag komplett entfernen (nicht nur Status setzen)
        _codingSessionService?.RemoveEvent(ev.EventId);
        _codingVm.Events.Remove(ev);
        _codingVm.SelectedDefect = null;
        CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        RefreshCodingEventsList();

        // Wenn keine Masken mehr sichtbar → Video weiter (gleich wie Delete auf Maske)
        if (BtnCodingPauseMode.IsChecked == true && !HasVisibleMasks())
            ResumeAfterPause();
    }
}
