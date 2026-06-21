using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using Rectangle = System.Windows.Shapes.Rectangle;

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
            DialogHost.Current.Warn("KI-Konfiguration konnte nicht geladen werden.", "Schnell-Scan");
            QuickScanButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            DialogHost.Current.Info("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Schnell-Scan");
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

        var layout = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
            segment.TimestampSeconds,
            videoDurationSec,
            offsetX,
            trackWidth);

        var rect = new Rectangle
        {
            Width = layout.Width,
            Height = 6,
            RadiusX = 1,
            RadiusY = 1,
            Fill = new SolidColorBrush(LiveDetectionDisplayPolicy.QuickScanSeverityColor(segment.Severity, segment.HasDamage)),
            Cursor = Cursors.Hand,
            Opacity = segment.HasDamage ? 0.85 : 0.4
        };

        rect.ToolTip = LiveDetectionDisplayPolicy.BuildQuickScanTooltip(segment);

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

        Canvas.SetLeft(rect, layout.Left);
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

        var videoDuration = QuickScanHeatmapLayoutPolicy.EstimateDuration(
            _heatmapRects.Select(item => item.Seg));
        if (videoDuration <= 0)
            return;

        foreach (var (seg, rect) in _heatmapRects)
        {
            var layout = QuickScanHeatmapLayoutPolicy.CalculateSegmentLayout(
                seg.TimestampSeconds,
                videoDuration,
                offsetX,
                trackWidth);

            Canvas.SetLeft(rect, layout.Left);
            rect.Width = layout.Width;
        }
    }
}
