using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Rectangle = System.Windows.Shapes.Rectangle;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog
        => _serviceProvider?.CodeSelectionCatalog;

    private AppProtocol.ICodeCatalogProvider? CodeCatalog
        => _serviceProvider?.CodeCatalog;

    private ViewModels.Windows.VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit)
        => new(entry, presetMeter, presetZeit, CodeSelectionCatalog);

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Live Detection ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveDetectionBadge(status, dotColor, stage));
            return;
        }

        var stageSuffix = string.IsNullOrWhiteSpace(stage) ? string.Empty : $" | {stage}";
        AiStatusBadge.Visibility = Visibility.Visible;
        AiStatusText.Text = $"{status}{stageSuffix}";
        AiStatusDot.Fill = new SolidColorBrush(dotColor);
    }

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetYoloStatus(text, dotColor, model));
            return;
        }

        YoloStatusBar.Visibility = Visibility.Visible;
        TxtYoloStatus.Text = $"YOLO: {text}";
        YoloDot.Fill = new SolidColorBrush(dotColor);
        TxtYoloModel.Text = model ?? string.Empty;
    }

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

    private async void LiveDetection_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
            return;
        }

        await StartLiveDetectionAsync();
    }

    private async Task StartLiveDetectionAsync()
    {
        AiRuntimeSettings cfg;
        try
        {
            cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
        }
        catch
        {
            DialogHost.Current.Warn("KI-Konfiguration konnte nicht geladen werden.", "Live-KI");
            LiveDetectionButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            DialogHost.Current.Info("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI");
            LiveDetectionButton.IsChecked = false;
            return;
        }

        try
        {
            var client = new OllamaClient(cfg.OllamaBaseUri,
                ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
                keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);

            // Auto-detect vision model: check if configured model exists, fallback to first *vl* model
            var visionModel = cfg.VisionModel;
            try
            {
                var models = await client.ListModelNamesAsync(CancellationToken.None);
                bool configuredExists = false;
                string? fallbackVision = null;
                foreach (var m in models)
                {
                    if (m.StartsWith(visionModel, StringComparison.OrdinalIgnoreCase) ||
                        m.Equals(visionModel, StringComparison.OrdinalIgnoreCase))
                        configuredExists = true;
                    if (fallbackVision == null && m.Contains("vl", StringComparison.OrdinalIgnoreCase))
                        fallbackVision = m;
                }
                if (!configuredExists && fallbackVision != null)
                    visionModel = fallbackVision;
            }
            catch { /* use configured model */ }

            _liveDetectionClient = client;
            _liveDetectionService = new LiveDetectionService(client, visionModel);
            _liveDetectionModelName = visionModel;
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            // Show overlay layer
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", Color.FromRgb(0x22, 0xC5, 0x5E), LiveDetectionDisplayPolicy.CompactModelName(visionModel));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _detectionTimer.Tick += DetectionTimer_Tick;
            _detectionTimer.Start();

            // Run first detection immediately
            RunDetectionAsync().SafeFireAndForget("LiveDetection");
        }
        catch (Exception ex)
        {
            LiveDetectionButton.IsChecked = false;
            DialogHost.Current.Warn($"Live-KI konnte nicht gestartet werden: {ex.Message}", "Live-KI");
        }
    }

    private void StopLiveDetection()
    {
        var updateUi = !_closing && !_playbackDisposed;

        _detectionTimer?.Stop();
        _detectionTimer = null;
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
        _isDetecting = false;
        _isDetectionInFlight = false;
        _liveDetectionService = null;
        _liveDetectionClient?.Dispose();
        _liveDetectionClient = null;
        _liveDetectionModelName = string.Empty;
        _currentFindings.Clear();

        if (!updateUi)
            return;

        // Hide overlay layer (unless manual mark mode is still active)
        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", Color.FromRgb(0x94, 0xA3, 0xB8));
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;

        // Fertig-Meldung mit Zusammenfassung
        int totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet — {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        // Video pausieren damit der User die Meldung sieht
        if (_player != null && !_playbackDisposed && _player.IsPlaying)
            _player.SetPause(true);

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            if (!_isDetecting)
                LiveDetectionStatusText.Visibility = Visibility.Collapsed;
        };
        hideTimer.Start();
    }

    private async void DetectionTimer_Tick(object? sender, EventArgs e)
    {
        if (_closing || _player is null) return;
        try
        {
            await RunDetectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] DetectionTimer_Tick Fehler: {ex.Message}");
        }
    }

    private async Task RunDetectionAsync()
    {
        if (_closing || _player is null) return;
        if (_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null)
            return;
        if (!_player.IsPlaying)
            return;
        // Keine neue Analyse waehrend User-Bestaetigung
        if (_detectionPendingFindings != null)
            return;

        _isDetectionInFlight = true;
        SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
            $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                if (!_closing && !_playbackDisposed)
                {
                    SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Bereit");
                }
                return;
            }

            if (_closing || _playbackDisposed || _liveDetectionService is null || _detectionCts is null)
                return;

            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Inferenz");
            var timestampSec = _player.Time / 1000.0;
            var result = await _liveDetectionService.AnalyzeFrameAsync(
                snapshot, timestampSec, _detectionCts.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (_closing || _playbackDisposed || !_isDetecting) return;

                _lastDetectionTimestamp = result.TimestampSeconds;
                _currentFindings.Clear();
                _currentFindings.AddRange(result.Findings);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Overlay");

                // Auto-Pause bei relevanten Befunden (Severity >= 2)
                var significantFindings = result.Findings
                    .Where(f => f.Severity >= 2).ToList();
                if (significantFindings.Count > 0)
                {
                    _detectionPendingFindings = significantFindings;
                    _detectionPendingFrameBytes = snapshot;
                    _detectionPendingTimestampSec = result.TimestampSeconds;
                    ShowDetectionConfirmation(significantFindings);
                    SetLiveDetectionBadge("Befund erkannt", Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Warte auf Bestaetigung");
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_closing || _playbackDisposed)
                return;

            var msg = ex.Message;
            if (msg.Length > 200) msg = msg[..200] + "...";
            Dispatcher.Invoke(() =>
            {
                if (_closing || _playbackDisposed)
                    return;

                LiveDetectionStatusText.Text = $"Fehler: {msg}";
                SetLiveDetectionBadge("KI Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                    LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName));
            });
        }
        finally
        {
            _isDetectionInFlight = false;
        }
    }

    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        if (_closing || _playbackDisposed)
            return null;

        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sewer_live_{Guid.NewGuid():N}.png");
        try
        {
            var success = TakeSnapshotSafe(tempPath, 640);
            if (!success || _closing || _playbackDisposed)
                return null;

            // Wait briefly for file write
            await Task.Delay(80);

            if (!File.Exists(tempPath))
                return null;

            return await File.ReadAllBytesAsync(tempPath,
                _detectionCts?.Token ?? CancellationToken.None);
        }
        catch
        {
            return null;
        }
        finally
        {
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => { if (File.Exists(tempPath)) File.Delete(tempPath); }, "Snapshot: Temp loeschen");
        }
    }

    private void UpdateDetectionStatus(LiveDetection result)
    {
        LiveDetectionStatusText.Text = LiveDetectionDisplayPolicy.BuildDetectionStatusText(result);
        if (result.Error is not null)
            return;

        if (result.Findings.Count > 0)
        {
            FindingSummaryPanel.Visibility = Visibility.Visible;
            FindingSummaryText.Text = LiveDetectionDisplayPolicy.BuildFindingSummaryText(result.Findings);
        }
        else
        {
            FindingSummaryPanel.Visibility = Visibility.Collapsed;
        }
    }

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Detection Overlay Rendering (ring-sector pattern from LiveFrameWindow) ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
        => LiveDetectionOverlayRenderer.Render(DetectionCanvas, findings, timestampSec, OnFindingClicked);

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Manual Marking ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    /// <summary>
    /// Nach abgeschlossener Markierung (Ellipse/Freihand/Rechteck): Code-Katalog oeffnen + Training speichern.
    /// </summary>
    private async void HandleMarkDrawingComplete()
    {
        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null) return;

            var timestampSec = _player.Time / 1000.0;

            // Frame einmal erfassen — wird fuer SAM-Segmentierung UND den YOLO-Export wiederverwendet.
            var frameBytes = await CaptureCurrentFrameAsync();

            // Uhrlage zunaechst geometrisch (Overlay-Zentrum -> Uhr) als Fallback.
            string? clockPos = LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(overlay);

            // SAM segmentiert die gezogene Box und schreibt echte Messwerte (Uhrlage/Hoehe/
            // Breite/Querschnitt) ins Overlay. Bei fehlendem Sidecar/Maske bleibt die
            // geometrische Schaetzung erhalten — der Codier-Ablauf wird nie blockiert.
            var samResult = await TrySegmentMarkBoxAsync(overlay, frameBytes);
            if (!string.IsNullOrEmpty(samResult?.Quant.ClockPosition))
                clockPos = samResult!.Quant.ClockPosition;

            // SAM-Maske SICHTBAR machen, BEVOR das Codierfenster aufgeht: der Nutzer sieht,
            // dass die KI die Markierung (z.B. den Bogen) erfasst hat. Kurze Pause, damit das
            // Overlay tatsaechlich gezeichnet wird, dann erst das VSA-Codierfenster.
            if (samResult != null)
            {
                ShowMarkSamMask(samResult, overlay);
                await Task.Delay(3000);   // 3 s: SAM-Maske sichtbar lassen, dann erst das Codefenster
            }

            // Training speichern + Codierfenster (VsaCodeExplorer) mit vorausgefuellten Messwerten.
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos, frameBytes);

            // Overlay + SAM-Maske + Bogen-Marker entfernen und Canvas neu zeichnen
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            ClearBendMarkers();
            if (_codingVm != null) _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);

            // Codiermodus: Werkzeug NICHT abschalten, sonst loest die naechste Box keine
            // Segmentierung / kein Codefenster mehr aus (Bug "funktioniert nur einmal").
            // Nur in der Live-Markierung (ausserhalb Codiermodus) nach dem Speichern abschalten.
            if (saved && !_isCodingMode)
            {
                // Erfolgreich gespeichert â†’ Tool deaktivieren
                DeactivateMarkTool();
            }
            else
            {
                // Abgebrochen â†’ Tool bleibt aktiv, naechste Markierung kann sofort gezeichnet werden
                if (_codingOverlayService != null)
                    _codingOverlayService.ActiveTool = _markToolType;
                CodingOverlayCanvas.Cursor = Cursors.Cross;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    /// <summary>
    /// Laesst SAM die gezogene Box segmentieren und schreibt die Messwerte ins Overlay
    /// (Hoehe/Breite mm, Querschnitt-%, Uhrlage). Gibt die SAM-Uhrlage zurueck oder null,
    /// wenn keine Segmentierung moeglich war (Aufrufer behaelt dann die geometrische
    /// Schaetzung). Reine Verdrahtung — die Logik liegt im MarkBoxSegmentationService.
    /// </summary>
    // Gibt das Segmentierungs-Ergebnis (inkl. Rohmaske) zurueck, damit der Aufrufer die
    // SAM-Maske sichtbar rendern kann. null, wenn keine Segmentierung moeglich war.
    private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync(
        OverlayGeometry overlay, byte[]? frameBytes)
    {
        if (_codingBoxSegmentation == null || frameBytes == null || frameBytes.Length == 0
            || overlay.Points.Count < 2)
            return null;
        try
        {
            var box = Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y)).ToList());
            var calibration = _codingOverlayService?.Calibration;
            int dn = calibration?.NominalDiameterMm ?? 0;

            var result = await _codingBoxSegmentation.SegmentBoxAsync(
                frameBytes, box, dn, calibration, System.Threading.CancellationToken.None);
            if (result == null) return null;

            if (result.Quant.HeightMm.HasValue) overlay.Q1Mm = result.Quant.HeightMm.Value;
            if (result.Quant.WidthMm.HasValue) overlay.Q2Mm = result.Quant.WidthMm.Value;
            var cross = result.Quant.CrossSectionReductionPercent ?? result.Quant.ExtentPercent;
            if (cross.HasValue) overlay.FillPercent = cross.Value;
            if (!string.IsNullOrEmpty(result.Quant.ClockPosition)
                && double.TryParse(result.Quant.ClockPosition,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var clk))
                overlay.ClockFrom = clk;

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Segmentierung uebersprungen: {ex.Message}");
            return null;
        }
    }

    // Zeigt die Erkennung der Mark-Box sichtbar auf dem Codier-Canvas, BEVOR das
    // VSA-Codierfenster aufgeht. Bei einem BOGEN waere die SAM-Maske irrefuehrend
    // (sie deckt das ganze runde Rohr-Loch ab, nicht den Bogen-Rand - SAM/SAM3/Hough koennen
    // die Bogen-Kontur nicht treffen, empirisch belegt). Daher fuer Boegen einen GEOMETRIE-
    // MARKER am Fluchtpunkt zeichnen (wo das Rohr abknickt) statt der Maske. Fuer echte
    // Punktschaeden (Riss/Anschluss) die SAM-Maske wie bisher.
    private void ShowMarkSamMask(Infrastructure.Ai.Pipeline.BoxSegmentationResult result, OverlayGeometry? overlay)
    {
        try
        {
            var rect = GetCodingContentRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            // BOX-SPEZIFISCH: is_bend ist frame-weit. Der Bogen-Marker darf NUR erscheinen,
            // wenn die GEZOGENE Box wirklich den Bogen meint - d.h. den Fluchtpunkt umschliesst
            // (die abknickende Rohroeffnung liegt am Fluchtpunkt). Ein Punktschaden an der Wand
            // liegt NICHT am Fluchtpunkt und behaelt seine SAM-Maske, auch wenn der Frame
            // zusaetzlich als Bogen gilt.
            if (result.IsBend && LiveDetectionGeometryMapper.BoxContainsVanishingPoint(overlay, result.VanishX, result.VanishY))
            {
                ShowBendMarker(result.VanishX, result.VanishY, rect);
                return;
            }

            // WICHTIG: in das tatsaechliche Video-Rechteck rendern (Letterbox/Pillarbox-Raender),
            // NICHT in die volle Canvas-Flaeche - sonst Maske verzerrt/verschoben.
            var samResp = new Infrastructure.Ai.Pipeline.SamResponse(
                new[] { result.Mask }, result.ImageWidth, result.ImageHeight, 0);
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                samResp,
                new[] { result.Quant },
                rect.Width,
                rect.Height,
                logger: null,
                options: null,
                offsetX: rect.X,
                offsetY: rect.Y);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Masken-Render uebersprungen: {ex.Message}");
        }
    }

    // Zeichnet einen Bogen-Marker (Ring + Label) am Fluchtpunkt - die ehrliche Anzeige
    // "KI hat hier einen Bogen erkannt", da eine praezise Bogen-Kontur technisch nicht
    // zuverlaessig moeglich ist. vanishX/Y sind normiert (0..1) im Video-Rechteck.
    private void ShowBendMarker(double vanishX, double vanishY, Rect rect)
    {
        double cx = rect.X + vanishX * rect.Width;
        double cy = rect.Y + vanishY * rect.Height;
        double r = Math.Max(24, Math.Min(rect.Width, rect.Height) * 0.10);

        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = r * 2, Height = r * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0x22, 0xC5, 0x5E)),
            IsHitTestVisible = false,
            Tag = "bend_marker"
        };
        Canvas.SetLeft(ring, cx - r);
        Canvas.SetTop(ring, cy - r);
        CodingOverlayCanvas.Children.Add(ring);

        var label = new System.Windows.Controls.TextBlock
        {
            Text = "Bogen erkannt",
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromArgb(200, 0x22, 0xC5, 0x5E)),
            Padding = new Thickness(4, 1, 4, 1),
            FontSize = 12,
            IsHitTestVisible = false,
            Tag = "bend_marker"
        };
        Canvas.SetLeft(label, cx - r);
        Canvas.SetTop(label, Math.Max(0, cy - r - 20));
        CodingOverlayCanvas.Children.Add(label);
    }

    // Entfernt alle Bogen-Marker (Tag "bend_marker") vom Codier-Canvas.
    private void ClearBendMarkers()
    {
        for (int i = CodingOverlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (CodingOverlayCanvas.Children[i] is FrameworkElement fe
                && (fe.Tag as string) == "bend_marker")
                CodingOverlayCanvas.Children.RemoveAt(i);
        }
    }

    /// <summary>
    /// Speichert eine Markierung als Teacher-Annotation (YOLO-Export + TeacherAnnotationStore).
    /// Eigenstaendige Implementierung im PlayerWindow-Codiermodus.
    /// </summary>
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition, byte[]? preCapturedFrame = null)
    {
        try
        {
            // 1. VSA-Code waehlen â€” VsaCodeExplorer oeffnet sich sofort
            // Meter automatisch aus OSD oder Videoposition berechnen
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = CodingExplorerEntryFactory.CreateSeed(overlay);
            var explorerVm = CreateVsaCodeExplorerViewModel(entry, autoMeter, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };
            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
                return false;

            var selectedEntry = explorer.SelectedEntry;

            // Den selbst gesetzten Code SOFORT als KI-BEFUND eintragen — unabhaengig davon, ob
            // der nachfolgende Training-/YOLO-Export klappt. Sonst fehlt der Code in KI-BEFUNDE,
            // wenn der Export scheitert (User-Wunsch: jeder eigene Code MUSS erscheinen).
            CodingEvent? manualEvent = null;
            if (_codingSessionService != null && _codingVm != null)
            {
                var manualMeter = 0.0;
                if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var pm0))
                    manualMeter = pm0;
                var manualEntry = CodingExplorerEntryFactory.CreateManualFromSelected(
                    selectedEntry,
                    manualMeter,
                    TimeSpan.FromSeconds(timestampSec));
                manualEvent = _codingSessionService.AddEvent(manualEntry, overlay);
                RefreshCodingEventsList();
            }

            // 2. Frame-Capture (bereits vor der SAM-Segmentierung erfasst -> wiederverwenden).
            var frameBytes = preCapturedFrame ?? await CaptureCurrentFrameAsync();
            if (frameBytes == null) return false;

            // 3. BoundingBox aus Overlay-Punkten
            var bbox = Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y)).ToList());

            // Mindestgroesse pruefen (1% des Frames)
            if (bbox.Width < 0.01 || bbox.Height < 0.01) return false;

            // 4. YOLO-Export
            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            // Frame in Temp speichern
            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_mark_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Temp aufrÃ¤umen
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => System.IO.File.Delete(tempFrame), "Mark-Training: Temp-Frame loeschen");

            // 5. TeacherAnnotation erstellen + persistieren
            var captureMeter = 0.0;
            if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedMeter))
                captureMeter = parsedMeter;

            var annotation = LiveDetectionTeacherAnnotationFactory.CreateManualMark(
                annotationId,
                selectedEntry,
                overlay,
                bbox,
                clockPosition,
                captureMeter,
                TimeSpan.FromSeconds(timestampSec),
                exportResult);

            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Foto nachtraeglich an den bereits eingetragenen Befund haengen (der Code wurde oben
            // schon SOFORT eingetragen, damit er auch bei Export-Fehlern in KI-BEFUNDE steht).
            if (manualEvent != null && exportResult.FullFramePath != null)
            {
                manualEvent.Entry.FotoPaths.Add(exportResult.FullFramePath);
                RefreshCodingEventsList();
            }

            ShowOsdMeterStatus($"✓ {selectedEntry.Code} gespeichert", resetAfterDelay: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
            return false;
        }
    }

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
    {
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = message;

        if (!resetAfterDelay)
            return;

        var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) =>
        {
            resetTimer.Stop();
            if (_codingLastOsdMeter.HasValue)
                TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
            else
                OsdMeterBadge.Visibility = Visibility.Collapsed;
        };
        resetTimer.Start();
    }

    private void DetectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker nutzt CodingOverlayCanvas (nicht DetectionCanvas)

        if (!_isManualMarkMode)
            return;

        var clickPoint = e.GetPosition(DetectionCanvas);
        var canvasSize = new Size(DetectionCanvas.ActualWidth, DetectionCanvas.ActualHeight);

        if (canvasSize.Width < 60 || canvasSize.Height < 60)
            return;

        // Pause video
        _player.SetPause(true);

        var clockPosition = LiveDetectionGeometryMapper.ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        _player.SetPause(true);
        OpenCodeCatalogForMark(
            finding.PositionClock,
            timestampSec,
            finding.VsaCodeHint);
    }

    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        var sp = _serviceProvider;

        if (sp?.CodeCatalog is null)
        {
            DialogHost.Current.Info(
                "Schadenscode-Katalog nicht verfügbar.\n" +
                "Bitte die App neu starten oder KI-Einstellungen prüfen.",
                "Markieren");
            return;
        }

        var entry = CodingExplorerEntryFactory.CreateSeed(
            videoTime: TimeSpan.FromSeconds(timestampSec),
            suggestedCode: suggestedCode,
            clockPosition: clockPosition);

        var explorerVm = CreateVsaCodeExplorerViewModel(
            entry,
            _codingLastOsdMeter ?? GetMeterFromVideoPosition(),
            TimeSpan.FromSeconds(timestampSec));

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
        {
            Owner = this
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

            _onEntryCreated?.Invoke(entry);
            ShowOverlay($"Beobachtung erfasst: {entry.Code}", TimeSpan.FromSeconds(4));
        }
    }

    // ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â
    // CODIER-MODUS (integriert im PlayerWindow)
    // ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â
}
