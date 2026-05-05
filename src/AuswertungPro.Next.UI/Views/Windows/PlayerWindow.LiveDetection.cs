using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.E Sub-A: Live-Detection-Lifecycle (Cluster B3, Teil 1).
//
// Lebenszyklus der Live-KI-Erkennung (5s-Timer + Snapshot + LiveDetectionService):
//   - LiveDetection_Click (Toggle-Button-Handler)
//   - StartLiveDetectionAsync / StopLiveDetection
//   - DetectionTimer_Tick + RunDetectionAsync (Inferenz-Loop)
//   - CaptureCurrentFrameAsync (640px-Snapshot fuer Live-Loop)
//   - UpdateDetectionStatus (Status-Text-Update)
//   - ClearDetectionOverlays (Canvas leeren)
//
// Status-Badges (SetLiveDetectionBadge, SetYoloStatus) — UI-State-Updates
// fuer das KI-Statusfeld.
//
// Felder im Hauptpartial: _liveDetectionClient, _liveDetectionService,
//   _detectionTimer, _detectionCts, _isDetecting, _isDetectionInFlight,
//   _liveDetectionModelName, _currentFindings, _detectionPendingFindings,
//   _detectionPendingFrameBytes, _detectionPendingTimestampSec,
//   _lastDetectionTimestamp, _isWindowClosed, _isManualMarkMode, _player.
public partial class PlayerWindow
{
    // ─── Status-Badges ─────────────────────────────────────────────────────────

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

    // ─── Live-Detection Lifecycle ──────────────────────────────────────────────

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
        AiRuntimeConfig cfg;
        try { cfg = AiRuntimeConfig.Load(); }
        catch
        {
            MessageBox.Show("KI-Konfiguration konnte nicht geladen werden.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            LiveDetectionButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
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
                $"Modell: {CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(visionModel));

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
            MessageBox.Show($"Live-KI konnte nicht gestartet werden: {ex.Message}", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopLiveDetection()
    {
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

        // Hide overlay layer (unless manual mark mode is still active)
        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", Color.FromRgb(0x94, 0xA3, 0xB8));
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;
        _currentFindings.Clear();

        // Fertig-Meldung mit Zusammenfassung
        int totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet — {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        // Video pausieren damit der User die Meldung sieht
        if (_player != null && _player.IsPlaying)
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
        // Window bereits geschlossen waehrend Tick im Flug: sofort raus,
        // sonst greift RunDetectionAsync auf disposed _player/Canvas zu.
        if (_isWindowClosed) return;
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
        if (_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null)
            return;
        if (!_player.IsPlaying)
            return;
        // Keine neue Analyse waehrend User-Bestaetigung
        if (_detectionPendingFindings != null)
            return;

        _isDetectionInFlight = true;
        SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
            $"{CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Bereit");
                return;
            }

            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{CompactModelName(_liveDetectionModelName)} | Inferenz");
            var timestampSec = _player.Time / 1000.0;
            var result = await _liveDetectionService.AnalyzeFrameAsync(
                snapshot, timestampSec, _detectionCts.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (!_isDetecting) return;

                _lastDetectionTimestamp = result.TimestampSeconds;
                _currentFindings.Clear();
                _currentFindings.AddRange(result.Findings);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Overlay");

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
                        $"{CompactModelName(_liveDetectionModelName)} | Warte auf Bestaetigung");
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Length > 200) msg = msg[..200] + "...";
            Dispatcher.Invoke(() =>
            {
                LiveDetectionStatusText.Text = $"Fehler: {msg}";
                SetLiveDetectionBadge("KI Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                    CompactModelName(_liveDetectionModelName));
            });
        }
        finally
        {
            _isDetectionInFlight = false;
        }
    }

    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"sewer_live_{Guid.NewGuid():N}.png");
        try
        {
            var success = TakeSnapshotSafe(tempPath, 640);
            if (!success)
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
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private void UpdateDetectionStatus(LiveDetection result)
    {
        if (result.Error is not null)
        {
            LiveDetectionStatusText.Text = $"Fehler: {result.Error}";
            return;
        }

        var count = result.Findings.Count;
        LiveDetectionStatusText.Text = count > 0
            ? $"{count} Befund(e) erkannt @ {result.TimestampSeconds:0.0}s"
            : $"Kein Befund @ {result.TimestampSeconds:0.0}s";

        if (count > 0)
        {
            var summary = string.Join(" | ",
                result.Findings.Take(3).Select(f =>
                    $"{f.VsaCodeHint ?? f.Label} (S{f.Severity})"));
            FindingSummaryPanel.Visibility = Visibility.Visible;
            FindingSummaryText.Text = summary;
        }
        else
        {
            FindingSummaryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }
}
