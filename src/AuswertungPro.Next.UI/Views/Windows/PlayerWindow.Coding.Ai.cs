using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- Coding KI-Analyse ---

    private async Task InitCodingAi()
    {
        try
        {
            var platformConfig = new AppSettingsAiSettingsProvider().Load();
            var config = platformConfig.ToRuntimeSettings();
            _codingPipelineConfig = App.Services is ServiceProvider sp
                ? sp.PipelineCfg
                : platformConfig.ToPipelineConfig();
            _codingAiModelName = config.VisionModel;
            if (!config.Enabled)
            {
                SetCodingAiState("Künstliche Intelligenz deaktiviert", Color.FromRgb(0x94, 0xA3, 0xB8), "Modell: aus");
                BtnCodingAnalyze.IsEnabled = false;
                return;
            }

            var client = new OllamaClient(
                config.OllamaBaseUri,
                ownedTimeout: config.OllamaRequestTimeout,
                keepAlive: config.OllamaKeepAlive,
                numCtx: config.OllamaNumCtx);
            _codingLiveDetection = new LiveDetectionService(client, config.VisionModel);
            _codingEnhancedVision = new EnhancedVisionAnalysisService(client, config.VisionModel, CodeCatalog);
            // Bewusst Default-Gewichte (statisch). Gelernte Gewichte werden NICHT geladen (siehe ADR-008).
            _codingQualityGate = new QualityGateService();

            // Multi-Model Pipeline (YOLO â†’ DINO â†’ SAM) initialisieren
            try
            {
                _codingVisionClient = new VisionPipelineClient(
                    _codingPipelineConfig.SidecarUrl,
                    sidecarToken: _codingPipelineConfig.SidecarToken);
                _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);
                _codingBoxSegmentation = new MarkBoxSegmentationService(_codingVisionClient.SegmentSamAsync);
                _codingAiEnabled = true;

                // Kontrollsicherung: Monitor pollt laufend und haelt den Modus aktuell
                // (behebt die fruehere Timing-Falle des einmaligen Health-Checks).
                _codingHealthMonitor = new PipelineHealthMonitor(
                    _codingVisionClient,
                    aiEnabled: () => _codingAiEnabled,
                    qwenAvailable: () => _codingLiveDetection != null || _codingEnhancedVision != null);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                // Sofort einmal auswerten, damit die Anzeige nicht leer startet.
                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            catch (Exception ex)
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Künstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"Monitor-Fehler: {ex.Message}");
            }
            SetYoloStatus("Bereit", Color.FromRgb(0x22, 0xC5, 0x5E), LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName));
        }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
            BtnCodingAnalyze.IsEnabled = false;
        }
    }

    // -- Pipeline-Kontrollsicherung: Live-Status + Auto-Recovery --------------

    private void OnPipelineHealthChanged(object? sender, AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        // Der Aufruf kommt aus dem Monitor-Loop (ThreadPool-Thread). Nach Window-Close
        // oder Verlassen des Codiermodus duerfen keine UI-Controls mehr angefasst werden.
        if (_closing || Dispatcher.HasShutdownStarted)
            return;

        if (!Dispatcher.CheckAccess())
        {
            // Nicht-blockierend marshallen; im UI-Thread Zustand erneut pruefen.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_closing && _isCodingMode && _codingHealthMonitor != null)
                    ApplyPipelineHealth(status);
            }));
            return;
        }

        if (_isCodingMode && _codingHealthMonitor != null)
            ApplyPipelineHealth(status);
    }

    /// <summary>
    /// Wendet den Pipeline-Status an: fuehrt den Multi-Model-Modus automatisch nach
    /// (Auto-Recovery) und aktualisiert Ampel + Detailanzeige.
    /// </summary>
    private void ApplyPipelineHealth(AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        _codingUseMultiModel = status.MultiModelActive;
        if (status.MultiModelActive && _codingMultiModel == null && _codingVisionClient != null)
            _codingMultiModel = _codingPipelineConfig is null
                ? new SingleFrameMultiModelService(_codingVisionClient)
                : new SingleFrameMultiModelService(_codingVisionClient, _codingPipelineConfig);

        var uiState = PipelineHealthUiStateFactory.Create(status);
        SetCodingAiState(uiState.Summary, uiState.Color, uiState.Detail);
        BtnCodingAnalyze.IsEnabled = uiState.AnalysisEnabled;
        UpdatePipelineHealthDetails(uiState.Details);
    }

    /// <summary>Aktualisiert die ausklappbare Detailanzeige (Sidecar/Token/Modelle/Modus).</summary>
    private void UpdatePipelineHealthDetails(PipelineHealthDetailsUiState details)
    {
        Hd_Sidecar.Text = details.Sidecar;
        Hd_Token.Text = details.Token;
        Hd_Yolo.Text = details.Yolo;
        Hd_Dino.Text = details.Dino;
        Hd_Sam.Text = details.Sam;
        Hd_Mode.Text = details.Mode;
    }

    /// <summary>Stoppt den Pipeline-Health-Monitor und meldet sich vom Event ab.</summary>
    private void StopPipelineHealthMonitor()
    {
        _codingAiEnabled = false;
        if (_codingHealthMonitor != null)
        {
            _codingHealthMonitor.StatusChanged -= OnPipelineHealthChanged;
            _ = _codingHealthMonitor.StopAsync();
            _codingHealthMonitor = null;
        }
    }

    /// <summary>Alle Overlays/Einblendungen vom Video entfernen.</summary>
    private void CodingClearOverlays_Click(object sender, RoutedEventArgs e)
        => ClearDetectionOverlays();

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Eingabemarker: Klick â†’ Stichwort â†’ KI
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

    /// <summary>MouseUp: Rechteck finalisieren â†’ Phase wechseln â†’ Popup anzeigen.</summary>
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

        // Phase wechseln: KEINE Canvas-Klicks mehr â†’ Popup sicher bedienbar
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

        SetCodingAiState("Beschreibung eingeben oder Stichwort wählen, dann Enter",
            Color.FromRgb(0x3B, 0x82, 0xF6), "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

    /// <summary>Enter in der Stichwort-ComboBox â†’ KI-Analyse starten.</summary>
    private void CmbEingabemarker_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelEingabemarker();
            ClearDetectionOverlays();
            return;
        }

        if (e.Key != System.Windows.Input.Key.Enter) return;
        SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
    }

    /// <summary>Auswahl in der Schnellauswahl-ComboBox â†’ Text uebernehmen und absenden.</summary>
    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nur wenn Popup sichtbar und etwas ausgewaehlt wurde
        if (EingabemarkerPopup.Visibility != Visibility.Visible) return;
        if (CmbEingabemarker.SelectedItem is ComboBoxItem item && item.Content is string text && !string.IsNullOrEmpty(text))
        {
            TxtEingabemarker.Text = text;
            SubmitEingabemarker().SafeFireAndForget("SubmitEingabemarker");
        }
    }

    private static string? ResolveEingabemarkerCodeHint(string? keyword)
        => AuswertungPro.Next.UI.Player.PlayerVsaCodeHintResolver.ResolveKeyword(keyword);

    /// <summary>Freitext oder Stichwort absenden â†’ Code ableiten oder KI-Analyse starten.</summary>
    private async Task SubmitEingabemarker()
    {
        string keyword = TxtEingabemarker.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(keyword)) return;

        EingabemarkerPopup.Visibility = Visibility.Collapsed;
        _eingabemarkerPhase = EingabemarkerPhase.Analyzing;

        // VSA-Hauptcode ableiten: Exakte StichwÃ¶rter ODER Freitext-Heuristik
        // Freitext wie "beule unten", "riss bei 3 uhr" wird durch InferCodeFromLabel erkannt
        string? codeHint = ResolveEingabemarkerCodeHint(keyword);

        try
        {
            // Duplikat-Check VOR der Analyse
            if (_codingVm != null && codeHint != null)
            {
                double checkMeter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var existingDup = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
                    _codingVm.Events,
                    codeHint,
                    checkMeter);
                if (existingDup != null)
                {
                    SetCodingAiState(
                        $"{codeHint} bereits vorhanden bei {existingDup.MeterAtCapture:F2}m â€” Duplikat",
                        Color.FromRgb(0xF5, 0x9E, 0x0B), "");
                    return;
                }
            }

            // Bekannter Hauptcode â†’ Event SOFORT erzeugen (kein Warten auf Qwen)
            if (codeHint != null && _codingVm != null && _codingSessionService != null)
            {
                double meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter;
                var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
                var label = LookupVsaLabel(codeHint) ?? keyword;

                var draft = CodingEingabemarkerEventFactory.CreateAccepted(
                    codeHint,
                    label,
                    keyword,
                    meter,
                    videoTime);

                // Foto vom aktuellen Frame
                var fotoPath = CodingCaptureSnapshot(draft.Entry);
                if (fotoPath != null) draft.Entry.FotoPaths.Add(fotoPath);

                var ev = _codingSessionService.AddEvent(draft.Entry, _codingVm.CurrentOverlay);
                ev.AiContext = draft.AiContext;
                // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
                // KEIN explizites _codingVm.Events.Add() â€” sonst doppelt!
                RefreshCodingEventsList();
                UpdateToolBadge();
                PersistSingleEventAsTrainingSample(ev).SafeFireAndForget("TrainingSaveSingle");
                SetCodingAiState($"{codeHint} {label} bei {meter:F2}m eingetragen",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "");
            }
            else
            {
                // Kein Hauptcode erkannt â†’ Qwen analysieren lassen
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
    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }

    // Analyse-Boxen kurz zeigen, dann nach 3s automatisch ausblenden, damit der Frame nicht
    // zugekleistert wird. WICHTIG: nur die visuellen Boxen entfernen — die Befundliste (KI-BEFUNDE)
    // bleibt stehen (deshalb NICHT ClearDetectionOverlays, das wuerde die Liste mitnehmen).
    private System.Windows.Threading.DispatcherTimer? _detectionAutoHideTimer;

    private void ScheduleDetectionAutoHide()
    {
        if (_detectionAutoHideTimer == null)
        {
            _detectionAutoHideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(3)
            };
            _detectionAutoHideTimer.Tick += (s, e) =>
            {
                _detectionAutoHideTimer!.Stop();
                DetectionCanvas.Children.Clear();
                DetectionOverlayGrid.Visibility = Visibility.Collapsed;
            };
        }
        _detectionAutoHideTimer.Stop();
        _detectionAutoHideTimer.Start();
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
            var currentMeterForStop = ResolveCodingMeterForFrame(captureTimestampSec);
            var currentVideoTimeForStop = TimeSpan.FromSeconds(captureTimestampSec);
            if (IsCodingAfterTerminalBoundary(currentMeterForStop, currentVideoTimeForStop))
            {
                ClearDetectionOverlays();
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                SetCodingAiState("Rohrende erreicht - KI-Analyse gestoppt",
                    Color.FromRgb(0x22, 0xC5, 0x5E), "Codierung abgeschlossen");
                return;
            }

            // â”€â”€ Multi-Model Pfad: YOLO â†’ DINO â†’ SAM â”€â”€
            if (_codingUseMultiModel && _codingMultiModel != null)
            {
                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 1 von 4: Snapshot", pulse: true);

                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }
                _detectionPendingFrameBytes = pngBytes;
                _detectionPendingTimestampSec = captureTimestampSec;
                var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                    pngBytes,
                    captureTimestampSec,
                    _codingAnalysisCts.Token);

                // Dateneinblendungs-Gating (wie im Qwen-Pfad): waehrend der Daten-/Texteinblendung
                // am Videoanfang NICHT codieren. Sonst bekommen fruehe Befunde (BCC, Streckenschaden,
                // BCD) ein Foto vom eingeblendeten Anfangsframe und einen falschen Anfangs-Meter.
                // Setzt zugleich den ersten sauberen Frame auch im
                // Multi-Model-Betrieb -> macht den BCD-Clean-Frame-Schutz hier erst wirksam.
                var readinessProbe = new AuswertungPro.Next.Application.Ai.LiveDetection(
                    captureTimestampSec, System.Array.Empty<AuswertungPro.Next.Application.Ai.LiveFrameFinding>(),
                    frameOsdMeter, null);
                UpdateFrameReadiness(readinessProbe);
                if (!IsFrameReady())
                {
                    SetCodingAiState("Dateneinblendung erkannt - übersprungen",
                        Color.FromRgb(0x94, 0xA3, 0xB8), "Warte auf sauberes Videobild...");
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    "Schritt 2 von 4: YOLO und DINO", pulse: true);

                int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
                var currentMeterForClassifier = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
                var reachLengthForClassifier = _codingVm?.EndMeter > 0
                    ? _codingVm.EndMeter
                    : Math.Max(currentMeterForClassifier, 1);

                var mmResult = await _codingMultiModel.AnalyzeFrameAsync(
                    pngBytes, dn, _codingOverlayService?.Calibration,
                    _codingAnalysisCts.Token,
                    currentMeterForClassifier,
                    reachLengthForClassifier);

                if (mmResult.Error != null)
                {
                    SetCodingAiState($"Fehler: {mmResult.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                        "Multi-Model");
                    return;
                }

                if (TryHandleBoundaryClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
                    return;

                if (TryHandleStructuralClassifierResult(mmResult, captureTimestampSec, frameOsdMeter))
                    return;

                if (!mmResult.IsRelevant || !mmResult.HasDetections)
                {
                    SetCodingAiState("Kein Schaden erkannt", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"YOLO {mmResult.YoloTimeMs:F0}ms | {mmResult.DinoDetections.Count} Detektionen");
                    Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                    return;
                }

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 3 von 4: SAM-Masken ({mmResult.DinoDetections.Count} Befunde)", pulse: true);

                // Naehe-Gate: nur codierbare Befunde metrieren; "Voraus" nur anzeigen.
                var segmented = BuildCodingSegmentedFindings(mmResult);
                int vorausCount = segmented.Count(s => !s.Proximity.IsCodierbar);
                int codierbarCount = segmented.Count - vorausCount;

                // Masken/Overlay rendern (alle; "Voraus" optisch abgesetzt).
                ShowMultiModelResults(mmResult, segmented);

                // Overlay-Policy einmalig anwenden: nur sichtbare codierbare Befunde zaehlen
                // als echte Befunde. Als Hintergrund (Hidden) verworfene Masken werden gemeldet.
                var visibleCodierbar = CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(segmented);
                var suppressedBackgroundCount = segmented.Count(s => s.Proximity.IsCodierbar) - visibleCodierbar.Count;
                var overlaySuppressionText = CodingSegmentedFindingVisibility.BuildOverlaySuppressionText(suppressedBackgroundCount);

                // DINO hatte Detektionen (sonst waeren wir oben raus), aber SAM lieferte keine Maske
                // -> Befund verloren (degraded). Nicht als sauberen Negativbefund (gruen) tarnen.
                if (segmented.Count == 0)
                {
                    SetCodingAiState("SAM ohne Maske - Befund nicht segmentiert",
                        Color.FromRgb(0xF5, 0x9E, 0x0B),
                        mmResult.SamResponse?.Degraded == true
                            ? $"SAM degraded ({mmResult.SamResponse.SkippedBoxes} Box(en) verloren)"
                            : "keine Maske erzeugt");
                    return;
                }

                if (codierbarCount == 0 && vorausCount > 0)
                {
                    SetCodingAiState("Ereignis voraus erkannt - näher heranfahren",
                        Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{vorausCount} voraus");
                    return;
                }

                var timingText = $"YOLO {mmResult.YoloTimeMs:F0}ms | DINO {mmResult.DinoTimeMs:F0}ms | SAM {mmResult.SamTimeMs:F0}ms";
                if (!string.IsNullOrEmpty(overlaySuppressionText))
                    timingText += $" | {overlaySuppressionText}";
                SetCodingAiState(
                    $"{codierbarCount} Befunde erkannt" + (vorausCount > 0 ? $" ({vorausCount} voraus ignoriert)" : ""),
                    Color.FromRgb(0x22, 0xC5, 0x5E),
                    timingText);

                // Nur sichtbare codierbare Befunde als Events (Hintergrundmasken raus).
                AddMultiModelFindingsAsEvents(
                    visibleCodierbar,
                    mmResult.SamResponse?.ImageWidth ?? 1, mmResult.SamResponse?.ImageHeight ?? 1,
                    mmResult.YoloMaxConfidence, captureTimestampSec, frameOsdMeter);
                return;
            }

            // â”€â”€ Qwen-only Fallback-Pfad â”€â”€
            SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                "Schritt 1 von 3: Snapshot", pulse: true);

            {
                var pngBytes = await CaptureSnapshotAsync();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    SetCodingAiState("Frame nicht extrahierbar", Color.FromRgb(0xEF, 0x44, 0x44),
                        $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
                    return;
                }
                _detectionPendingFrameBytes = pngBytes;
                _detectionPendingTimestampSec = captureTimestampSec;
                var frameOsdMeter = await TryReadAnalyzedFrameOsdMeterAsync(
                    pngBytes,
                    captureTimestampSec,
                    _codingAnalysisCts.Token);

                SetCodingAiState(activityText, Color.FromRgb(0xF5, 0x9E, 0x0B),
                    $"Schritt 2 von 3: Inferenz ({LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)})", pulse: true);

                LiveDetection result;
                if (_codingEnhancedVision != null)
                {
                    var b64 = Convert.ToBase64String(pngBytes);
                    var importContext = GatherImportContext();
                    var enhanced = await _codingEnhancedVision.AnalyzeAsync(
                        b64, importContext, _codingAnalysisCts.Token);
                    result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, captureTimestampSec);
                }
                else
                {
                    result = await _codingLiveDetection!.AnalyzeFrameAsync(
                        pngBytes, captureTimestampSec, _codingAnalysisCts.Token);
                }
                result = result with { MeterReading = frameOsdMeter };

                ShowCodingAiResults(result);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetCodingAiState($"Fehler: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
        finally
        {
            _codingIsAnalyzing = false;
            if (disableAnalyzeButton)
                BtnCodingAnalyze.IsEnabled = true;
        }
    }

    private bool TryHandleBoundaryClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var code = mmResult.ClassifierCode;
        if (code is not ("BCD" or "BCE"))
            return false;
        if (_codingVm == null || _codingSessionService == null)
            return false;

        var videoTime = _codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);

        // Plausibilitaet eines Rohrende-Vorschlags: Der Klassifikator haelt das dunkle
        // Tunnelende am Fluchtpunkt manchmal faelschlich fuer das Rohrende, obwohl die
        // Kamera noch weit davon weg ist. Solch ein zu fruehes BCE wuerde alles
        // weitere Protokollieren stoppen. Fachregel User 2026-06-16: BCE nur nahe am
        // bekannten Haltungsende setzen. Zu frueh -> ignorieren und normal weiteranalysieren.
        if (code == "BCE"
            && !CodingDedupPolicy.IsBoundaryEndCodePlausible(code, meter, _codingVm.EndMeter))
        {
            var possibleLabel = LookupVsaLabel(code) ?? "Rohrende";
            System.Diagnostics.Debug.WriteLine(
                $"[Boundary] BCE bei {meter:F2}m verworfen (Haltungsende ~{_codingVm.EndMeter:F2}m, noch zu weit) - weiteranalysieren");
            SetCodingAiState("Mögliches Rohrende voraus - noch nicht am Ende",
                Color.FromRgb(0xF5, 0x9E, 0x0B), "näher heranfahren");
            ClearDetectionOverlays();
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            CodingFindingsList.ItemsSource = new[]
            {
                new AiFindingDisplayItem(new LiveFrameFinding(
                    Label: $"Mögliches {possibleLabel}",
                    Severity: 3,
                    PositionClock: null,
                    ExtentPercent: null,
                    VsaCodeHint: code))
            };
            return true;
        }

        var beforeCount = _codingVm.Events.Count;
        var anyAdded = false;

        if (code == "BCD")
        {
            EnsureRohranfangExists(meter, videoTime, _detectionPendingFrameBytes, ref anyAdded);
        }
        else
        {
            // VSA-Pflicht: bei Rohrende duerfen keine offenen Streckenschaeden zurueckbleiben.
            CloseTrackedStreckenschaeden(meter);
            EnsureRohrendeExists(_codingVm.EndMeter, videoTime, _detectionPendingFrameBytes);
            ClearDetectionOverlays();
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        }

        var label = LookupVsaLabel(code) ?? (code == "BCD" ? "Rohranfang" : "Rohrende");
        var added = anyAdded || _codingVm.Events.Count > beforeCount;
        var confidence = mmResult.ClassifierConfidence.HasValue
            ? $" {mmResult.ClassifierConfidence.Value:P0}"
            : "";
        var statusText = added ? $"{label} erkannt" : $"{label} bereits vorhanden";

        SetCodingAiState(statusText, Color.FromRgb(0x22, 0xC5, 0x5E),
            $"Klassifikator{confidence}");

        CodingFindingsList.ItemsSource = new[]
        {
            new AiFindingDisplayItem(new LiveFrameFinding(
                Label: label,
                Severity: 4,
                PositionClock: null,
                ExtentPercent: null,
                VsaCodeHint: code))
        };

        return true;
    }

    private bool TryHandleStructuralClassifierResult(
        SingleFrameResult mmResult,
        double captureTimestampSec,
        double? frameOsdMeter)
    {
        var code = mmResult.ClassifierCode;
        if (code is not ("BCA" or "BCC"))
            return false;

        // Wenn DINO/SAM Befunde liefert, bleibt der praezisere Maskenpfad zustaendig.
        if (mmResult.HasDetections)
            return false;

        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null)
            return false;

        var meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromSeconds(captureTimestampSec);
        var label = LookupVsaLabel(code) ?? (code == "BCC" ? "Bogen" : "Anschluss");
        var finding = new LiveFrameFinding(
            Label: label,
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);
        var resolvedCode = ResolveFindingCodeForCoding(finding, meter);
        if (resolvedCode == null || !resolvedCode.StartsWith(code, StringComparison.OrdinalIgnoreCase))
            return false;

        var coveringEvent = codingVm.Events.FirstOrDefault(e =>
            CodingDedupPolicy.CodesMatch(e.Entry.Code, resolvedCode) &&
            CodingFindingCoveragePolicy.IsCovered(e, meter, finding));

        var confidence = mmResult.ClassifierConfidence.HasValue
            ? $" {mmResult.ClassifierConfidence.Value:P0}"
            : "";

        ClearDetectionOverlays();
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        CodingFindingsList.ItemsSource = new[]
        {
            new AiFindingDisplayItem(finding with { VsaCodeHint = resolvedCode })
        };

        if (coveringEvent != null)
        {
            SetCodingAiState($"{label} bereits vorhanden", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Klassifikator{confidence}");
            return true;
        }

        var draft = CodingStructuralClassifierEventFactory.Create(
            resolvedCode,
            LookupVsaLabel(resolvedCode) ?? label,
            label,
            mmResult.ClassifierConfidence,
            meter,
            videoTime,
            meterFromOsd: _lastResolvedMeterIsOsd);

        AttachAnalyzedFramePhoto(draft.Entry);

        var ev = codingSessionService.AddEvent(draft.Entry);
        ev.MeterAtCapture = meter;
        ev.VideoTimestamp = videoTime;
        ev.AiContext = draft.AiContext;

        RefreshCodingEventsList();
        SetCodingAiState($"{draft.Entry.Beschreibung} erkannt", Color.FromRgb(0x22, 0xC5, 0x5E),
            $"Klassifikator{confidence}");
        return true;
    }

    private bool IsCodingAfterTerminalBoundary(double? currentMeter, TimeSpan currentVideoTime)
    {
        return CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            CodingTerminalBoundaryCandidateBuilder.Enumerate(
                _codingSessionService?.ActiveSession?.Events,
                _codingVm?.Events,
                _codingImportEvents),
            currentMeter,
            currentVideoTime);
    }

    /// <summary>
    /// <summary>
    /// Sammelt alle Import-Eintraege als Erwartungshorizont fuer die KI-Analyse.
    /// Die KI erhaelt die bekannten VSA-Codes und kann sie zuweisen statt "???".
    /// </summary>
    // â”€â”€ Multi-Model Rendering (YOLO â†’ DINO â†’ SAM) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Rendert Multi-Model Ergebnisse: SAM-Masken (gruene Konturen) + Label-Badges mit Messungen.
    /// </summary>
    private void ShowMultiModelResults(SingleFrameResult mmResult, IReadOnlyList<SegmentedFinding> segmented)
    {
        // Alte Masken entfernen
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

        // Gruene SAM-Masken nur fuer codierbare (nahe) Befunde. Voraus-Befunde werden
        // nicht gezeichnet (siehe BuildVisibleMaskFindings) — nur intern gemerkt.
        if (mmResult.SamResponse != null)
        {
            if (mmResult.SamResponse is { ImageWidth: > 0, ImageHeight: > 0 } srAsp)
                _codingVideoAspect = (double)srAsp.ImageWidth / srAsp.ImageHeight;

            var visibleMasks = CodingSegmentedFindingVisibility.BuildVisibleMaskFindings(segmented);
            if (visibleMasks.Count > 0)
            {
                var candidates = visibleMasks
                    .Select(s => new Ai.Pipeline.SamMaskRenderer.MaskRenderCandidate(
                        s.Mask,
                        s.Proximity.IsCodierbar ? s.Quant : null,
                        s.Dino?.Confidence))
                    .ToList();

                var maskContent = GetCodingContentRect();
                Ai.Pipeline.SamMaskRenderer.RenderCandidates(
                    CodingOverlayCanvas,
                    candidates,
                    mmResult.SamResponse.ImageWidth,
                    mmResult.SamResponse.ImageHeight,
                    maskContent.Width,
                    maskContent.Height,
                    logger: _serviceProvider?.LoggerFactory.CreateLogger("SamMaskRenderer"),
                    options: Ai.Pipeline.SamMaskRenderer.WinCanStyleOptions,
                    offsetX: maskContent.X,
                    offsetY: maskContent.Y);
            }
        }

        // "Voraus"-Befunde werden NICHT mehr gezeichnet (Fachregel User 2026-06-16:
        // erst zwischen DN-Kreis und Bildrand zeigen/codieren). Sie bleiben nur intern
        // in 'segmented' gemerkt; der Status meldet "Ereignis voraus erkannt".
        double iw = mmResult.SamResponse?.ImageWidth ?? 0;
        double ih = mmResult.SamResponse?.ImageHeight ?? 0;
        if (iw > 0 && ih > 0)
            _codingVideoAspect = iw / ih;

        // Kalibrierkreis anzeigen
        _showReferenceDn = true;
        RenderReferenceDn();
    }

    /// <summary>
    /// Naehe-Gate fuer den Qwen/Enhanced-Pfad: prueft per Bbox + Kalibrierung, ob ein
    /// KI-Befund noch zu weit voraus ist (ganz im DN-Kreis). Fachregel User 2026-06-16:
    /// erst codieren wenn das Ereignis ueber den DN-Kreis nach aussen reicht.
    /// Ohne verwertbare Bbox kann die Distanz nicht geometrisch geprueft werden ->
    /// konservativ false (nicht blockieren), damit reine Textbefunde nicht verschwinden.
    /// </summary>
    private bool IsFindingTooFarAhead(LiveFrameFinding finding)
    {
        return CodingFindingProximityPolicy.IsTooFarAhead(
            finding,
            _codingOverlayService?.Calibration,
            _codingVideoAspect);
    }

    /// <summary>Baut SegmentedFindings aus dem Multi-Model-Ergebnis inkl. Naehe-Pruefung.</summary>
    private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings(SingleFrameResult mmResult)
    {
        if (mmResult.SamResponse == null)
            return System.Array.Empty<SegmentedFinding>();

        var cal = _codingOverlayService?.Calibration;
        double vanishX = cal?.PipeCenter.X ?? 0.5;
        double vanishY = cal?.PipeCenter.Y ?? 0.5;
        double pipeRadius = (cal != null && cal.NormalizedDiameter > 0) ? cal.NormalizedDiameter / 2.0 : 0.5;

        return SegmentedFindingBuilder.Build(
            mmResult.SamResponse,
            mmResult.DinoDetections,
            mmResult.QuantifiedMasks,
            vanishX, vanishY, pipeRadius,
            AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);
    }

    /// <summary>
    /// Automatische Streckenschaden-Verfolgung (VSA 2.1.2). Laeuft bei JEDEM Analyse-Tick,
    /// auch mit leerer Streckenschaden-Liste — sonst koennte der Tracker offene Strecken nie
    /// automatisch schliessen. Die Fachregel liegt in Application (StreckenschadenTracker +
    /// StreckenschadenActionMapper); hier wird nur gefiltert, aufgerufen und Events angelegt/geaendert.
    ///
    /// Streckenschaden-Befunde (Code mit IsStreckenschadenCode) werden NICHT als Punkt-Events
    /// gefuehrt — die hier "verbrauchten" Segmente werden zurueckgegeben, damit der normale
    /// Punkt-Loop sie ueberspringt (referenzgleich, exakt die Streckenschaden-Codes).
    /// </summary>
    private HashSet<SegmentedFinding> ApplyStreckenschadenTracking(
        IReadOnlyList<SegmentedFinding> segmented, double meter, TimeSpan videoTime)
    {
        var consumed = new HashSet<SegmentedFinding>();
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null)
            return consumed;

        // 1) Codierbare Streckenschaden-Befunde sammeln und Code aufloesen (gleicher Resolver wie Loop).
        var observations = new List<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation>();
        foreach (var seg in segmented)
        {
            if (!seg.Proximity.IsCodierbar) continue;
            var q = seg.Quant;
            var pseudo = new LiveFrameFinding(
                Label: q.Label,
                Severity: CodingQuantificationSeverityPolicy.Estimate(q),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                VsaCodeHint: null);
            var code = ResolveFindingCodeForCoding(pseudo, meter);
            if (code == null) continue;
            if (!VsaCodeResolver.IsStreckenschadenCode(code)) continue;

            consumed.Add(seg);
            var clock = LiveDetectionGeometryMapper.ParseClockHour(q.ClockPosition);
            observations.Add(new AuswertungPro.Next.Application.Ai.StreckenschadenTracker.Observation(
                MainCode: code, ClockHour: clock, Meter: meter));
        }

        // 2) Tracker fuettern (auch mit leerer Liste -> ermoeglicht Auto-Schliessen nach Toleranzdistanz).
        var actions = _streckenTracker.Update(observations, meter);

        // 3) Aktionen in konkrete Anweisungen uebersetzen und ausfuehren.
        ApplyStreckenschadenActions(actions, videoTime);
        return consumed;
    }

    /// <summary>
    /// Fuehrt die vom Mapper bestimmten Anweisungen aus: offenen Streckenschaden-Eintrag anlegen
    /// bzw. einen bestehenden schliessen (MeterEnd setzen). Keine Fachlogik hier.
    /// </summary>
    private void ApplyStreckenschadenActions(
        IReadOnlyList<AuswertungPro.Next.Application.Ai.StreckenschadenTracker.SegmentAction> actions,
        TimeSpan videoTime)
    {
        var codingSessionService = _codingSessionService;
        var codingVm = _codingVm;
        if (codingSessionService == null || codingVm == null || actions.Count == 0)
            return;

        // Aktuell offene Streckenschaden-Eintraege als Mapper-Sicht (Referenz = CodingEvent).
        var openEntries = codingVm.Events
            .Where(e => e.Entry.IsStreckenschaden && !e.Entry.MeterEnd.HasValue)
            .Select(e => new AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.OpenEntry(
                MainCode: e.Entry.Code, StartMeter: e.Entry.MeterStart ?? e.MeterAtCapture, Reference: e))
            .ToList();

        var instructions = AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.MapAll(actions, openEntries);
        if (instructions.Count == 0) return;

        bool anyChanged = false;
        foreach (var instr in instructions)
        {
            switch (instr.Kind)
            {
                case AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.InstructionKind.CreateOpen:
                {
                    var draft = CodingStreckenschadenEventFactory.CreateOpen(
                        instr.MainCode,
                        LookupVsaLabel(instr.MainCode),
                        instr.StartMeter,
                        videoTime);
                    AttachAnalyzedFramePhoto(draft.Entry);
                    var ev = codingSessionService.AddEvent(draft.Entry);
                    ev.MeterAtCapture = instr.StartMeter;
                    ev.AiContext = draft.AiContext;
                    anyChanged = true;
                    break;
                }
                case AuswertungPro.Next.Application.Ai.StreckenschadenActionMapper.InstructionKind.CloseExisting:
                {
                    if (instr.TargetReference is CodingEvent target)
                    {
                        target.Entry.MeterEnd = instr.EndMeter;
                        target.Entry.IsStreckenschaden = true;
                        codingSessionService.UpdateEvent(target.EventId, target.Entry, target.Overlay);
                        anyChanged = true;
                    }
                    break;
                }
            }
        }

        if (anyChanged)
            RefreshCodingEventsList();
    }

    /// <summary>
    /// Schliesst ALLE vom Tracker gefuehrten offenen Strecken am angegebenen Meter (Pflicht bei
    /// Rohrende BCE / Abbruch BDC / Exit). Fuehrt die Close-Anweisungen aus; der bestehende
    /// CloseOpenStreckenschaeden-Dialog bleibt nur als Sicherheitsnetz fuer Reste.
    /// </summary>
    private void CloseTrackedStreckenschaeden(double endMeter)
    {
        var actions = _streckenTracker.CloseAll(endMeter);
        if (actions.Count == 0) return;
        var videoTime = _player != null ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;
        ApplyStreckenschadenActions(actions, videoTime);
    }

    /// <summary>
    /// Erstellt CodingEvents aus Multi-Model Befunden (DINO-Detections + SAM-Quantifizierung).
    /// </summary>
    /// <summary>
    /// Multi-Model Findings als CodingEvents â€” nutzt denselben Resolver-
    /// und Label-Pfad wie der Qwen/Enhanced-Pfad (ResolveFindingCodeForCoding, LookupVsaLabel).
    /// </summary>
    private void AddMultiModelFindingsAsEvents(
        IReadOnlyList<SegmentedFinding> segmented, double imageWidth, double imageHeight,
        double? yoloMaxConfidence, double captureTimestampSec, double? frameOsdMeter)
    {
        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null) return;

        double meter = ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;

        // Streckenschaden-Befunde (laengs > 1 m) laufen NICHT als Punkt-Events, sondern ueber den
        // automatischen Tracker. Laeuft bei jedem Tick (auch leer) -> ermoeglicht Auto-Schliessen.
        // Die hier verbrauchten Segmente werden im Punkt-Loop uebersprungen (genau die Streckencodes).
        var streckenConsumed = ApplyStreckenschadenTracking(segmented, meter, videoTime);

        // BCD wird NICHT mehr automatisch erzeugt â€” nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        foreach (var seg in segmented)
        {
            if (streckenConsumed.Contains(seg)) continue; // als Streckenschaden behandelt
            var quant = seg.Quant;
            var dino = seg.Dino;

            // Gemeinsamer Resolver: DINO-Label â†’ LiveFrameFinding â†’ ResolveFindingCodeForCoding
            // So laeuft der Multi-Model-Pfad durch exakt denselben Code wie Qwen.
            var pseudoFinding = new LiveFrameFinding(
                Label: quant.Label,
                Severity: CodingQuantificationSeverityPolicy.Estimate(quant),
                PositionClock: NormalizeClockPosition(quant.ClockPosition),
                ExtentPercent: quant.ExtentPercent,
                VsaCodeHint: null,  // DINO liefert englische Labels, kein VSA-Code
                HeightMm: quant.HeightMm,
                WidthMm: quant.WidthMm,
                IntrusionPercent: quant.IntrusionPercent,
                CrossSectionReductionPercent: quant.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: dino != null ? dino.X1 / imageWidth : null,
                BboxY1: dino != null ? dino.Y1 / imageHeight : null,
                BboxX2: dino != null ? dino.X2 / imageWidth : null,
                BboxY2: dino != null ? dino.Y2 / imageHeight : null);

            // Gemeinsamer Resolver (identisch mit Qwen-Pfad)
            var code = ResolveFindingCodeForCoding(pseudoFinding, meter);
            if (code == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] Kein VSA-Code fuer Label='{quant.Label}' â€” uebersprungen");
                continue;
            }

            if (CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser(code, seg.Proximity))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Multi-Model] {code} bei {meter:F2}m nur voraus erkannt - nicht protokolliert");
                continue;
            }

            var officialLabel = LookupVsaLabel(code);

            // BCD/BCE existieren pro Haltung nur EINMAL â€” Meterstand-unabhaengige Dedup
            // Primaer gegen session.Events pruefen (wird nie gecleared).
            if (CodingDedupPolicy.IsOneTimeCode(code)
                && (codingSessionService.ActiveSession?.Events.Any(e =>
                        CodingDedupPolicy.CodesMatch(e.Entry.Code, code)) == true
                    || codingVm.Events.Any(e => CodingDedupPolicy.CodesMatch(e.Entry.Code, code))))
                continue;

            // Dedup gegen bestehende Events (identisch mit Qwen-Pfad)
            var coveringEvent = codingVm.Events.FirstOrDefault(e =>
                CodingDedupPolicy.CodesMatch(e.Entry.Code, code) &&
                CodingFindingCoveragePolicy.IsCovered(e, meter, pseudoFinding));
            if (coveringEvent != null) continue;

            // QualityGate mit Multi-Model Evidenz
            double dinoConf = dino?.Confidence ?? quant.Confidence;
            // D2-A: ECHTE YOLO-Confidence (hoechste Box des Frames) statt Festwert 0.8.
            // Ist sie null (keine YOLO-Box), ueberspringt das QualityGate das Signal und
            // renormalisiert ueber DINO/SAM/Plausibilitaet. Klar erkannte Befunde bekommen
            // so wieder eine ehrliche, hohe Confidence statt durchgehend gelb.
            var evidence = new EvidenceVector(
                YoloConf: yoloMaxConfidence,
                DinoConf: dinoConf,
                SamMaskStability: quant.Confidence,
                PlausibilityScore: officialLabel != null ? 0.8 : 0.4
            );
            var gateResult = _codingQualityGate?.Evaluate(evidence)
                ?? new QualityGateResult(dinoConf, TrafficLight.Yellow,
                    new Dictionary<string, double>(), "Multi-Model")!;

            var quantRule = CodingManifestQuantRuleResolver.Resolve(CodeSelectionCatalog, code);
            var draft = CodingMultiModelEventFactory.Create(
                code,
                officialLabel,
                seg,
                meter,
                videoTime,
                dinoConf,
                gateResult.CompositeConfidence,
                imageWidth,
                imageHeight,
                meterFromOsd: _lastResolvedMeterIsOsd,
                calibration: _codingOverlayService?.Calibration,
                manifestRule: quantRule);

            AttachAnalyzedFramePhoto(draft.Entry);

            var codingEvent = codingSessionService.AddEvent(draft.Entry);
            codingEvent.AiContext = draft.AiContext;
            codingEvent.Overlay = draft.Overlay;

            anyAdded = true;
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            UpdateToolBadge();
        }
        // KEIN PauseAndAskConfirmation im kontinuierlichen Live-Loop: der 5s-Timer
        // (CodingLiveAiTimer_Tick) haelt bei WaitingForUserInput/Pause an — ein Pause-Dialog
        // pro Befund wuergt damit die laufende Erkennung ab (Regression aus D1). Befunde
        // bleiben als Ignored in der KI-BEFUNDE-Liste und werden dort bestaetigt; das Video
        // laeuft durch und erkennt ueber die ganze Haltung.
    }

    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
    {
        if (_codingImportEvents == null || _codingImportEvents.Count == 0)
            return null;

        var context = new List<(string, string, double)>();
        foreach (var evt in _codingImportEvents)
        {
            var entry = evt.Entry;
            var code = entry?.Code;
            if (string.IsNullOrWhiteSpace(code)) continue;
            context.Add((code, entry?.Beschreibung ?? code, evt.MeterAtCapture));
        }

        return context.Count > 0 ? context : null;
    }

    private void ShowCodingAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetCodingAiState($"Fehler: {result.Error}", Color.FromRgb(0xEF, 0x44, 0x44),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
            CodingFindingsList.ItemsSource = null;
            return;
        }

        // â”€â”€ Zustandsautomat: Einblendung vs. echtes Videobild â”€â”€
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
                $"Warte auf Videobild... (Bild {_codingFrameReadiness.SkippedFrames} von 3)");
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

        // â”€â”€ Ab hier: Frame ist bereit fuer Analyse â”€â”€

        // OSD-Meterstand uebernehmen (Defense-in-Depth: nochmals Plausibilitaet pruefen)
        if (result.MeterReading.HasValue && result.MeterReading.Value <= 500 && _codingVm != null)
        {
            _codingLastOsdMeter = result.MeterReading.Value;
            _codingLastOsdTimestampSec = result.TimestampSeconds;
            _codingSessionService?.MoveToMeter(result.MeterReading.Value);
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"{result.MeterReading.Value:F2}m (OSD)";
        }

        // â”€â”€ Findings filtern: VSA-Validierung + Deduplizierung â”€â”€
        // Eine einzige gefilterte Liste fuer UI, Overlays und Event-Erstellung.
        var currentMeter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var validFindings = FilterValidFindings(result.Findings, currentMeter);

        if (validFindings.Count == 0)
        {
            var noDamageText = result.MeterReading.HasValue
                ? $"OSD {result.MeterReading.Value:F2}m \u2013 Kein Schaden"
                : "Kein Schaden";
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

        // Vor dem Hinzufuegen pruefen, welche Befunde schon bekannt/abgehandelt sind
        // (durch ein bestehendes Event abgedeckt). Nur NEUE bekommen eine Box — sonst
        // tauchen akzeptierte Befunde bei jeder erneuten Analyse wieder als Box auf.
        var findingsToDraw = validFindings.Where(f => !IsFindingAlreadyKnown(f, currentMeter)).ToList();

        // KI-Findings als CodingEvents mit AiContext in die Ereignisliste einfuegen
        AddAiFindingsAsEvents(result, validFindings);

        // Nur NEUE Befunde als visuelle Overlays auf dem Videobild anzeigen
        if (findingsToDraw.Count > 0 && !CodingOverlayPopup.IsOpen)
        {
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            RenderDetectionOverlay(findingsToDraw, _player.Time / 1000.0);
            ScheduleDetectionAutoHide();   // verbleibende Boxen nach 3s ausblenden (Liste bleibt)
        }
        else
        {
            // Nichts Neues zu zeigen -> evtl. noch sichtbare Alt-Boxen wegnehmen (Liste bleibt)
            DetectionCanvas.Children.Clear();
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Filtert KI-Findings: VSA-Code-Validierung, BCD/BCE-Ausschluss, Deduplizierung.
    /// Die gefilterte Liste wird fuer UI, Overlays und Event-Erstellung verwendet.
    /// Deduplizierung: code + BBox-Mittelpunkt (verschiedene Positionen = verschiedene Befunde).
    /// </summary>
    /// <summary>
    /// Filtert und normalisiert KI-Findings.
    /// Nach diesem Schritt gilt fuer jedes Finding:
    ///   - VsaCodeHint ist ein gueltiger VSA-Code (validiert) oder das Finding wurde verworfen
    ///   - Keine "???"-Codes, keine ungeprueften Hint-Werte
    /// </summary>
    private IReadOnlyList<LiveFrameFinding> FilterValidFindings(IReadOnlyList<LiveFrameFinding> raw, double currentMeter)
    {
        return CodingFindingFilterPolicy.FilterValid(
            raw,
            currentMeter,
            ResolveFindingCodeForCoding,
            _codingSessionService?.ActiveSession?.Events,
            _codingVm?.Events,
            message => System.Diagnostics.Debug.WriteLine(message));
    }

    /// <summary>
    /// Klartext-Lookup fuer einen VSA-Code mit Fallback-Kette:
    /// Voller Code â†’ 3-Zeichen-Hauptcode â†’ 2-Zeichen-Gruppe â†’ null.
    /// </summary>
    /// <summary>Delegiert an VsaCodeResolver.LookupLabel.</summary>
    private static string? LookupVsaLabel(string code) => VsaCodeResolver.LookupLabel(code);

    /// <summary>Delegiert an VsaCodeResolver.NormalizeClock.</summary>
    private static string? NormalizeClockPosition(string? raw) => VsaCodeResolver.NormalizeClock(raw);

    /// <summary>
    /// Einzige Quelle fuer VSA-Code-Aufloesung eines KI-Findings.
    /// Delegiert an VsaCodeResolver (zentrale Utility) + Import-Verfeinerung.
    /// Gibt validen VSA-Code oder null zurueck â€” nie "???".
    /// </summary>
    private string? ResolveFindingCodeForCoding(LiveFrameFinding finding, double currentMeter)
    {
        return CodingFindingCodeResolver.Resolve(finding, currentMeter, _codingImportEvents);
    }

    /// <summary>
    /// KI-Befunde als CodingEvents eintragen â€” mit QualityGate-Ampelsystem.
    /// Erwartet bereits gefilterte Findings (aus FilterValidFindings).
    /// </summary>
    // Ist dieser Befund schon durch ein bestehendes Event abgedeckt (bekannt/abgehandelt)?
    // Nutzt dieselbe Dedup-Logik wie das Event-Hinzufuegen (Code-Match + IsAlreadyCovered),
    // damit akzeptierte Befunde nicht bei jeder Analyse wieder als Box gezeichnet werden.
    private bool IsFindingAlreadyKnown(LiveFrameFinding finding, double meter)
    {
        return CodingKnownFindingPolicy.IsKnown(
            finding,
            meter,
            _codingSessionService?.ActiveSession?.Events,
            _codingVm?.Events);
    }

    private void AddAiFindingsAsEvents(LiveDetection result, IReadOnlyList<LiveFrameFinding> validFindings)
    {
        var codingVm = _codingVm;
        var codingSessionService = _codingSessionService;
        if (codingVm == null || codingSessionService == null) return;

        double meter = ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading);
        var videoTime = codingVm.CurrentVideoTime ?? TimeSpan.FromMilliseconds(_player.Time);
        bool anyAdded = false;
        CodingEvent? firstUnsure = null;
        QualityGateResult? firstUnsureGate = null;

        // BCD wird NICHT mehr automatisch erzeugt â€” nur durch Eingabemarker oder Qwen-Erkennung.
        // EnsureRohranfangExists(meter, videoTime, ref anyAdded);

        if (validFindings.Count == 0)
        {
            if (anyAdded) RefreshCodingEventsList();
            return;
        }

        foreach (var finding in validFindings)
        {
            // FilterValidFindings garantiert: VsaCodeHint ist ein gueltiger VSA-Code.
            // Kein zweiter Inferenzpfad hier â€” nur uebernehmen.
            string code = finding.VsaCodeHint!;

            // Naehe-Gate (Fachregel User 2026-06-16): Ereignis noch ganz im DN-Kreis
            // (zu weit voraus) -> nur intern erkannt, NICHT codieren. Erst wenn es ueber
            // den DN-Kreis nach aussen reicht, stimmt die Distanz.
            // Ausnahme: Steuercodes BCD/BCE (Rohranfang/-ende) sind Pflicht und duerfen
            // nicht weggemerkt werden.
            if (!CodingDedupPolicy.IsOneTimeCode(code) && IsFindingTooFarAhead(finding))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Qwen] {code} bei {meter:F2}m nur voraus erkannt (im DN-Kreis) - nicht protokolliert");
                continue;
            }

            // BCD/BCE existieren pro Haltung nur EINMAL â€” Meterstand-unabhaengige Dedup.
            // Primaer gegen session.Events pruefen (wird nie gecleared, im Gegensatz zu _codingVm.Events).
            if (CodingDedupPolicy.IsOneTimeCode(code)
                && (codingSessionService.ActiveSession?.Events.Any(e =>
                        CodingDedupPolicy.CodesMatch(e.Entry.Code, code)) == true
                    || codingVm.Events.Any(e => CodingDedupPolicy.CodesMatch(e.Entry.Code, code))))
            {
                System.Diagnostics.Debug.WriteLine($"[BCD-Dedup] AddFindings: {code} uebersprungen (bereits vorhanden)");
                continue;
            }

            // Klartext aufloesen (voller Code â†’ Hauptcode â†’ Gruppe)
            var officialLabel = LookupVsaLabel(code);

            // Duplikat-Check: gleicher Code (oder gleicher Hauptcode) bereits vorhanden?
            // Hauptcode-Match: BCAEB vs BCA = gleiche Schadensgruppe â†’ Duplikat.
            // 1. Punktschaden: code + meter Â±0.3m + gleiche Position
            // 2. Streckenschaden: code faellt in den MeterStart..MeterEnd Bereich
            // 3. Bereits akzeptierter/bearbeiteter Code: nicht nochmal melden
            var coveringEvent = codingVm.Events.FirstOrDefault(e =>
                CodingDedupPolicy.CodesMatch(e.Entry.Code, code) &&
                CodingFindingCoveragePolicy.IsCovered(e, meter, finding));
            if (coveringEvent != null)
            {
                // Offener Streckenschaden: letzte Sichtung merken (fuer automatisches Schliessen)
                // MeterEnd bleibt null (= offen) â€” wird beim Exit via CloseOpenStreckenschaeden gesetzt
                if (coveringEvent.Entry.IsStreckenschaden)
                    coveringEvent.MeterAtCapture = Math.Max(coveringEvent.MeterAtCapture, meter);
                continue;
            }

            var gateResult = CodingLiveFindingQualityGatePolicy.Evaluate(_codingQualityGate, finding);

            var draft = CodingLiveFindingEventFactory.Create(
                code,
                officialLabel,
                finding,
                meter,
                videoTime,
                gateResult);

            // Foto 1: exakt der analysierte KI-Frame, damit die Vorschau sofort ein Bild hat.
            AttachAnalyzedFramePhoto(draft.Entry);

            var codingEvent = codingSessionService.AddEvent(draft.Entry);
            codingEvent.AiContext = draft.AiContext;
            codingEvent.Overlay = draft.Overlay;

            anyAdded = true;

            // Zur Bestaetigung vorlegen, wenn die KI unsicher ist (gelb/rot) ODER
            // der Befund kritisch ist (Severity >= 4) - kritische Schaeden duerfen
            // niemals stillschweigend uebernommen werden.
            if ((!gateResult.IsGreen || finding.Severity >= 4) && firstUnsure == null)
            {
                firstUnsure = codingEvent;
                firstUnsureGate = gateResult;
            }
        }

        if (anyAdded)
        {
            RefreshCodingEventsList();
            RenderAiOverlays();
            if (codingVm.CurrentOverlay != null)
                RenderOverlayGeometry(codingVm.CurrentOverlay, isPreview: false);
            UpdateToolBadge();
        }

        if (firstUnsure != null && firstUnsureGate != null)
            PauseAndAskConfirmation(firstUnsure, firstUnsureGate);
    }

    private void CodingLiveAi_Click(object sender, RoutedEventArgs e)
    {
        if (BtnCodingLiveAi.IsChecked == true)
        {
            _codingLiveAiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _codingLiveAiTimer.Tick += CodingLiveAiTimer_Tick;
            _codingLiveAiTimer.Start();

            // Gruen blinken wenn aktiv
            _codingLiveAiBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _codingLiveAiBlinkTimer.Tick += (_, _) =>
            {
                if (_closing || _player is null) return;
                _codingLiveAiBlinkState = !_codingLiveAiBlinkState;
                BtnCodingLiveAi.Background = new SolidColorBrush(
                    _codingLiveAiBlinkState
                        ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
                        : Color.FromRgb(0x16, 0x65, 0x34));  // Dunkelgruen
            };
            _codingLiveAiBlinkTimer.Start();
            BtnCodingLiveAi.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));

            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
        else
        {
            _codingLiveAiTimer?.Stop();
            _codingLiveAiTimer = null;

            // Blinken stoppen, Standardfarbe zuruecksetzen
            _codingLiveAiBlinkTimer?.Stop();
            _codingLiveAiBlinkTimer = null;
            BtnCodingLiveAi.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

            SetCodingAiState("Künstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
    }

    private async void CodingLiveAiTimer_Tick(object? sender, EventArgs e)
    {
        if (_closing || _player is null) return;
        try
        {
            // Nicht analysieren wenn: bereits analysierend, Video pausiert, WaitingForUserInput
            if (_codingLiveDetection == null) return;
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

    /// <summary>VLC-Snapshot als PNG-Bytes extrahieren.</summary>
    private async Task<byte[]?> CaptureSnapshotAsync()
    {
        var tmpDir = Path.GetTempPath();
        var snapFile = Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");
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
                return await File.ReadAllBytesAsync(snapFile);
            return null;
        }
        finally
        {
            AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (File.Exists(snapFile)) File.Delete(snapFile); }, "Snapshot: Temp loeschen");
        }
    }

    // --- Ampel: Pause + Bestaetigungs-Panel ---

    private void PauseAndAskConfirmation(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        // Video pausieren
        _player.SetPause(true);
        _codingSessionService?.SetWaitingForInput();

        _codingPendingConfirmEvent = codingEvent;
        _codingPendingGateResult = gateResult;

        // Ampel-Farbe setzen (Gruen = sicher, aber kritischer Befund zur Bestaetigung)
        var ampelColor = gateResult.IsGreen
            ? Color.FromRgb(0x22, 0xC5, 0x5E)   // Gruen
            : gateResult.IsYellow
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)   // Gelb
                : Color.FromRgb(0xEF, 0x44, 0x44);   // Rot
        ConfirmAmpel.Fill = new SolidColorBrush(ampelColor);

        // Globale Ampel aktualisieren
        SetCodingAiState(TxtCodingAiStatus.Text, ampelColor,
            gateResult.IsGreen ? "QualityGate: Grün (kritisch)"
            : gateResult.IsYellow ? "QualityGate: Gelb" : "QualityGate: Rot");

        // Panel befuellen
        TxtConfirmCode.Text = codingEvent.Entry.Code ?? "???";
        TxtConfirmConfidence.Text = $"({gateResult.CompositeConfidence:P0})";
        TxtConfirmDescription.Text = codingEvent.Entry.Beschreibung ?? codingEvent.AiContext?.Reason ?? "";
        TxtConfirmDetail.Text = gateResult.IsGreen
            ? "Kritischer Befund \u2014 bitte bestätigen oder korrigieren."
            : gateResult.IsYellow
                ? "KI ist unsicher \u2014 bitte prüfen."
                : "KI hat geringe Sicherheit \u2014 bitte Code korrigieren oder verwerfen.";

        CodingConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent?.AiContext != null)
        {
            _codingPendingConfirmEvent.AiContext.Decision = CodingUserDecision.Accepted;
            // QualityGate-Ampel aufs Event schreiben, BEVOR das Panel _codingPendingGateResult auf null setzt.
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveAccept");
        }

        CloseConfirmationAndResume();
    }

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
    {
        // VSA-Code-Explorer oeffnen \u2192 User waehlt korrekten Code
        CloseConfirmationPanel();

        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.AcceptedWithEdit;
            // Defect-Detail-Panel oeffnen fuer manuelle Bearbeitung
            LstCodingEvents.SelectedItem = _codingPendingConfirmEvent;
        }

        ResumeAfterConfirmation();
    }

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
    {
        if (_codingPendingConfirmEvent != null)
        {
            _codingPendingConfirmEvent.AiContext!.Decision = CodingUserDecision.Rejected;
            if (_codingPendingGateResult != null)
                _codingPendingConfirmEvent.AiContext.QualityGateLevel =
                    _codingPendingGateResult.TrafficLight.ToString();

            // Gold-Fund: abgelehnten Befund als Negativbeispiel (Status=Rejected, inkl. Snapshot)
            // sichern, BEVOR er aus der Session entfernt wird.
            PersistSingleEventAsTrainingSample(_codingPendingConfirmEvent).SafeFireAndForget("TrainingSaveReject");

            _codingSessionService?.RemoveEvent(_codingPendingConfirmEvent.EventId);
            _codingVm?.Events.Remove(_codingPendingConfirmEvent);
            RefreshCodingEventsList();
        }

        CloseConfirmationAndResume();
    }

    private void CloseConfirmationAndResume()
    {
        CloseConfirmationPanel();
        ResumeAfterConfirmation();
    }

    private void CloseConfirmationPanel()
    {
        CodingConfirmationPanel.Visibility = Visibility.Collapsed;
        _codingPendingConfirmEvent = null;
        _codingPendingGateResult = null;
    }

    private void ResumeAfterConfirmation()
    {
        // Session wieder auf Running
        if (_codingSessionService?.ActiveSession?.State == CodingSessionState.WaitingForUserInput)
            _codingSessionService.ResumeSession();

        // Video weiterlaufen lassen (wenn Auto-KI aktiv)
        if (BtnCodingLiveAi.IsChecked == true)
            _player.SetPause(false);

        // Globale Ampel zuruecksetzen
        if (BtnCodingLiveAi.IsChecked == true)
        {
            SetCodingAiState("Automatische KI-Analyse aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Intervall alle 5 Sekunden | {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
        else
        {
            SetCodingAiState("Künstliche Intelligenz bereit", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(_codingAiModelName)}");
        }
    }
}
