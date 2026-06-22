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

using AuswertungPro.Next.UI.Player;

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

    // Live Detection

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
            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(visionModel));

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
        SetYoloStatus("Gestoppt", PlayerStatusColors.Muted);
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
        SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
            $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                if (!_closing && !_playbackDisposed)
                {
                    SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                        $"{LiveDetectionDisplayPolicy.CompactModelName(_liveDetectionModelName)} | Bereit");
                }
                return;
            }

            if (_closing || _playbackDisposed || _liveDetectionService is null || _detectionCts is null)
                return;

            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Warning,
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

                SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
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
                    SetLiveDetectionBadge("Befund erkannt", PlayerStatusColors.Warning,
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
                SetLiveDetectionBadge("KI Fehler", PlayerStatusColors.Error,
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

    // Detection Overlay Rendering (ring-sector pattern from LiveFrameWindow)

    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
        => LiveDetectionOverlayRenderer.Render(DetectionCanvas, findings, timestampSec, OnFindingClicked);
}
