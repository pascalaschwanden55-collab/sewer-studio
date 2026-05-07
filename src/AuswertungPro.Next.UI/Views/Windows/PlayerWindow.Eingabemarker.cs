using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Eingabemarker-Workflow extrahiert aus PlayerWindow.xaml.cs.
//
// Eingabemarker = manuelle BBox-Eingabe mit Code-Auswahl per ComboBox.
// Enthaelt Click-Handler (Open), Cancel, Canvas-Mouse-Handler (Down/Move/Up
// zum Aufziehen der BBox), ComboBox-Handler (KeyDown/SelectionChanged) und
// SubmitEingabemarker (Frame + YOLO + TeacherAnnotation persistieren).
public partial class PlayerWindow
{
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    // ═══════════════════════════════════════════════
    // Eingabemarker: Klick → Stichwort → KI
    // ═══════════════════════════════════════════════

    /// <summary>Eingabemarker Button: Video pausieren, Rechteck-Zeichenmodus aktivieren.</summary>
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
    {
        if (BtnEingabemarker.IsChecked == true)
        {
            // Aktivieren: Video pausieren, CodingOverlayPopup oeffnen (VLC Airspace)
            _player.SetPause(true);
            _eingabemarkerPhase = EingabemarkerPhase.Drawing;
            EnsureMarkOverlayReady();
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Cross;
            SetCodingAiState("Eingabemarker: Rechteck um die Beobachtung ziehen",
                Color.FromRgb(0x3B, 0x82, 0xF6), "Klicken + Ziehen = Bereich markieren");
        }
        else
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Eingabemarker abbrechen und Zustand zuruecksetzen.</summary>
    private void CancelEingabemarker()
    {
        _eingabemarkerPhase = EingabemarkerPhase.Inactive;
        BtnEingabemarker.IsChecked = false;
        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        if (_eingabemarkerPreviewRect != null)
        {
            CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect);
            _eingabemarkerPreviewRect = null;
        }
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    /// <summary>MouseDown auf CodingOverlayCanvas im Eingabemarker-Drawing-Modus: Drag starten.</summary>
    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;

        _eingabemarkerDragStart = canvasPos;
        CodingOverlayCanvas.CaptureMouse();

        // Vorschau-Rechteck erstellen
        _eingabemarkerPreviewRect = new System.Windows.Shapes.Rectangle
        {
            Stroke = System.Windows.Media.Brushes.Lime,
            StrokeThickness = 2,
            StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
            Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(_eingabemarkerPreviewRect, canvasPos.X);
        Canvas.SetTop(_eingabemarkerPreviewRect, canvasPos.Y);
        _eingabemarkerPreviewRect.Width = 0;
        _eingabemarkerPreviewRect.Height = 0;
        CodingOverlayCanvas.Children.Add(_eingabemarkerPreviewRect);
    }

    /// <summary>MouseMove waehrend Eingabemarker Rechteck-Drag: Vorschau aktualisieren.</summary>
    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing || _eingabemarkerPreviewRect == null) return;

        double x = Math.Min(_eingabemarkerDragStart.X, canvasPos.X);
        double y = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y);
        double w = Math.Abs(canvasPos.X - _eingabemarkerDragStart.X);
        double h = Math.Abs(canvasPos.Y - _eingabemarkerDragStart.Y);

        Canvas.SetLeft(_eingabemarkerPreviewRect, x);
        Canvas.SetTop(_eingabemarkerPreviewRect, y);
        _eingabemarkerPreviewRect.Width = w;
        _eingabemarkerPreviewRect.Height = h;
    }

    /// <summary>MouseUp: Rechteck finalisieren → Phase wechseln → Popup anzeigen.</summary>
    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;
        CodingOverlayCanvas.ReleaseMouseCapture();

        double canvasW = CodingOverlayCanvas.ActualWidth;
        double canvasH = CodingOverlayCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0) { CancelEingabemarker(); return; }

        // Normiertes Rechteck berechnen
        double x1 = Math.Min(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y1 = Math.Min(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;
        double x2 = Math.Max(_eingabemarkerDragStart.X, canvasPos.X) / canvasW;
        double y2 = Math.Max(_eingabemarkerDragStart.Y, canvasPos.Y) / canvasH;

        // Mindestgroesse pruefen
        if ((x2 - x1) < 0.02 || (y2 - y1) < 0.02) { CancelEingabemarker(); return; }

        _eingabemarkerRectNorm = new Rect(x1, y1, x2 - x1, y2 - y1);

        // Phase wechseln: KEINE Canvas-Klicks mehr → Popup sicher bedienbar
        _eingabemarkerPhase = EingabemarkerPhase.Input;
        CodingOverlayCanvas.IsHitTestVisible = false; // Canvas ignoriert jetzt Klicks
        CodingOverlayCanvas.Cursor = System.Windows.Input.Cursors.Arrow;

        // Popup in der Toolbar anzeigen (kein VLC Airspace Problem)
        EingabemarkerPopup.Visibility = Visibility.Visible;

        // Freitext-Feld fokussieren
        TxtEingabemarker.Text = "";
        CmbEingabemarker.SelectedIndex = -1;
        Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

        SetCodingAiState("Beschreibung eingeben oder Stichwort waehlen, dann Enter",
            Color.FromRgb(0x3B, 0x82, 0xF6), "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

    /// <summary>Enter in der Stichwort-ComboBox → KI-Analyse starten.</summary>
    private void CmbEingabemarker_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter) return;
        SubmitEingabemarker();
    }

    /// <summary>Auswahl in der Schnellauswahl-ComboBox → Text uebernehmen und absenden.</summary>
    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nur wenn Popup sichtbar und etwas ausgewaehlt wurde
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker();
        }
    }

    /// <summary>Freitext oder Stichwort absenden → Code ableiten oder KI-Analyse starten.</summary>
    private async void SubmitEingabemarker()
    {
        string keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        // VSA-Hauptcode ableiten: Exakte Stichwörter ODER Freitext-Heuristik
        // Freitext wie "beule unten", "riss bei 3 uhr" wird durch InferCodeFromLabel erkannt
        string? codeHint = keyword.ToUpperInvariant() switch
        {
            "ROHRANFANG" => "BCD",
            "ROHRENDE" => "BCE",
            "ANSCHLUSS" => "BCA",
            "BOGEN" => "BCC",
            "RISS" => "BAB",
            "BRUCH" => "BAC",
            "VERFORMUNG" => "BAA",
            "OBERFLAECHENSCHADEN" => "BAF",
            "VERSATZ" or "VERSCHIEBUNG" => "BAJ",
            "WURZELN" or "BEWUCHS" => "BBA",
            "ABLAGERUNG" => "BBC",
            "INKRUSTATION" => "BBB",
            "WASSERSTAND" => "BDD",
            "ABBRUCH" => "BDC",
            // Kein exaktes Stichwort → Freitext-Heuristik (z.B. "beule unten", "riss bei 3 uhr")
            _ => AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(keyword)
        };

        try
        {
            // Duplikat-Check VOR der Analyse
            if (_codingVm != null && codeHint != null)
            {
                double checkMeter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                // BCD/BCE/BDC: Einmal-Codes — Meter egal
                bool isEinmalCode = codeHint is "BCD" or "BCE" or "BDC";
                var existingDup = _codingVm.Events.FirstOrDefault(e =>
                    CodesMatchForDedup(e.Entry.Code, codeHint) &&
                    (isEinmalCode || Math.Abs(e.MeterAtCapture - checkMeter) < 1.0));
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m — Duplikat",
                        Color.FromRgb(0xF5, 0x9E, 0x0B), "");
                    return;
                }
            }

            // Bekannter Hauptcode → Event SOFORT erzeugen (kein Warten auf Qwen)
            if (codeHint != null && _codingVm != null && _codingSessionService != null)
            {
                double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var entry = new ProtocolEntry
                {
                    Source = ProtocolEntrySource.Ai,
                    Code = codeHint,
                    Beschreibung = label,
                    MeterStart = meter,
                    Zeit = videoTime
                };

                // Foto vom aktuellen Frame
                var fotoPath = CodingCaptureSnapshot(entry);
                if (fotoPath != null) entry.FotoPaths.Add(fotoPath);

                var ev = _codingSessionService.AddEvent(entry, _codingVm.CurrentOverlay);
                ev.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = codeHint,
                    Confidence = 1.0,
                    Reason = $"Eingabemarker: {keyword}",
                    Decision = CodingUserDecision.Accepted
                };
                // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
                // KEIN explizites _codingVm.Events.Add() — sonst doppelt!
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev);
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "");
            }
            else
            {
                // Kein Hauptcode erkannt → Qwen analysieren lassen
                SetCodingAiState($"KI analysiert: \"{keyword}\" ...",
                    Color.FromRgb(0xF5, 0x9E, 0x0B), "Qwen analysiert");
                await RunCodingAnalysisAsync(
                    $"Eingabemarker: {keyword}",
                    disableAnalyzeButton: true,
                    keywordHint: keyword,
                    codeHint: null);
            }
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44), "");
        }
        finally
        {
            CancelEingabemarker();
        }
    }

    /// <summary>Detection-Overlays aufraumen (Boxen, Labels, Findings-Liste).</summary>
    // Phase 6.1.E: ClearDetectionOverlays nach PlayerWindow.LiveDetection.cs migriert.


    /// <summary>
    /// <summary>
    /// Sammelt alle Import-Eintraege als Erwartungshorizont fuer die KI-Analyse.
    /// Die KI erhaelt die bekannten VSA-Codes und kann sie zuweisen statt "???".
    /// </summary>
    // ── Multi-Model Rendering (YOLO → DINO → SAM) ────────────────────

    /// <summary>
    /// Rendert Multi-Model Ergebnisse: SAM-Masken (gruene Konturen) + Label-Badges mit Messungen.
    /// </summary>
    /// <summary>Aktuelles Multi-Model-Ergebnis fuer Klick-Interaktion.</summary>
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameResult? _currentMmResult;
    /// <summary>Ferne Detektionen (innerhalb Rohrkreis) — grau als Vorschau angezeigt.</summary>
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.SingleFrameResult? _previewMmResult;

    // Phase 6.1.F Sub-G: ShowMultiModelResults + AddMultiModelFindingsAsEvents + ShowCodingAiResults nach PlayerWindow.CodingMode.cs migriert.

    /// <summary>Aktuell selektierte Maske (Klick auf Overlay).</summary>
    private int _selectedMaskIndex = -1;

    /// <summary>
    /// Sperrliste: vom Benutzer abgelehnte Befunde (Code + Meter-Bereich).
    /// Verhindert dass die Auto-Analyse denselben Befund erneut einfuegt.
    /// Wird pro Session gefuehrt, Reset bei neuem Video.
    /// </summary>
    private readonly HashSet<string> _rejectedFindings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Erzeugt einen Sperrlisten-Key: Code + gerundeter Meterstand (±0.5m Toleranz).</summary>
    // Phase 6.1.A: MakeRejectionKey nach PlayerWindow.Helpers.cs migriert.

}
