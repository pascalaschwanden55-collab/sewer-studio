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
    // === Sub-G: Multi-Model Result-Rendering ===

    /// <param name="mmResult">Analyse-Ergebnis (ungefiltert).</param>
    /// <param name="acceptedIndices">Masken-Indices die ein Event bekommen haben (null = VSA-Code-Filter).</param>
    private void ShowMultiModelResults(Ai.Pipeline.SingleFrameResult mmResult, HashSet<int>? acceptedIndices = null)
    {
        // Masken aufteilen: akzeptierte (nah) vs. verworfene (fern/ungueltig)
        var validIndices = new List<int>();
        var rejectedIndices = new List<int>();
        for (int i = 0; i < mmResult.QuantifiedMasks.Count; i++)
        {
            bool accepted = acceptedIndices != null
                ? acceptedIndices.Contains(i)
                : Ai.VsaCodeResolver.InferCodeFromLabel(mmResult.QuantifiedMasks[i].Label) != null;

            if (accepted) validIndices.Add(i);
            else rejectedIndices.Add(i);
        }

        // Akzeptierte Masken als Haupt-Ergebnis
        Ai.Pipeline.SingleFrameResult FilterByIndices(List<int> indices)
        {
            var fq = indices.Select(i => mmResult.QuantifiedMasks[i]).ToList();
            var fd = indices.Where(i => i < mmResult.DinoDetections.Count)
                .Select(i => mmResult.DinoDetections[i]).ToList();
            var fs = mmResult.SamResponse != null
                ? new Ai.Pipeline.SamResponse(
                    indices.Where(i => i < mmResult.SamResponse.Masks.Count)
                        .Select(i => mmResult.SamResponse.Masks[i]).ToList(),
                    mmResult.SamResponse.ImageWidth, mmResult.SamResponse.ImageHeight,
                    mmResult.SamResponse.InferenceTimeMs)
                : mmResult.SamResponse;
            return new Ai.Pipeline.SingleFrameResult(
                mmResult.IsRelevant, fd, fs, fq,
                mmResult.YoloTimeMs, mmResult.DinoTimeMs, mmResult.SamTimeMs, mmResult.Error);
        }

        var nearResult = validIndices.Count < mmResult.QuantifiedMasks.Count
            ? FilterByIndices(validIndices) : mmResult;
        _currentMmResult = nearResult;
        _previewMmResult = rejectedIndices.Count > 0 ? FilterByIndices(rejectedIndices) : null;

        // Alte Masken entfernen
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        // SAM-Masken rendern: nahe Befunde farbig + klickbar
        if (nearResult.SamResponse != null && nearResult.QuantifiedMasks.Count > 0)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                nearResult.SamResponse,
                nearResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                onMaskClicked: OnMaskOverlayClicked,
                onMaskDeleted: OnMaskOverlayDeleted);
        }

        // Ferne Befunde (innerhalb Rohrkreis) grau + gedimmt rendern (Vorschau)
        if (_previewMmResult?.SamResponse != null && _previewMmResult.QuantifiedMasks.Count > 0)
        {
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                _previewMmResult.SamResponse,
                _previewMmResult.QuantifiedMasks,
                CodingOverlayCanvas.ActualWidth,
                CodingOverlayCanvas.ActualHeight,
                previewMode: true,
                indexOffset: nearResult.QuantifiedMasks.Count);
        }

        // Kalibrierkreis anzeigen
        _showReferenceDn = true;
        RenderReferenceDn();
    }

    /// <summary>
    /// Erstellt Events und gibt die Masken-Indices zurueck die tatsaechlich ein Event bekommen haben.
    /// </summary>
    private HashSet<int> AddMultiModelFindingsAsEvents(
        Ai.Pipeline.SingleFrameResult mmResult, double captureTimestampSec)
    {
        var acceptedIndices = new HashSet<int>();
        if (_codingVm == null || _codingSessionService == null) return acceptedIndices;

        double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;

        // BCD wird NICHT mehr automatisch erzeugt — nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        for (int i = 0; i < mmResult.QuantifiedMasks.Count; i++)
        {
            var quant = mmResult.QuantifiedMasks[i];
            var dino = i < mmResult.DinoDetections.Count ? mmResult.DinoDetections[i] : null;

            // Erkennungszone: nur Detektionen AUSSERHALB des Rohrkreises (nah) auswerten
            // Detektionen in der Tiefe (innerhalb Rohrkreis) → grau anzeigen, kein Event
            int imgW = mmResult.SamResponse?.ImageWidth ?? 1;
            int imgH = mmResult.SamResponse?.ImageHeight ?? 1;
            if (!IsInsideDetectionZone(dino, imgW, imgH))
                continue;

            // Gemeinsamer Resolver: DINO-Label → LiveFrameFinding → ResolveFindingCodeForCoding
            // So laeuft der Multi-Model-Pfad durch exakt denselben Code wie Qwen.
            var pseudoFinding = new LiveFrameFinding(
                Label: quant.Label,
                Severity: EstimateSeverityFromQuantification(quant),
                PositionClock: NormalizeClockPosition(quant.ClockPosition),
                ExtentPercent: quant.ExtentPercent,
                VsaCodeHint: null,  // DINO liefert englische Labels, kein VSA-Code
                HeightMm: quant.HeightMm,
                WidthMm: quant.WidthMm,
                IntrusionPercent: quant.IntrusionPercent,
                CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: dino != null ? dino.X1 / (mmResult.SamResponse?.ImageWidth ?? 1) : null,
                BboxY1: dino != null ? dino.Y1 / (mmResult.SamResponse?.ImageHeight ?? 1) : null,
                BboxX2: dino != null ? dino.X2 / (mmResult.SamResponse?.ImageWidth ?? 1) : null,
                BboxY2: dino != null ? dino.Y2 / (mmResult.SamResponse?.ImageHeight ?? 1) : null);

            // Gemeinsamer Resolver (identisch mit Qwen-Pfad)
            var code = ResolveFindingCodeForCoding(pseudoFinding, meter);
            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Kein VSA-Code fuer Label='{quant.Label}' — uebersprungen");
                continue;
            }

            // Sperrliste: vom Benutzer abgelehnte Befunde nicht erneut einfuegen
            if (_rejectedFindings.Contains(MakeRejectionKey(code, meter)))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Gesperrt: {code} @ {meter:F1}m (vom Benutzer geloescht)");
                continue;
            }

            // Kunststoffrohr-Regel: Infiltration (BBF) und Bodeneindringung (BBD) sind
            // bei intakten Kunststoffrohren physikalisch unmoeglich — das Rohr ist dicht.
            // Nur wenn ein Strukturschaden (BA = Riss/Bruch/Versatz) in der Naehe ist,
            // kann Wasser eindringen. Ohne Begleitschaden → Fehlalarm verwerfen.
            if (code.StartsWith("BBF", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith("BBD", StringComparison.OrdinalIgnoreCase))
            {
                if (IsKunststoffRohr() && !HasNearbyStructuralDamage(meter))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Multi-Model] Kunststoff-Filter: {code} @ {meter:F1}m verworfen — kein Begleitschaden");
                    continue;
                }
            }

            var officialLabel = LookupVsaLabel(code);

            // BCD/BCE existieren pro Haltung nur EINMAL — Meterstand-unabhaengige Dedup
            // Primaer gegen session.Events pruefen (wird nie gecleared).
            if ((string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
                && (_codingSessionService?.ActiveSession?.Events.Any(e =>
                        CodesMatchForDedup(e.Entry.Code, code)) == true
                    || _codingVm.Events.Any(e => CodesMatchForDedup(e.Entry.Code, code))))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = _codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, code) &&
                IsAlreadyCovered(e, meter, pseudoFinding));
            if (coveringEvent != null) continue;

            // QualityGate mit Multi-Model Evidenz
            double dinoConf = dino?.Confidence ?? quant.Confidence;
            var evidence = new EvidenceVector(
                YoloConf: 0.8,
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(dinoConf, TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Multi-Model")!;

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Ai,
                Code = code,
                Beschreibung = officialLabel ?? quant.Label,
                MeterStart = meter,
                Zeit = videoTime
            };

            // Messungen in CodeMeta (gleiche Logik wie Qwen-Pfad)
            ApplyQuantificationToEntry(entry, code, quant);

            var codingEvent = _codingSessionService?.AddEvent(entry);
            if (codingEvent is not null)
                codingEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = code,
                    Confidence = gateResult.CompositeConfidence,
                    Reason = $"{quant.Label} (DINO {dinoConf:P0})",
                    Decision = gateResult.IsGreen
                        ? CodingUserDecision.Accepted
                        : CodingUserDecision.Ignored
                };

            acceptedIndices.Add(i);
            anyAdded = true;
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }

        return acceptedIndices;
    }

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {CompactModelName(_codingAiModelName)}");
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // ── Zustandsautomat: Einblendung vs. echtes Videobild ──
        // Zuerst State aktualisieren, dann pruefen ob Frame analysiert werden darf.
        // Gating BEVOR irgendetwas ins UI geschrieben wird.
        UpdateFrameReadiness(result);

        if (!IsFrameReady())
        {
            // Ergebnis puffern statt verwerfen (Warmup-Phase)
            if (result.Findings.Count > 0)
                _pendingWarmupResult = result;

            SetCodingAiState("Dateneinblendung erkannt \u2014 \u00fcbersprungen",
                Color.FromRgb(0x94, 0xA3, 0xB8),
                $"Warte auf Videobild... (Bild {_codingOsdSkippedFrames} von 3)");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        // Warmup-Puffer nachtraeglich verarbeiten (erste Ready-Transition)
        if (_pendingWarmupResult != null)
        {
            var buffered = _pendingWarmupResult;
            _pendingWarmupResult = null;
            // Bestes gepuffertes Ergebnis verwenden wenn aktuelles leer ist
            if (result.Findings.Count == 0 && buffered.Findings.Count > 0)
                result = buffered;
        }

        // ── Ab hier: Frame ist bereit fuer Analyse ──

        // OSD-Meterstand uebernehmen (Plausibilitaet: nicht rueckwaerts springen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            var newMeter = result.MeterReading.Value;
            var prevMeter = _codingLastOsdMeter ?? 0;

            // Nur vorwaerts aktualisieren (Kamera faehrt nicht rueckwaerts)
            // Ausnahme: erster Meter-Wert (currentMeter == 0) darf immer gesetzt werden
            if (newMeter >= prevMeter || prevMeter == 0)
            {
                _codingLastOsdMeter = newMeter;
                _codingSessionService?.MoveToMeter(newMeter);
                OsdMeterBadge.Visibility = Visibility.Visible;
                TxtOsdMeter.Text = $"{newMeter:F2}m (OSD)";
            }
            else
            {
                // Qwen hat kleineren Meter gelesen → ignorieren (wahrscheinlich OSD-Fehler)
                System.Diagnostics.Debug.WriteLine(
                    $"[OSD] Meter-Ruecksprung ignoriert: {newMeter:F2}m < {prevMeter:F2}m");
            }
        }

        // ── Findings filtern: VSA-Validierung + Deduplizierung ──
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = result.MeterReading ?? (_codingVm?.CurrentMeter ?? 0);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Befund"
                : "Kein Befund";
            SetCodingAiState(noDamageText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay aktualisiert");
            CodingFindingsList.ItemsSource = null;
            DetectionCanvas.Children.Clear();
            return;
        }

        var findingsText = result.MeterReading.HasValue
            ? $"OSD {result.MeterReading.Value:F2}m \u2013 {validFindings.Count} Befund(e)"
            : $"{validFindings.Count} Befund(e)";
        SetCodingAiState(findingsText, Color.FromRgb(0x22, 0xC5, 0x5E), "Schritt 3 von 3: Overlay und Events");
        CodingFindingsList.ItemsSource = validFindings
            .Select(f => new AiFindingDisplayItem(f)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Befunde als visuelle Overlays direkt auf dem Videobild anzeigen
        if (validFindings.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(validFindings, _player.Time / 1000.0);
        }

        // Pausenmodus: Video pausieren wenn Befunde erkannt
        if (BtnCodingPauseMode.IsChecked == true && validFindings.Count > 0)
        {
            _player?.SetPause(true);
            SetCodingAiState(
                $"{validFindings.Count} Befunde — pausiert zum Pruefen",
                Color.FromRgb(0x38, 0xBD, 0xF8),
                "Delete = Befund loeschen | Leertaste = weiter");
        }
    }
    // === Sub-H: Frame-Readiness + OSD-Meter-Reader ===

    // --- Dateneinblendung-Erkennung: Zustandsautomat ---
    //
    // WaitingForVideo: Dateneinblendung wird vermutet, Analyse blockiert.
    // Warmup:          Erster Meter gesehen, warte auf Bestaetigung (2. Frame).
    // Ready:           Analyse freigeschaltet, kein weiteres Gating.
    //
    private enum FrameReadiness { WaitingForVideo, Warmup, Ready }
    private FrameReadiness _codingFrameState = FrameReadiness.WaitingForVideo;
    private int _codingOsdSkippedFrames;
    private int _codingMeterConfirmCount;

    // Warmup-Puffer: Ergebnis aus der Warmup-Phase wird zwischengespeichert
    // und nach Transition zu Ready nachtraeglich verarbeitet.
    private LiveDetection? _pendingWarmupResult;

    /// <summary>Setzt den Einblendungs-Zustand zurueck (bei Eintritt/Austritt Codier-Modus).</summary>
    private void ResetFrameReadiness()
    {
        _codingFrameState = FrameReadiness.WaitingForVideo;
        _codingOsdSkippedFrames = 0;
        _codingMeterConfirmCount = 0;
        _codingLastOsdMeter = null; // Stale Meter aus vorheriger Session verhindern
        _pendingWarmupResult = null;
    }

    /// <summary>
    /// Reine Bewertung: Ist der aktuelle Frame bereit fuer die Analyse?
    /// Aendert KEINEN Zustand — dafuer ist UpdateFrameReadiness zustaendig.
    /// </summary>
    private bool IsFrameReady() => _codingFrameState == FrameReadiness.Ready;

    /// <summary>
    /// Aktualisiert den Einblendungs-Zustand anhand des aktuellen Analyse-Ergebnisses.
    /// Muss VOR IsFrameReady aufgerufen werden.
    ///
    /// Uebergaenge:
    ///   WaitingForVideo → Warmup:  erster Frame mit Meterstand (aus aktuellem result)
    ///   WaitingForVideo → Ready:   3 Frames ohne Meter (kein OSD vorhanden)
    ///   Warmup          → Ready:   2. Frame mit Meterstand (Bestaetigung)
    ///   Warmup          → Ready:   2 Frames in Warmup ohne zweiten Meter (Fallback gegen Deadlock)
    /// </summary>
    private void UpdateFrameReadiness(LiveDetection result)
    {
        if (_codingFrameState == FrameReadiness.Ready)
            return;

        // NUR den aktuellen Frame-Meter verwenden, NICHT den gecachten _codingLastOsdMeter.
        // Sonst kann ein stale Wert aus vorheriger Navigation die Sperre umgehen.
        bool hasMeterThisFrame = result.MeterReading.HasValue;

        switch (_codingFrameState)
        {
            case FrameReadiness.WaitingForVideo:
                if (hasMeterThisFrame)
                {
                    // Erster Meter gesehen → Warmup (noch nicht sofort freischalten)
                    _codingFrameState = FrameReadiness.Warmup;
                    _codingMeterConfirmCount = 1;
                    _codingOsdSkippedFrames = 0; // Zaehler fuer Warmup-Fallback neu starten
                }
                else
                {
                    // Kein Meter → zaehlen. Nach 3 Frames: kein OSD vorhanden.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 3)
                        _codingFrameState = FrameReadiness.Ready;
                }
                break;

            case FrameReadiness.Warmup:
                if (hasMeterThisFrame)
                    _codingMeterConfirmCount++;

                // 2 Frames mit Meter → sofort Ready (stabiler Uebergang)
                if (_codingMeterConfirmCount >= 2)
                {
                    _codingMeterConfirmCount = 0;
                    _codingFrameState = FrameReadiness.Ready;
                }
                else
                {
                    // Fallback: nach 2 Frames in Warmup (auch ohne zweiten Meter) → Ready.
                    // Verhindert Deadlock bei OCR-Aussetzern nach erstem Meter.
                    _codingOsdSkippedFrames++;
                    if (_codingOsdSkippedFrames >= 2)
                    {
                        _codingMeterConfirmCount = 0;
                        _codingFrameState = FrameReadiness.Ready;
                    }
                }
                break;
        }
    }

    // --- OSD Meter automatisch lesen beim Navigieren ---

    private double? _codingLastOsdMeter;

    /// <summary>
    /// Liest den OSD-Meterstand vom aktuellen Video-Frame (async, via KI).
    /// Wird bei Codier-Navigation und bei Event-Erstellung aufgerufen.
    /// </summary>
    // OSD-Prompt: NUR Meterstand lesen, keine Analyse (schneller, praeziser)
    private static readonly string OsdMeterPrompt = """
        Kanalinspektion OSD (On-Screen-Display).
        Lies NUR die Meterzahl UNTEN RECHTS im Bild.
        Das ist eine Dezimalzahl wie "0.00", "7.90", "14.98" - die gefahrene Distanz.
        IGNORIERE alle Zahlen im oberen Headertext (Knotennummern wie 74468, 80622 etc.).
        IGNORIERE Datumsangaben und andere Texte.
        Antworte NUR mit der Zahl, z.B.: 7.90
        Falls kein Meterstand lesbar: 0.00
        """;

    private async Task<double?> CodingReadOsdMeterAsync()
    {
        if (_codingLiveDetection == null) return null;

        try
        {
            var tmpDir = Path.GetTempPath();
            var snapFile = Path.Combine(tmpDir, $"sewerstudio_osd_{Guid.NewGuid():N}.png");
            byte[]? pngBytes = null;

            try
            {
                TakeSnapshotSafe(snapFile);
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    if (File.Exists(snapFile) && new FileInfo(snapFile).Length > 100)
                        break;
                }
                if (File.Exists(snapFile))
                    pngBytes = await File.ReadAllBytesAsync(snapFile);
            }
            finally
            {
                try { if (File.Exists(snapFile)) File.Delete(snapFile); } catch { }
            }

            if (pngBytes == null || pngBytes.Length == 0) return null;

            // Leichtgewichtiger OSD-Request: nur Meterstand, keine volle Analyse
            var config = AiRuntimeConfig.Load();
            var client = config.CreateOllamaClient();
            var b64 = Convert.ToBase64String(pngBytes);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", OsdMeterPrompt, new[] { b64 })
            };
            var raw = await client.ChatAsync(config.VisionModel, messages, cts.Token);

            // Parse: nur eine Zahl erwartet
            var meterText = raw?.Trim().Replace(",", ".");
            if (!string.IsNullOrWhiteSpace(meterText))
            {
                // Zahl extrahieren (erste Dezimalzahl im Text)
                var match = System.Text.RegularExpressions.Regex.Match(
                    meterText, @"(\d{1,3}(?:\.\d{1,2})?)");
                if (match.Success && double.TryParse(match.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var meter))
                {
                    // Plausibilitaet: 0-500m (Knotennummern sind 5+ stellig)
                    if (meter >= 0 && meter <= 500)
                    {
                        _codingLastOsdMeter = meter;
                        OsdMeterBadge.Visibility = Visibility.Visible;
                        TxtOsdMeter.Text = $"{meter:F2}m (OSD)";
                        return meter;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}