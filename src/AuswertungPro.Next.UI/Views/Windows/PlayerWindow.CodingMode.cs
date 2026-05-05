using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.QualityGate;
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
    // === Sub-E: EnterCodingMode + LoadExistingProtocolEventsAsImport + ExitCodingMode ===

    private void EnterCodingMode()
    {
        if (_isCodingMode || _haltungRecord == null) return;
        _isCodingMode = true;
        ResetFrameReadiness();

        // Video pausieren
        _player.SetPause(true);

        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
        }

        LiveDetectionButton.Visibility = Visibility.Collapsed;
        LiveDetectionStatusText.Visibility = Visibility.Collapsed;

        // Session-Services erstellen
        _codingSessionService = new CodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(_codingSessionService, _codingOverlayService);
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;

        // DN laden
        int nominalDn = 0;
        if (_haltungRecord.Fields.TryGetValue("DN_mm", out var dnStr)
            && int.TryParse(dnStr, out var dn) && dn > 0)
        {
            nominalDn = dn;
            _codingOverlayService.SetCalibration(new PipeCalibration { NominalDiameterMm = dn });
        }

        TxtCodingCalibDn.Text = nominalDn > 0 ? $"DN: {nominalDn} mm" : "DN: unbekannt";
        TxtCodingCalibStatus.Text = _codingOverlayService.IsCalibrated
            ? "Kalibriert" : "Nicht kalibriert";

        // Fallback: Haltungslaenge pruefen, ggf. manuell abfragen
        EnsureHaltungslaenge(_haltungRecord);

        // Session starten
        try
        {
            _codingVm.StartSessionCommand.Execute(_haltungRecord);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Codier-Modus", MessageBoxButton.OK, MessageBoxImage.Warning);
            ExitCodingMode();
            return;
        }

        // Pruefen ob Session tatsaechlich gestartet wurde
        // (StartSessionCommand faengt Fehler intern ab, z.B. fehlende Haltungslaenge)
        if (_codingSessionService.ActiveSession == null)
        {
            ExitCodingMode();
            return;
        }

        // Session pausieren (Video steht still, Schritt-Navigation)
        _codingSessionService.PauseSession();

        TxtCodingRange.Text = $"/ {_codingVm.EndMeter:F2}m";
        TxtCodingMeter.Text = "0.00m";

        // ALLE bestehenden Beobachtungen in Import-Referenz verschieben.
        // KI-Befunde-Liste startet LEER — KI erkennt frisch, User korrigiert.
        _codingImportEvents.Clear();
        var allExisting = _codingVm.Events.OrderBy(e => e.MeterAtCapture).ToList();
        _codingVm.Events.Clear();
        foreach (var ev in allExisting)
            _codingImportEvents.Add(ev);
        LstImportEvents.ItemsSource = _codingImportEvents;
        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();

        // WICHTIG: Auch session.Events leeren, damit CompleteSession() nur neue
        // KI-Events enthaelt (Import-Events sind in _codingImportEvents gesichert).
        // Sonst: Duplikate im Protokoll (Import + neue KI-Events).
        _codingSessionService.ActiveSession?.Events.Clear();

        // KI-Events-Liste binden (startet leer)
        LstCodingEvents.ItemsSource = _codingVm.Events;
        RunCodingDefectCount.Text = "0";

        // UI einblenden
        CodingOverlayPopup.IsOpen = true;
        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(700);
        CodingToolbar.Visibility = Visibility.Visible;

        // PipeGraphTimeline einrichten und einblenden
        PipeTimeline.TotalLength = _codingVm.EndMeter;
        PipeTimeline.MeterAccessor = obj => obj is CodingEvent ce ? ce.MeterAtCapture : 0;
        PipeTimeline.CodeAccessor = obj => obj is CodingEvent ce ? ce.Entry.Code : "?";
        PipeTimeline.ConfidenceAccessor = obj => obj is CodingEvent ce && ce.AiContext != null
            ? ce.AiContext.Confidence : -1;
        PipeTimeline.IsRejectedAccessor = obj => obj is CodingEvent ce
            && CodingSessionViewModel.GetDefectStatus(ce) == DefectStatus.Rejected;
        PipeTimeline.Markers = _codingVm.Events;
        PipeTimeline.NavigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (_codingVm.IsRunning || _codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });
        CodingTimelinePanel.Visibility = Visibility.Visible;

        // KI initialisieren + OSD-Timer starten
        InitCodingAi();
        StartCodingOsdTimer();

        // OSD-Badge sofort sichtbar
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";

        // Bestehende Protokoll-Eintraege direkt in Import-Referenz laden
        // (NICHT in KI-Befunde — die startet leer)
        LoadExistingProtocolEventsAsImport();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Import-Referenz-Liste.
    /// KI-Befunde-Liste bleibt leer (KI erkennt frisch).
    /// </summary>
    private void LoadExistingProtocolEventsAsImport()
    {
        if (_haltungRecord?.Protocol?.Current?.Entries == null) return;

        var entries = _haltungRecord.Protocol.Current.Entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        foreach (var entry in entries)
        {
            // Duplikat-Check (CodingSessionService hat evtl. schon geladen)
            if (_codingImportEvents.Any(ev => ev.Entry.EntryId == entry.EntryId))
                continue;

            _codingImportEvents.Add(new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? 0,
                VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
            });
        }

        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();
    }

    private void ExitCodingMode()
    {
        if (!_isCodingMode) return;
        _isCodingMode = false;

        // User-Klage 2026-04-25: "Wenn ich nach dem Codieren das Fenster schliesse,
        // schliesst es das ganze Programm." Ursache: Dieser 90-Zeilen-Cleanup ohne
        // globalen Schutz. CloseOpenStreckenschaeden zeigt einen Dialog, der bei
        // Window-Close-Race werfen kann. EnsureRohrendeExists schreibt in
        // Datenstrukturen die schon disposed sein koennen. Jede dieser Exceptions
        // eskaliert ueber DispatcherUnhandledException und kann die App killen.
        // Fix: jeder Block einzeln in try/catch via Safe()-Helper.
        void Safe(string step, Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExitCodingMode] {step}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Beim Verlassen: IMMER offene Streckenschaeden schliessen
        // (egal ob Rohrende, Abbruch oder einfacher Exit)
        bool exitAborted = false;
        Safe("Streckenschaeden+Endcode", () =>
        {
            if (_codingVm != null && _codingVm.Events.Count > 0)
            {
                var endMeter = _codingLastOsdMeter ?? _codingVm.EndMeter;
                if (!CloseOpenStreckenschaeden(endMeter))
                {
                    // User hat "Abbrechen" geklickt → Exit abbrechen, weiter codieren
                    _isCodingMode = true;
                    exitAborted = true;
                    return;
                }

                // Ende-Code nur einfuegen wenn weder BCE (Rohrende) noch BDC (Abbruch) vorhanden
                bool hasEndCode = _codingVm.Events.Any(e =>
                    string.Equals(e.Entry.Code, "BCE", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.Entry.Code, "BDC", StringComparison.OrdinalIgnoreCase));
                if (!hasEndCode)
                {
                    var endTime = TimeSpan.FromMilliseconds(_player?.Length ?? 0);
                    EnsureRohrendeExists(_codingVm.EndMeter, endTime);
                }
            }
        });
        if (exitAborted) return;

        Safe("Timer-Stop", () =>
        {
            StopCodingOsdTimer();
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;
            StopCodingAiPulse();
        });

        Safe("AnalysisCts", () =>
        {
            _codingAnalysisCts?.Cancel();
            _codingAnalysisCts?.Dispose();
            _codingAnalysisCts = null;
        });

        Safe("ImportEvents-Clear", () =>
        {
            _codingImportEvents.Clear();
            LstImportEvents.ItemsSource = null;
        });

        Safe("ConfirmationPanels-Hide", () =>
        {
            CodingConfirmationPanel.Visibility = Visibility.Collapsed;
            DetectionConfirmationPanel.Visibility = Visibility.Collapsed;
            _codingPendingConfirmEvent = null;
            _codingPendingGateResult = null;
            _detectionPendingFindings = null;
            _detectionPendingFrameBytes = null;
            _detectionPendingTimestampSec = null;
            DetectionCanvas.Children.Clear();
            if (!_isDetecting)
                DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        });

        Safe("OverlayCanvas-Cleanup", () =>
        {
            if (CodingOverlayCanvas.IsMouseCaptured)
                CodingOverlayCanvas.ReleaseMouseCapture();
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.Children.Clear();
            CodingOverlayCanvas.IsHitTestVisible = false;
            CodingOverlayCanvas.Cursor = Cursors.Arrow;
        });

        Safe("UI-Hide", () =>
        {
            CodingSidePanel.Visibility = Visibility.Collapsed;
            CodingSidePanelColumn.Width = new GridLength(0);
            CodingToolbar.Visibility = Visibility.Collapsed;
            CodingTimelinePanel.Visibility = Visibility.Collapsed;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
            CodingCalibrationHint.Visibility = Visibility.Collapsed;
            CodingMeasurementPanel.Visibility = Visibility.Collapsed;
            OsdMeterBadge.Visibility = Visibility.Collapsed;
            LiveDetectionButton.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Visibility = _isDetecting ? Visibility.Visible : Visibility.Collapsed;
        });

        Safe("Tool-State-Reset", () =>
        {
            _activeCodingToolName = null;
            TxtActiveToolLabel.Text = "";
            BtnCodingLiveAi.IsChecked = false;
            TxtCodingAiStage.Text = string.Empty;
            _codingSchemaManager.Cancel();
            _codingSchemaType = null;
        });

        Safe("VM-Unsubscribe+Null", () =>
        {
            if (_codingVm != null)
                _codingVm.PropertyChanged -= CodingVm_PropertyChanged;
            _codingVm = null;
            _codingSessionService = null;
            _codingOverlayService = null;
            _codingIsCalibrating = false;
            _codingCalibStart = null;
            ResetFrameReadiness(); // setzt auch _codingLastOsdMeter = null
            _codingOverlaySuspendDepth = 0;
            _codingOverlayWasOpenBeforeSuspend = false;
        });
    }
    // === Sub-F: KI-Pfad — InitCodingAi + RunCodingAnalysisAsync ===

    // --- Coding KI-Analyse ---

    private async void InitCodingAi()
    {
        try
        {
            var config = AiRuntimeConfig.Load();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Kuenstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = config.CreateOllamaClient();
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel, config.ReferenceVisionModel);
            _codingQualityGate = new QualityGateService();

            // Multi-Model Pipeline (YOLO → DINO → SAM) initialisieren
            try
            {
                var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://localhost:8100";
                _codingVisionClient = new Ai.Pipeline.VisionPipelineClient(new Uri(sidecarUrl));
                var health = await _codingVisionClient.HealthCheckAsync();
                if (health != null)
                {
                    _codingMultiModel = new Ai.Pipeline.SingleFrameMultiModelService(_codingVisionClient);
                    // Codier-Modus: Direkt Qwen, Sidecar nur fuer SAM-Nachsegmentierung
                    SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{CompactModelName(_codingAiModelName)} + SAM-Segmentierung");
                }
                else
                {
                    SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{CompactModelName(_codingAiModelName)} (ohne SAM)");
                }
            }
            catch
            {
                SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_codingAiModelName)} (ohne SAM)");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(_codingAiModelName));

            // Few-Shot-Beispiele laden — ohne diese findet die KI drastisch weniger (Audit-Fix)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_codingEnhancedVision == null) return;
                    var store = new Ai.Training.FewShotExampleStore();
                    await _codingEnhancedVision.EnableFewShotAsync(store);
                    var fsDiag = _codingEnhancedVision.FewShotDiagnostics ?? "keine";
                    Dispatcher.Invoke(() =>
                        SetCodingAiState("Kuenstliche Intelligenz bereit (Few-Shot)", Color.FromRgb(0x22, 0xC5, 0x5E),
                            $"{CompactModelName(_codingAiModelName)} | {fsDiag}"));
                }
                catch (Exception fex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Player] Few-Shot laden fehlgeschlagen: {fex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

    private async void CodingAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunCodingAnalysisAsync("Aktuellen Frame analysieren...", disableAnalyzeButton: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingAnalyzeFrame_Click error: {ex.Message}");
        }
    }

    private async Task RunCodingAnalysisAsync(string activityText, bool disableAnalyzeButton = false,
        string? keywordHint = null, string? codeHint = null)
    {
        if ((_codingEnhancedVision == null && _codingLiveDetection == null && _codingMultiModel == null)
            || _codingIsAnalyzing) return;

        _codingIsAnalyzing = true;
        _codingAnalysisCts?.Cancel();
        _codingAnalysisCts = new CancellationTokenSource();

        try
        {
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = false;

            // Zeitstempel VOR dem Capture festhalten (CaptureSnapshotAsync wartet bis zu 1s)
            var captureTimestampSec = _player.Time / 1000.0;

            // ── YOLO-first Live-Analyse: YOLO26l-seg → SAM → optional Qwen-Eskalation ──
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44));
                    return;
                }

                var b64 = Convert.ToBase64String(pngBytes);
                int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;

                // ── Schritt 1: YOLO26l-seg Erkennung (2ms) + Kandidaten-Tracking ──
                LiveDetection? result = null;
                bool yoloHadFindings = false;

                if (_codingVisionClient != null && _codingMultiModel != null)
                {
                    try
                    {
                        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                            "YOLO Detection...", pulse: true);

                        var mmResult = await _codingMultiModel.AnalyzeFrameAsync(
                            pngBytes, dn, _codingOverlayService?.Calibration,
                            _codingAnalysisCts.Token);

                        if (mmResult.HasDetections && mmResult.SamResponse != null)
                        {
                            int imgW = mmResult.SamResponse.ImageWidth;
                            int imgH = mmResult.SamResponse.ImageHeight;

                            // Jede Detektion klassifizieren: Nahbereich oder Tiefe?
                            var nearIndices = new HashSet<int>();
                            var depthLabels = new List<string>();

                            for (int i = 0; i < mmResult.DinoDetections.Count; i++)
                            {
                                var d = mmResult.DinoDetections[i];
                                double nArea = ((d.X2 - d.X1) * (d.Y2 - d.Y1)) / (imgW * (double)imgH);
                                double ncx = ((d.X1 + d.X2) / 2.0) / imgW;
                                double ncy = ((d.Y1 + d.Y2) / 2.0) / imgH;

                                // Tiefe-Erkennung: Box-Mittelpunkt nahe Bildmitte = Fluchtpunkt = weit weg
                                // Unabhaengig von Box-Groesse (YOLO liefert grosse Boxen wegen Training)
                                double distFromCenter = Math.Sqrt(Math.Pow(ncx - 0.5, 2) + Math.Pow(ncy - 0.5, 2));
                                // Box beruehrt den Bildrand? → definitiv nah
                                double margin = 0.05;
                                bool touchesEdge = d.X1 / imgW < margin || d.Y1 / imgH < margin
                                               || d.X2 / imgW > (1 - margin) || d.Y2 / imgH > (1 - margin);
                                // Nah = Box beruehrt Rand ODER Mittelpunkt weit von Bildmitte
                                bool isNear = touchesEdge || distFromCenter > 0.25;

                                if (!isNear)
                                {
                                    // In der Tiefe → Kandidat merken (noch nicht protokollieren)
                                    _codingDepthCandidates[d.Label] = (captureTimestampSec, nArea, d.Confidence, d.Label);
                                    depthLabels.Add(d.Label);
                                }
                                else
                                {
                                    // Nahbereich → als Befund akzeptieren
                                    nearIndices.Add(i);

                                    // War das ein vorheriger Kandidat der jetzt nah ist? → bestaetigt!
                                    if (_codingDepthCandidates.Remove(d.Label))
                                        System.Diagnostics.Debug.WriteLine(
                                            $"[Kandidat→Befund] {d.Label} wurde bestaetigt (von Tiefe zu Nah)");
                                }
                            }

                            if (nearIndices.Count > 0)
                            {
                                yoloHadFindings = true;

                                // Nur Nahbereich-Detektionen als Events + SAM-Masken
                                var acceptedIndices = AddMultiModelFindingsAsEvents(mmResult, captureTimestampSec);
                                // Nur nahe Masken rendern
                                var nearAccepted = acceptedIndices != null
                                    ? new HashSet<int>(acceptedIndices.Where(i => nearIndices.Contains(i)))
                                    : nearIndices;
                                ShowMultiModelResults(mmResult, nearAccepted);

                                int nearCount = _currentMmResult?.QuantifiedMasks.Count ?? 0;
                                var depthInfo = depthLabels.Count > 0
                                    ? $" | Tiefe: {string.Join(", ", depthLabels)}"
                                    : "";
                                SetCodingAiState(
                                    $"{nearCount} Befunde (YOLO){depthInfo}",
                                    Color.FromRgb(0x22, 0xC5, 0x5E),
                                    $"YOLO {mmResult.YoloTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms");

                                if (BtnCodingPauseMode.IsChecked == true && nearCount > 0)
                                {
                                    _player?.SetPause(true);
                                    SetCodingAiState(
                                        $"{nearCount} Befunde — pausiert{depthInfo}",
                                        Color.FromRgb(0x38, 0xBD, 0xF8),
                                        "Delete = loeschen | O = OK | Leertaste = weiter");
                                }
                            }
                            else if (depthLabels.Count > 0)
                            {
                                // Nur Tiefen-Kandidaten → anzeigen als Vorschau
                                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                                SetCodingAiState(
                                    $"Vorschau: {string.Join(", ", depthLabels)} (in Tiefe)",
                                    Color.FromRgb(0x94, 0xA3, 0xB8),
                                    "Kamera muss naeher — wird bestaetigt wenn im Nahbereich");
                            }
                            else
                            {
                                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                            }
                        }
                        else
                        {
                            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                        }
                    }
                    catch (Exception yoloEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[YOLO-Live] {yoloEx.Message}");
                    }
                }

                // ── Schritt 2: Qwen-Fallback wenn YOLO nichts findet ODER Sidecar offline ──
                if (!yoloHadFindings && _codingEnhancedVision != null)
                {
                    try
                    {
                        SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                            $"Qwen-Fallback: {CompactModelName(_codingAiModelName)}", pulse: true);

                        var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                            b64, _codingAnalysisCts.Token);
                        result = Ai.LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);

                        System.Diagnostics.Debug.WriteLine(
                            _codingEnhancedVision.LastRawOutput ?? "[Qwen] keine Rohdaten");

                        ShowCodingAiResults(result);
                    }
                    catch (Exception qwenEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Qwen-Fallback] {qwenEx.Message}");
                        SetCodingAiState("Analyse fehlgeschlagen",
                            Color.FromRgb(0xEF, 0x44, 0x44), qwenEx.Message);
                    }
                }

                // Wenn weder YOLO noch Qwen etwas gefunden haben
                if (!yoloHadFindings && (result == null || result.Findings.Count == 0))
                {
                    SetCodingAiState("Kein Befund erkannt",
                        Color.FromRgb(0x94, 0xA3, 0xB8), "YOLO + Qwen");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
        finally
        {
            _codingIsAnalyzing = false;
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = true;
        }
    }
}