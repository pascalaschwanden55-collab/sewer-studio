using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Mask-Triage + Pause-Mode + Live-AI extrahiert aus PlayerWindow.xaml.cs.
//
// Mask-Triage (Pausenmodus): User akzeptiert/verwirft SAM-Masken im
// pausierten Video.
//   - OnMaskOverlayClicked: Maske selektieren / cyclen
//   - HighlightSelectedMask / SyncBefundListeToMask / EnlargeListItem
//   - OnMaskOverlayDeleted / DeleteMaskAtIndex / RemoveMatchingCodingEvent
//   - AcceptMaskAtIndex
//   - CodingPauseMode_Click + CodingLiveAi_Click + CodingLiveAiTimer_Tick
public partial class PlayerWindow
{
    private void OnMaskOverlayClicked(int maskIndex)
    {
        // Wenn gleiche Maske nochmal geklickt → zur naechsten wechseln (Cycle)
        if (maskIndex == _selectedMaskIndex)
        {
            maskIndex = FindNextVisibleMaskIndex(maskIndex);
            if (maskIndex < 0) return;
        }

        _selectedMaskIndex = maskIndex;

        // Visuelle Hervorhebung: selektierte Maske dicker, andere normal
        HighlightSelectedMask(maskIndex);

        if (_currentMmResult?.QuantifiedMasks is { } masks && maskIndex < masks.Count)
        {
            var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(masks[maskIndex].Label);
            SetCodingAiState(
                $"Befund {maskIndex + 1}/{masks.Count}: {vsaCode ?? masks[maskIndex].Label}",
                Color.FromRgb(0x38, 0xBD, 0xF8),
                "Delete = verwerfen | O = OK | Leertaste = weiter");

            // Befundliste synchronisieren: passenden Eintrag selektieren
            SyncBefundListeToMask(maskIndex, vsaCode);
        }
    }

    /// <summary>Findet die naechste sichtbare Maske nach dem gegebenen Index (Cycle).</summary>
    private int FindNextVisibleMaskIndex(int afterIndex)
    {
        int total = _currentMmResult?.QuantifiedMasks.Count ?? 0;
        for (int offset = 1; offset < total; offset++)
        {
            int candidate = (afterIndex + offset) % total;
            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{candidate}";
            if (CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(e => tag.Equals(e.Tag as string)))
                return candidate;
        }
        return afterIndex; // Nur eine Maske uebrig
    }

    /// <summary>Selektierte Maske visuell hervorheben (dickere Kontur, Blink-Animation, andere gedimmt).</summary>
    private void HighlightSelectedMask(int selectedIndex)
    {
        var selectedTag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{selectedIndex}";

        foreach (var el in CodingOverlayCanvas.Children.OfType<System.Windows.Shapes.Path>())
        {
            if (el.Tag is not string tag || !tag.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag))
                continue;

            bool isSelected = tag == selectedTag;

            if (el.Stroke is not null)
            {
                // Kontur-Path
                el.StrokeThickness = isSelected ? 5 : 2;
                el.Opacity = isSelected ? 1.0 : 0.4;
            }
            else
            {
                // Fill-Path
                el.Opacity = isSelected ? 1.0 : 0.2;
            }

            // Blink-Animation auf selektierter Maske
            el.BeginAnimation(UIElement.OpacityProperty, null); // Alte Animation stoppen
            if (isSelected)
            {
                var blink = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.0,
                    To = 0.3,
                    Duration = TimeSpan.FromMilliseconds(300),
                    AutoReverse = true,
                    RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2)
                };
                el.BeginAnimation(UIElement.OpacityProperty, blink);
            }
        }
    }

    /// <summary>Synchronisiert die Befundliste (LstCodingEvents) mit der selektierten Maske — mit Flash-Animation.</summary>
    private void SyncBefundListeToMask(int maskIndex, string? vsaCode)
    {
        if (LstCodingEvents.Items.Count == 0 || string.IsNullOrEmpty(vsaCode)) return;

        // Suche den Event in der Liste der zum Maske-Code passt
        for (int i = LstCodingEvents.Items.Count - 1; i >= 0; i--)
        {
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;
            if (string.Equals(ev.Entry.Code, vsaCode, StringComparison.OrdinalIgnoreCase)
                || (ev.Entry.Code?.StartsWith(vsaCode, StringComparison.OrdinalIgnoreCase) == true))
            {
                _enlargeSuppressShrink = true;
                LstCodingEvents.SelectedIndex = i;
                LstCodingEvents.ScrollIntoView(LstCodingEvents.Items[i]);
                _enlargeSuppressShrink = false;

                // Ballon-Effekt: vergroessert bis abgehandelt oder anderes Event gewaehlt
                EnlargeListItem(i);
                return;
            }
        }
    }

    /// <summary>Aktuell vergroessertes ListBox-Item (bleibt gross bis abgehandelt oder anderes gewaehlt).</summary>
    private System.Windows.Controls.ListBoxItem? _enlargedListItem;
    /// <summary>Unterdrueckt Shrink in SelectionChanged wenn Maske die Selektion steuert.</summary>
    private bool _enlargeSuppressShrink;

    /// <summary>Vergroessert ein ListBox-Item persistent (Ballon-Effekt) + blauer Hintergrund.</summary>
    private void EnlargeListItem(int index)
    {
        // Vorheriges zuruecksetzen
        ShrinkEnlargedListItem();

        if (index < 0 || index >= LstCodingEvents.Items.Count) return;

        // Container holen — ggf. erst nach ScrollIntoView verfuegbar
        LstCodingEvents.UpdateLayout();
        var container = LstCodingEvents.ItemContainerGenerator
            .ContainerFromIndex(index) as System.Windows.Controls.ListBoxItem;
        if (container == null) return;

        _enlargedListItem = container;

        // Hintergrund blau — deutlich sichtbar
        container.Background = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
        container.FontWeight = System.Windows.FontWeights.Bold;

        // Vergroessern mit Animation: 1.0 → 1.18 (deutlich sichtbar)
        container.RenderTransformOrigin = new System.Windows.Point(0.0, 0.5); // Links verankert
        container.RenderTransform = new ScaleTransform(1.0, 1.0);
        var grow = new System.Windows.Media.Animation.DoubleAnimation
        {
            To = 1.18,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        ((ScaleTransform)container.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ((ScaleTransform)container.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>Setzt das vergroesserte ListBox-Item auf Normalgroesse zurueck.</summary>

    /// <summary>Delete auf Maske (via Maus-Callback) — weiterleiten an zentrale Methode.</summary>
    private void OnMaskOverlayDeleted(int maskIndex)
    {
        DeleteMaskAtIndex(maskIndex);
    }

    /// <summary>
    /// Verwirft eine Maske (Delete-Taste). Identische Funktion wie Ablehnen in der Befundliste:
    /// Sperrliste, Event entfernen, SAM-Maske entfernen, Negativ-Feedback, ggf. Video weiter.
    /// </summary>
    private void DeleteMaskAtIndex(int maskIndex)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks || maskIndex >= masks.Count) return;

        var quant = masks[maskIndex];
        var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
        var meter = _codingVm?.CurrentMeter ?? 0;

        // Auf Sperrliste setzen → wird nicht mehr erneut eingefuegt
        _rejectedFindings.Add(MakeRejectionKey(vsaCode, meter));

        // Zugehoeriges CodingEvent entfernen (gleicher Pfad wie Ablehnen in Befundliste)
        RemoveMatchingCodingEvent(vsaCode, meter);
        if (_codingVm != null)
        {
            _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        }

        // SAM-Maske visuell entfernen
        Ai.Pipeline.SamMaskRenderer.RemoveMaskGroup(
            CodingOverlayCanvas, $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{maskIndex}");

        // Negativ-Feedback speichern
        Task.Run(() => SaveNegativeFeedbackAsync(quant.Label, vsaCode, meter))
            .SafeFireAndForget("NegativeFeedbackMask");

        _selectedMaskIndex = -1;
        RefreshCodingEventsList();

        // Wenn keine Masken mehr sichtbar → Video weiter
        if (!HasVisibleMasks())
            ResumeAfterPause();
    }

    /// <summary>Entfernt das CodingEvent das zum geloeschten Overlay gehoert.</summary>
    private void RemoveMatchingCodingEvent(string? vsaCode, double meter)
    {
        if (_codingVm == null || string.IsNullOrEmpty(vsaCode)) return;

        // Neueste Events zuerst (rueckwaerts suchen)
        for (int i = _codingVm.Events.Count - 1; i >= 0; i--)
        {
            var ev = _codingVm.Events[i];
            if (CodesMatchForDedup(ev.Entry.Code, vsaCode)
                && Math.Abs((ev.MeterAtCapture) - meter) < 1.0)
            {
                _codingVm.RemoveEvent(ev);
                _codingSessionService?.ActiveSession?.Events.Remove(ev);
                System.Diagnostics.Debug.WriteLine(
                    $"[Sperrliste] CodingEvent entfernt: {ev.Entry.Code} @ {ev.MeterAtCapture:F1}m");
                break;
            }
        }
    }

    /// <summary>
    /// Akzeptiert eine Maske (O-Taste). Identische Funktion wie Akzeptieren in der Befundliste:
    /// Decision=Accepted, Sperrliste, SAM-Maske entfernen, Positiv-Feedback, ggf. Video weiter.
    /// </summary>
    private void AcceptMaskAtIndex(int maskIndex)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks || maskIndex >= masks.Count) return;

        var quant = masks[maskIndex];
        var vsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(quant.Label);
        var meter = _codingVm?.CurrentMeter ?? 0;

        // Zugehoeriges CodingEvent finden und ueber ViewModel akzeptieren (gleicher Pfad wie Liste)
        if (_codingVm != null && !string.IsNullOrEmpty(vsaCode))
        {
            var matchingEvent = _codingVm.Events.FirstOrDefault(e =>
                CodesMatchForDedup(e.Entry.Code, vsaCode)
                && Math.Abs(e.MeterAtCapture - meter) < 1.0);
            if (matchingEvent != null)
            {
                _codingVm.SelectedDefect = matchingEvent;
                _codingVm.AcceptDefectCommand.Execute(null);
            }
        }

        // Auf Sperrliste setzen → wird bei naechster Analyse nicht erneut erkannt
        _rejectedFindings.Add(MakeRejectionKey(vsaCode, meter));

        // SAM-Maske visuell entfernen
        Ai.Pipeline.SamMaskRenderer.RemoveMaskGroup(
            CodingOverlayCanvas, $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{maskIndex}");

        // Positiv-Feedback speichern
        Task.Run(() => SavePositiveFeedbackAsync(quant.Label, vsaCode, meter))
            .SafeFireAndForget("PositiveFeedbackMask");

        _selectedMaskIndex = -1;
        RefreshCodingEventsList();

        // Wenn keine Masken mehr sichtbar → Video weiter
        if (!HasVisibleMasks())
            ResumeAfterPause();
    }

    // Phase 6.1.F Sub-K: SAM-Mask-Helper + IsInsideDetectionZone + IsKunststoffRohr + HasNearbyStructuralDamage + ResumeAfterPause + GatherImportContext + IsAlreadyCovered + IsSamePosition + CodesMatchForDedup nach PlayerWindow.CodingMode.cs migriert.

    // Phase 6.1.B: Feedback-Loop (Cluster B4) nach PlayerWindow.Feedback.cs migriert.
    // Enthaelt: _feedbackHttpClient, _positiveFeedbackLock, _negativeFeedbackLock,
    // CreateFeedbackService, ResolveFeedbackCode, BuildFeedbackMappedEntry,
    // IngestFeedbackAsync, SavePositiveFeedbackAsync, SaveNegativeFeedbackAsync.

    /// <summary>
    /// Erstellt CodingEvents aus Multi-Model Befunden (DINO-Detections + SAM-Quantifizierung).
    /// </summary>
    /// <summary>
    /// Multi-Model Findings als CodingEvents — nutzt denselben Resolver-
    /// und Label-Pfad wie der Qwen/Enhanced-Pfad (ResolveFindingCodeForCoding, LookupVsaLabel).
    /// </summary>


    /// <summary>
    /// Filtert KI-Findings: VSA-Code-Validierung, BCD/BCE-Ausschluss, Deduplizierung.
    /// Die gefilterte Liste wird fuer UI, Overlays und Event-Erstellung verwendet.
    /// Deduplizierung: code + BBox-Mittelpunkt (verschiedene Positionen = verschiedene Befunde).
    /// </summary>
    // Phase 6.1.F Sub-I: FilterValidFindings + ResolveFindingCodeForCoding + RefineGenericCodeFromImport + TryResolveImportFallbackCode + AddAiFindingsAsEvents nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>
    /// Erlaubte Code-Familien fuer Import-Fallback.
    /// Umfasst Bestandsaufnahme (BC), Strukturschaeden (BA) und Betriebliche Stoerungen (BB).
    /// </summary>
    // Phase 6.1.A: IsAllowedImportFallbackCode nach PlayerWindow.Helpers.cs migriert.


    private void CodingPauseMode_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingPauseMode.IsChecked == true)
        {
            // Pausenmodus aktivieren — setzt auch Auto-Analyse an falls nicht schon aktiv
            if (BtnCodingLiveAi.IsChecked != true)
            {
                BtnCodingLiveAi.IsChecked = true;
                CodingLiveAi_Click(BtnCodingLiveAi, new RoutedEventArgs());
            }
            BtnCodingPauseMode.Background = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
            SetCodingAiState("KI-Analyse mit Pause aktiv", Color.FromRgb(0x38, 0xBD, 0xF8),
                "Video pausiert bei jedem Befund — Delete = loeschen, Leertaste = weiter");
        }
        else
        {
            BtnCodingPauseMode.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            SetCodingAiState("Pausenmodus deaktiviert", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }

    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingLiveAi.IsChecked == true)
        {
            // 8s Intervall: Qwen braucht ~3s Inferenz + 1s Capture + Puffer
            _codingLiveAiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _codingLiveAiTimer.Tick += CodingLiveAiTimer_Tick;
            _codingLiveAiTimer.Start();

            // Gruen blinken wenn aktiv
            _codingLiveAiBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _codingLiveAiBlinkTimer.Tick += (_, _) =>
            {
                _codingLiveAiBlinkState = !_codingLiveAiBlinkState;
                BtnCodingLiveAi.Background = new SolidColorBrush(
                    _codingLiveAiBlinkState
                        ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
                        : Color.FromRgb(0x16, 0x65, 0x34));  // Dunkelgruen
            };
            _codingLiveAiBlinkTimer.Start();
            BtnCodingLiveAi.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {CompactModelName(_codingAiModelName)}");
        }
        else
        {
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;

            // Blinken stoppen, Standardfarbe zuruecksetzen
            _codingLiveAiBlinkTimer?.Stop();
            _codingLiveAiBlinkTimer = null;
            BtnCodingLiveAi.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

            SetCodingAiState("Kuenstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(_codingAiModelName)}");
        }
    }

    private async void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
    {
        // 2026-04-26: Window-Lifecycle-Guard. async-void darf bei IsClosed
        // nicht weiterlaufen — sonst greift RunCodingAnalysisAsync auf
        // disposed _player/_codingVisionClient zu (App-Crash).
        if (_isWindowClosed) return;
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            // Mindestens ein Analyse-Service muss verfuegbar sein
            if (_codingEnhancedVision == null && _codingLiveDetection == null) return;
            if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput) return;

            // Nur analysieren wenn Video tatsaechlich laeuft
            if (_player == null || !_player.IsPlaying) return;

            await RunCodingAnalysisAsync("Automatische KI-Analyse: Analysiere...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] CodingLiveAiTimer_Tick error: {ex.Message}");
        }
    }
}
