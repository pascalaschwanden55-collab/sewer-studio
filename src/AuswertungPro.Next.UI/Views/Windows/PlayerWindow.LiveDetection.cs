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
using AuswertungPro.Next.Application.Ai.Teacher;
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
    private async void QuickScan_Click(object sender, RoutedEventArgs e)
    {
        if (_isQuickScanning)
        {
            _quickScanCts?.Cancel();
            QuickScanButton.IsChecked = false;
            return;
        }

        AiRuntimeSettings cfg;
        try
        {
            cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
        }
        catch
        {
            MessageBox.Show("KI-Konfiguration konnte nicht geladen werden.", "Schnell-Scan",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            QuickScanButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Schnell-Scan",
                MessageBoxButton.OK, MessageBoxImage.Information);
            QuickScanButton.IsChecked = false;
            return;
        }

        var ffmpegPath = cfg.FfmpegPath ?? FfmpegLocator.ResolveFfmpeg();
        using var client = new OllamaClient(cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
            keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);
        var service = new QuickScanService(client, cfg.VisionModel, ffmpegPath);

        _quickScanCts = new CancellationTokenSource();
        _isQuickScanning = true;

        HeatmapCanvas.Children.Clear();
        _heatmapRects.Clear();

        QuickScanStatusText.Visibility = Visibility.Visible;
        QuickScanStatusText.Text = "Starte...";

        var progress = new Progress<QuickScanProgress>(p =>
        {
            QuickScanStatusText.Text = p.Status;
            if (p.LatestSegment is { } seg)
                AddHeatmapSegment(seg, p.FramesTotal * 5.0); // estimate duration
        });

        try
        {
            var result = await service.ScanAsync(_videoPath, progress, _quickScanCts.Token);

            // Rebuild heatmap with exact duration
            HeatmapCanvas.Children.Clear();
            _heatmapRects.Clear();
            foreach (var seg in result.Segments)
                AddHeatmapSegment(seg, result.VideoDurationSeconds);

            QuickScanStatusText.Text = result.Error ?? $"Fertig: {result.FramesAnalyzed} Frames analysiert";
        }
        catch (OperationCanceledException)
        {
            QuickScanStatusText.Text = "Abgebrochen";
        }
        catch (Exception ex)
        {
            QuickScanStatusText.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            _isQuickScanning = false;
            QuickScanButton.IsChecked = false;
            _quickScanCts?.Dispose();
            _quickScanCts = null;

            // Hide status after 5 seconds
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            hideTimer.Tick += (_, _) =>
            {
                hideTimer.Stop();
                if (!_isQuickScanning)
                    QuickScanStatusText.Visibility = Visibility.Collapsed;
            };
            hideTimer.Start();
        }
    }

    private void AddHeatmapSegment(QuickScanSegment segment, double videoDurationSec)
    {
        if (videoDurationSec <= 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        double segWidth = (5.0 / videoDurationSec) * trackWidth;
        if (segWidth < 2) segWidth = 2;

        double ratio = Math.Clamp(segment.TimestampSeconds / videoDurationSec, 0.0, 1.0);
        double x = offsetX + ratio * trackWidth;

        var rect = new Rectangle
        {
            Width = segWidth,
            Height = 6,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(SeverityToColor(segment.Severity, segment.HasDamage)),
            Cursor = Cursors.Hand,
            Opacity = segment.HasDamage ? 0.85 : 0.4
        };

        var tip = segment.HasDamage
            ? $"Schaden: {segment.Label ?? "?"} (Schwere {segment.Severity})"
              + (segment.Clock != null ? $"\nUhr: {segment.Clock}" : "")
              + $"\n@ {segment.TimestampSeconds:0.0}s"
            : $"Kein Schaden @ {segment.TimestampSeconds:0.0}s";
        rect.ToolTip = tip;

        var timestampSec = segment.TimestampSeconds;
        rect.MouseLeftButtonDown += (_, _) =>
        {
            EnsurePlaying();
            _player.SetPause(true);
            var length = _player.Length;
            if (length > 0)
            {
                var targetMs = (long)(timestampSec * 1000);
                if (targetMs > length) targetMs = length;
                _player.Time = targetMs;
            }
            UpdateUi();
        };

        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, 0);

        HeatmapCanvas.Children.Add(rect);
        _heatmapRects.Add((segment, rect));
    }

    private void RepositionHeatmap()
    {
        if (_heatmapRects.Count == 0)
            return;

        var (offsetX, trackWidth) = GetSliderTrackBounds();
        if (trackWidth <= 0)
            return;

        // Infer video duration from the last segment timestamp + step
        double videoDuration = 0;
        foreach (var (seg, _) in _heatmapRects)
        {
            if (seg.TimestampSeconds + 5.0 > videoDuration)
                videoDuration = seg.TimestampSeconds + 5.0;
        }
        if (videoDuration <= 0)
            return;

        foreach (var (seg, rect) in _heatmapRects)
        {
            double ratio = Math.Clamp(seg.TimestampSeconds / videoDuration, 0.0, 1.0);
            double x = offsetX + ratio * trackWidth;
            double w = (5.0 / videoDuration) * trackWidth;
            if (w < 2) w = 2;

            Canvas.SetLeft(rect, x);
            rect.Width = w;
        }
    }

    private static Color SeverityToColor(int severity, bool hasDamage)
    {
        if (!hasDamage)
            return Color.FromArgb(100, 0x94, 0xA3, 0xB8); // grey with alpha

        return severity switch
        {
            >= 4 => (Color)ColorConverter.ConvertFromString("#EF4444"), // red
            3    => (Color)ColorConverter.ConvertFromString("#F59E0B"), // orange
            2    => (Color)ColorConverter.ConvertFromString("#FACC15"), // yellow
            _    => (Color)ColorConverter.ConvertFromString("#22C55E"), // green
        };
    }

    private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog
        => _serviceProvider?.CodeSelectionCatalog ?? TryGetAppServiceProvider()?.CodeSelectionCatalog;

    private AppProtocol.ICodeCatalogProvider? CodeCatalog
        => _serviceProvider?.CodeCatalog ?? TryGetAppServiceProvider()?.CodeCatalog;

    private static ServiceProvider? TryGetAppServiceProvider()
    {
        try
        {
            return App.Services as ServiceProvider;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private ViewModels.Windows.VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit)
        => new(entry, presetMeter, presetZeit, CodeSelectionCatalog);

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Live Detection ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    private static string CompactModelName(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return "?";

        var trimmed = model.Trim();
        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
            trimmed = trimmed[(slashIndex + 1)..];
        return trimmed;
    }

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
        LiveDetectionStatusText.Text = $"KI-Analyse beendet â€” {totalEvents} Beobachtungen";
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
            $"{CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                if (!_closing && !_playbackDisposed)
                {
                    SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                        $"{CompactModelName(_liveDetectionModelName)} | Bereit");
                }
                return;
            }

            if (_closing || _playbackDisposed || _liveDetectionService is null || _detectionCts is null)
                return;

            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{CompactModelName(_liveDetectionModelName)} | Inferenz");
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
            ? $"{count} Schaden erkannt @ {result.TimestampSeconds:0.0}s"
            : $"Kein Schaden @ {result.TimestampSeconds:0.0}s";

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

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Detection Overlay Rendering (ring-sector pattern from LiveFrameWindow) ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
    {
        DetectionCanvas.Children.Clear();

        var width = DetectionCanvas.ActualWidth;
        var height = DetectionCanvas.ActualHeight;
        if (width < 60 || height < 60)
            return;

        if (findings.Count == 0)
            return;

        // Pruefen ob mindestens ein Finding Bbox hat
        bool hasBbox = findings.Any(f => f.BboxX1.HasValue && f.BboxY1.HasValue
                                       && f.BboxX2.HasValue && f.BboxY2.HasValue);

        // Wenn keine Bboxes: Fallback auf Ring-Sektor-Darstellung
        if (!hasBbox)
        {
            RenderRingSectorOverlay(findings, timestampSec, width, height);
            return;
        }

        // â”€â”€ Bbox-basiertes Rendering: Rechtecke + Labels direkt auf dem Bild â”€â”€
        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var finding = findings[i];
            var color = MapDetectionSeverityColor(finding.Severity);

            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var px1 = finding.BboxX1.Value * width;
                var py1 = finding.BboxY1.Value * height;
                var px2 = finding.BboxX2.Value * width;
                var py2 = finding.BboxY2.Value * height;

                var rectLeft = Math.Min(px1, px2);
                var rectTop = Math.Min(py1, py2);
                var rectW = Math.Max(1, Math.Abs(px2 - px1));
                var rectH = Math.Max(1, Math.Abs(py2 - py1));

                // Farbiges Rechteck (halbtransparent gefuellt, farbiger Rand)
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = rectW,
                    Height = rectH,
                    Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)),
                    RadiusX = 4,
                    RadiusY = 4,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, rectLeft);
                Canvas.SetTop(rect, rectTop);
                DetectionCanvas.Children.Add(rect);

                // Label-Badge oben am Rechteck
                var labelText = $"{finding.VsaCodeHint ?? finding.Label} [S{finding.Severity}]";
                if (finding.ExtentPercent is > 0)
                    labelText += $" {finding.ExtentPercent}%";

                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    IsHitTestVisible = true,
                    Child = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                };

                var capturedFinding = finding;
                var capturedTimestamp = timestampSec;
                label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
                label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
                    + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
                    + $"\nSchwere: {finding.Severity}/5";

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = label.DesiredSize;
                var lx = Math.Clamp(rectLeft, 2, width - desired.Width - 2);
                var ly = Math.Clamp(rectTop - desired.Height - 4, 2, height - desired.Height - 2);
                Canvas.SetLeft(label, lx);
                Canvas.SetTop(label, ly);
                DetectionCanvas.Children.Add(label);
            }
            else
            {
                // Einzelnes Finding ohne Bbox â†’ Ring-Sektor-Fallback
                RenderRingSectorFinding(finding, i, findings.Count, width, height, timestampSec);
            }
        }
    }

    /// <summary>
    /// Fallback: Ring-Sektor-Darstellung wenn keine Bounding Boxes verfuegbar.
    /// </summary>
    private void RenderRingSectorOverlay(IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec, double width, double height)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        // Aeusserer Fuehrungsring
        var guide = new System.Windows.Shapes.Ellipse
        {
            Width = ringOuter * 2, Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        DetectionCanvas.Children.Add(guide);

        // Innerer Fuehrungsring
        var guideInner = new System.Windows.Shapes.Ellipse
        {
            Width = ringInner * 2, Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        DetectionCanvas.Children.Add(guideInner);

        // Uhr-Teilstriche
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            DetectionCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = cx + Math.Cos(rad) * (ringInner - 4),
                Y1 = cy + Math.Sin(rad) * (ringInner - 4),
                X2 = cx + Math.Cos(rad) * (ringOuter + 4),
                Y2 = cy + Math.Sin(rad) * (ringOuter + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8, IsHitTestVisible = false
            });
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
            RenderRingSectorFinding(findings[i], i, findings.Count, width, height, timestampSec);
    }

    /// <summary>
    /// Rendert ein einzelnes Finding als Ring-Sektor (Fallback ohne Bbox).
    /// </summary>
    private void RenderRingSectorFinding(LiveFrameFinding finding, int index, int total,
        double width, double height, double timestampSec)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var parsedClock = ParseClockHour(finding.PositionClock);
        var centerDeg = parsedClock.HasValue
            ? -90 + (parsedClock.Value % 12) * 30
            : -90 + index * (360.0 / total);

        var sweep = finding.ExtentPercent is > 0
            ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
            : 18.0;

        var startDeg = centerDeg - sweep / 2.0;
        var color = MapDetectionSeverityColor(finding.Severity);

        var sector = new System.Windows.Shapes.Path
        {
            Data = BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
            Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 1.0, IsHitTestVisible = false
        };
        DetectionCanvas.Children.Add(sector);

        // Severity-Punkt ausserhalb Ring
        var rad2 = DegToRad(centerDeg);
        var mx = cx + Math.Cos(rad2) * (ringOuter + 2);
        var my = cy + Math.Sin(rad2) * (ringOuter + 2);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White, StrokeThickness = 0.8, IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, mx - 4);
        Canvas.SetTop(dot, my - 4);
        DetectionCanvas.Children.Add(dot);

        // Label-Badge (klickbar)
        var labelText = BuildDetectionLabel(finding);
        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 17, 19, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = labelText, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        var capturedFinding = finding;
        var capturedTimestamp = timestampSec;
        label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
        label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
            + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
            + $"\nSchwere: {finding.Severity}/5";

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var lx = Math.Cos(rad2) >= 0 ? mx + 8 : mx - desired.Width - 8;
        var ly = my - desired.Height / 2.0;
        Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
        Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
        DetectionCanvas.Children.Add(label);
    }

    private static string BuildDetectionLabel(LiveFrameFinding f)
    {
        var baseText = string.IsNullOrWhiteSpace(f.VsaCodeHint)
            ? f.Label : $"{f.VsaCodeHint} {f.Label}";
        if (baseText.Length > 24) baseText = baseText[..24] + "...";

        var clock = string.IsNullOrWhiteSpace(f.PositionClock) ? "?" : f.PositionClock;
        var extent = f.ExtentPercent is > 0 ? $"{f.ExtentPercent}%" : "";
        var extra = "";
        if (f.HeightMm is > 0) extra += $" H:{f.HeightMm}mm";
        if (f.IntrusionPercent is > 0) extra += $" Einr:{f.IntrusionPercent}%";
        return $"{clock}{(extent.Length > 0 ? $" / {extent}" : "")}{extra} - {baseText}";
    }

    private static Color MapDetectionSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),
        4 => Color.FromRgb(249, 115, 22),
        3 => Color.FromRgb(245, 158, 11),
        2 => Color.FromRgb(132, 204, 22),
        _ => Color.FromRgb(34, 197, 94)
    };

    private static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;
        if (hour == 0) return 12;
        if (hour > 12) hour %= 12;
        return hour == 0 ? 12 : hour;
    }

    private static Geometry BuildRingSectorGeometry(
        double cx, double cy, double innerR, double outerR, double startDeg, double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var large = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerR, cy + Math.Sin(startRad) * outerR);
        var p2 = new Point(cx + Math.Cos(endRad) * outerR, cy + Math.Sin(endRad) * outerR);
        var p3 = new Point(cx + Math.Cos(endRad) * innerR, cy + Math.Sin(endRad) * innerR);
        var p4 = new Point(cx + Math.Cos(startRad) * innerR, cy + Math.Sin(startRad) * innerR);

        var fig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        fig.Segments.Add(new ArcSegment(p2, new Size(outerR, outerR), 0, large, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(p3, true));
        fig.Segments.Add(new ArcSegment(p4, new Size(innerR, innerR), 0, large, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { fig });
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    // ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ Manual Marking ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬ÃƒÂ¢"Ã¢â€šÂ¬

    // â”€â”€ Markieren Popup-MenÃ¼ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private OverlayToolType _markToolType = OverlayToolType.None;

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        if (_isCodingMode)
            ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
        else
            MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
    {
        ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
    }

    private void MarkTool_Punkt_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Point, "Punkt");

    private void MarkTool_Ellipse_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Ellipse, "Ellipse");

    private void MarkTool_Freihand_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Freehand, "Freihand");

    private void MarkTool_Rechteck_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Rechteck");

    private void ActivateMarkTool(OverlayToolType tool, string label)
    {
        MarkToolPopup.IsOpen = false;
        CodingMarkToolPopup.IsOpen = false;
        ToolsDropdownPopup.IsOpen = false;
        _markToolType = tool;
        TxtMarkToolName.Text = label;
        TxtActiveToolLabel.Text = label;
        _player.SetPause(true);
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (tool == OverlayToolType.Point)
        {
            // Bestehende Punkt-Logik: DetectionCanvas aktivieren
            _isManualMarkMode = true;
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            DetectionOverlayGrid.IsHitTestVisible = true;
            DetectionCanvas.IsHitTestVisible = true;
            DetectionCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            // Zeichen-Tools: CodingOverlayPopup aktivieren
            _isManualMarkMode = false;
            EnsureMarkOverlayReady();
            _codingOverlayService!.ActiveTool = tool;

            // Offene Zeichnung verwerfen
            if (_codingVm != null)
                _codingVm.CurrentOverlay = null;

            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = Cursors.Cross;
        }
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private static ICodingSessionService CreateCodingSessionService()
    {
        return new CodingSessionService(
            () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig());
    }

    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= CreateCodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(
                _codingSessionService,
                _codingOverlayService,
                new InfraSelfImproving.CodingFeedbackRecorder());
        }
    }

    private void DeactivateMarkTool()
    {
        _markToolType = OverlayToolType.None;
        _isManualMarkMode = false;
        TxtMarkToolName.Text = "Markieren";

        DetectionCanvas.Cursor = Cursors.Arrow;
        DetectionCanvas.IsHitTestVisible = false;
        if (!_isDetecting)
        {
            DetectionOverlayGrid.IsHitTestVisible = false;
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }

        if (!_isCodingMode)
        {
            _codingSchemaManager.Cancel();
            _codingOverlayService?.CancelDraw();
            if (_codingOverlayService != null)
                _codingOverlayService.ActiveTool = OverlayToolType.None;
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.IsHitTestVisible = false;
        }
    }

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

            // Uhrzeiger-Position aus Overlay-Zentrum berechnen
            string? clockPos = null;
            if (overlay.Points.Count > 0)
            {
                var avgX = overlay.Points.Average(p => p.X);
                var avgY = overlay.Points.Average(p => p.Y);
                var cx = 0.5; var cy = 0.5; // Rohrmitte (normalisiert)
                var dx = avgX - cx;
                var dy = avgY - cy;
                var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var clockAngle = (angleDeg + 90 + 360) % 360;
                var hour = (int)Math.Round(clockAngle / 30.0) % 12;
                if (hour == 0) hour = 12;
                clockPos = hour.ToString();
            }

            // Training speichern: Frame + YOLO-Export + TeacherAnnotation + CodingEvent
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos);

            // Overlay entfernen und Canvas neu zeichnen
            if (_codingVm != null) _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);

            if (saved)
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
    /// Speichert eine Markierung als Teacher-Annotation (YOLO-Export + TeacherAnnotationStore).
    /// Vereinfachte Version von CodingModeWindow.BtnSaveAsTraining_Click.
    /// </summary>
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition)
    {
        try
        {
            // 1. VSA-Code waehlen â€” VsaCodeExplorer oeffnet sich sofort
            // Meter automatisch aus OSD oder Videoposition berechnen
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = new ProtocolEntry();
            var explorerVm = CreateVsaCodeExplorerViewModel(entry, autoMeter, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };
            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
                return false;

            var selectedEntry = explorer.SelectedEntry;

            // 2. Frame-Capture
            var frameBytes = await CaptureCurrentFrameAsync();
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
            try { System.IO.File.Delete(tempFrame); } catch { }

            // 5. TeacherAnnotation erstellen + persistieren
            var captureMeter = 0.0;
            if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedMeter))
                captureMeter = parsedMeter;

            var annotation = new TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = captureMeter,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                ToolType = overlay.ToolType,
                Points = new List<Domain.Models.NormalizedPoint>(
                    overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y))),
                BoundingBox = bbox,
                ClockPosition = clockPosition != null && double.TryParse(clockPosition, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = overlay.Q2Mm,
                HeightMm = overlay.Q1Mm
            };

            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Markierung AUCH als CodingEvent in die KI-Befunde-Liste eintragen
            if (_codingSessionService != null && _codingVm != null)
            {
                var codingEntry = new ProtocolEntry
                {
                    Source = ProtocolEntrySource.Manual,
                    Code = selectedEntry.Code,
                    Beschreibung = selectedEntry.Beschreibung,
                    MeterStart = selectedEntry.MeterStart ?? captureMeter,
                    MeterEnd = selectedEntry.MeterEnd,
                    Zeit = selectedEntry.Zeit ?? TimeSpan.FromSeconds(timestampSec),
                    IsStreckenschaden = selectedEntry.IsStreckenschaden,
                    CodeMeta = selectedEntry.CodeMeta
                };
                if (exportResult.FullFramePath != null)
                    codingEntry.FotoPaths.Add(exportResult.FullFramePath);

                _codingSessionService.AddEvent(codingEntry, overlay);
                RefreshCodingEventsList();
            }

            // Dezente Statusmeldung im OSD-Badge (kein MessageBox-Popup)
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"âœ“ {selectedEntry.Code} gespeichert";

            // Badge nach 3 Sekunden zuruecksetzen
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
            return true;
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"\u2717 Fehler: {ex.Message}";
            return false;
        }
    }

    // â”€â”€ LiveDetection Bestaetigungs-Logik â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private void ShowDetectionConfirmation(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0) return;

        // Video pausieren und zur Fundstelle springen
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        // Zur Fundstelle springen (Timestamp aus dem analysierten Frame)
        if (_detectionPendingTimestampSec.HasValue && _player != null)
        {
            long targetMs = (long)(_detectionPendingTimestampSec.Value * 1000);
            _player.Time = targetMs;
        }

        // Zusammenfassung der Befunde
        var primary = findings[0];
        var severityText = primary.Severity switch
        {
            5 => "S5 kritisch",
            4 => "S4 schwer",
            3 => "S3 mittel",
            2 => "S2 leicht",
            _ => $"S{primary.Severity}"
        };

        TxtDetectionFinding.Text = findings.Count == 1
            ? $"KI-Erkennung: {primary.Label} ({severityText})"
            : $"KI-Erkennung: {findings.Count} Befunde â€” {primary.Label} ({severityText})";

        var details = new System.Text.StringBuilder();
        foreach (var f in findings)
        {
            if (details.Length > 0) details.Append("  |  ");
            details.Append($"{f.PositionClock ?? "?"} Uhr Â· {f.Label}");
            if (f.ExtentPercent.HasValue) details.Append($" Â· {f.ExtentPercent}%");
        }
        TxtDetectionDetail.Text = details.ToString();

        DetectionConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ResumeDetection()
    {
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;

        // Video automatisch weiterlaufen lassen nach Entscheidung
        if (_player != null && !_player.IsPlaying)
            _player.Play();
    }

    private async void DetectionAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var timestampSec = _detectionPendingTimestampSec ?? (_player.Time / 1000.0);
            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();

            foreach (var finding in _detectionPendingFindings)
            {
                var code = finding.VsaCodeHint ?? finding.Label;
                int classId = InfraTeacher.VsaYoloClassMap.GetClassId(code);
                var annotationId = Guid.NewGuid().ToString("N")[..12];
                var baseName = $"det_{annotationId}";

                // Bounding-Box aus Uhrposition ableiten (Ring-Sektor â†’ normalisierte Koordinaten)
                var bbox = BBoxFromClockPosition(finding);

                // Frame temp speichern
                var tempFrame = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
                await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

                var exportResult = await exportService.ExportAsync(tempFrame, bbox, code, classId, baseName);
                try { System.IO.File.Delete(tempFrame); } catch { }

                // TeacherAnnotation erstellen
                var annotation = new TeacherAnnotation
                {
                    AnnotationId = annotationId,
                    VsaCode = code,
                    Beschreibung = finding.Label,
                    MeterPosition = 0,
                    VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                    ToolType = OverlayToolType.None,
                    Points = new List<Domain.Models.NormalizedPoint>(),
                    BoundingBox = bbox,
                    ClockPosition = double.TryParse(finding.PositionClock, out var cp) ? cp : null,
                    FullFramePath = exportResult.FullFramePath,
                    CroppedRegionPath = exportResult.CroppedRegionPath,
                    YoloAnnotationPath = exportResult.YoloAnnotationPath,
                    WidthMm = finding.WidthMm,
                    HeightMm = finding.HeightMm
                };
                await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);
            }

            // Dezente Bestaetigung im OSD-Badge
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"âœ“ {_detectionPendingFindings.Count} Befund(e) gespeichert";

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
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"âœ— Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private async void DetectionCorrect_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _player.Time / 1000.0;

            // VsaCodeExplorer oeffnen fuer Korrektur â€” Meter aus OSD/Video
            var autoMeter2 = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = new ProtocolEntry();
            var explorerVm = CreateVsaCodeExplorerViewModel(entry, autoMeter2, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };

            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
            {
                ResumeDetection();
                return;
            }

            var selectedEntry = explorer.SelectedEntry;

            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var primary = _detectionPendingFindings[0];
            var timestampSecForFrame = _detectionPendingTimestampSec ?? timestampSec;
            var bbox = BBoxFromClockPosition(primary);

            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"det_corr_{annotationId}";

            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);
            try { System.IO.File.Delete(tempFrame); } catch { }

            var annotation = new TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = 0,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSecForFrame),
                ToolType = OverlayToolType.None,
                Points = new List<Domain.Models.NormalizedPoint>(),
                BoundingBox = bbox,
                ClockPosition = double.TryParse(primary.PositionClock, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = primary.WidthMm,
                HeightMm = primary.HeightMm
            };
            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"âœ“ Training: {selectedEntry.Code} (korrigiert)";

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
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"âœ— Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private void DetectionSkip_Click(object sender, RoutedEventArgs e)
    {
        ResumeDetection();
    }

    /// <summary>
    /// Erzeugt eine grobe BoundingBox aus Uhrposition + Ausdehnung eines LiveFrameFinding.
    /// Mapping: Uhrposition â†’ Kreissektor â†’ normalisierte Box im Bild.
    /// </summary>
    private static Application.Ai.NormalizedBoundingBox BBoxFromClockPosition(LiveFrameFinding finding)
    {
        // Uhrzeiger â†’ Winkel (12 Uhr = oben = -90Â°, dann im Uhrzeigersinn)
        double clockHour = 6; // Default: unten
        if (double.TryParse(finding.PositionClock, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            clockHour = parsed;

        double angleDeg = (clockHour / 12.0) * 360.0 - 90.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        // Extent-basierte Groesse (% Umfang â†’ Box-Groesse)
        double extent = (finding.ExtentPercent ?? 15) / 100.0;
        double boxSize = Math.Clamp(extent * 0.6, 0.08, 0.40);

        // Zentrum auf ~35% Radius vom Bildmittelpunkt
        double cx = 0.5 + 0.35 * Math.Cos(angleRad);
        double cy = 0.5 + 0.35 * Math.Sin(angleRad);

        return new Application.Ai.NormalizedBoundingBox
        {
            XCenter = Math.Clamp(cx, 0, 1),
            YCenter = Math.Clamp(cy, 0, 1),
            Width = Math.Clamp(boxSize, 0.08, 0.40),
            Height = Math.Clamp(boxSize, 0.08, 0.40)
        };
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

        var clockPosition = ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private static string ClickToClockPosition(Point click, Size canvasSize)
    {
        var cx = canvasSize.Width / 2.0;
        var cy = canvasSize.Height / 2.0;
        var dx = click.X - cx;
        var dy = click.Y - cy;

        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var clockAngle = (angleDeg + 90 + 360) % 360;
        var hour = (int)Math.Round(clockAngle / 30.0) % 12;
        if (hour == 0) hour = 12;

        return hour.ToString();
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
        // Resolve ServiceProvider: prefer injected, fallback to App.Services
        var sp = _serviceProvider ?? (App.Services as ServiceProvider);

        if (sp?.CodeCatalog is null)
        {
            MessageBox.Show(
                "Schadenscode-Katalog nicht verfuegbar.\n" +
                "Bitte die App neu starten oder KI-Einstellungen pruefen.",
                "Markieren", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Zeit = TimeSpan.FromSeconds(timestampSec),
        };

        if (!string.IsNullOrWhiteSpace(suggestedCode))
            entry.Code = suggestedCode;

        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        if (!string.IsNullOrWhiteSpace(clockPosition))
            entry.CodeMeta.Parameters["vsa.uhr.von"] = clockPosition;

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
