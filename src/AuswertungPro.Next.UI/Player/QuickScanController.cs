using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace AuswertungPro.Next.UI.Player;

public sealed class QuickScanController
{
    private readonly Canvas _heatmapCanvas;
    private readonly ToggleButton _quickScanButton;
    private readonly TextBlock _quickScanStatusText;
    private readonly PlayerPlaybackControlHost _playbackControlHost;
    private readonly PlayerTimelineHost _timelineHost;
    private readonly string _videoPath;
    private readonly Action _ensurePlaying;
    private readonly Action _updateUi;
    private readonly Func<(double offsetX, double trackWidth)> _getSliderTrackBounds;
    private readonly IProcessOutputReader _processOutputs;
    private readonly IDialogService _dialogs;

    private CancellationTokenSource? _quickScanCts;
    private bool _isQuickScanning;
    private readonly List<(QuickScanSegment Seg, Rectangle Rect)> _heatmapRects = new();

    public QuickScanController(
        Canvas heatmapCanvas,
        ToggleButton quickScanButton,
        TextBlock quickScanStatusText,
        PlayerPlaybackControlHost playbackControlHost,
        PlayerTimelineHost timelineHost,
        string videoPath,
        Action ensurePlaying,
        Action updateUi,
        Func<(double offsetX, double trackWidth)> getSliderTrackBounds,
        IProcessOutputReader processOutputs,
        IDialogService dialogs)
    {
        _heatmapCanvas = heatmapCanvas ?? throw new ArgumentNullException(nameof(heatmapCanvas));
        _quickScanButton = quickScanButton ?? throw new ArgumentNullException(nameof(quickScanButton));
        _quickScanStatusText = quickScanStatusText ?? throw new ArgumentNullException(nameof(quickScanStatusText));
        _playbackControlHost = playbackControlHost ?? throw new ArgumentNullException(nameof(playbackControlHost));
        _timelineHost = timelineHost ?? throw new ArgumentNullException(nameof(timelineHost));
        _videoPath = videoPath ?? throw new ArgumentNullException(nameof(videoPath));
        _ensurePlaying = ensurePlaying ?? throw new ArgumentNullException(nameof(ensurePlaying));
        _updateUi = updateUi ?? throw new ArgumentNullException(nameof(updateUi));
        _getSliderTrackBounds = getSliderTrackBounds ?? throw new ArgumentNullException(nameof(getSliderTrackBounds));
        _processOutputs = processOutputs ?? throw new ArgumentNullException(nameof(processOutputs));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    /// <summary>Bricht einen laufenden Scan ab (null-safe). Vom Window-Teardown aufgerufen.</summary>
    public void Cancel() => _quickScanCts?.Cancel();

    public async Task ToggleAsync()
    {
        if (_isQuickScanning)
        {
            _quickScanCts?.Cancel();
            _quickScanButton.IsChecked = false;
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
            _dialogs.Warn("KI-Konfiguration konnte nicht geladen werden.", "Schnell-Scan");
            _quickScanButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            _dialogs.Info("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Schnell-Scan");
            _quickScanButton.IsChecked = false;
            return;
        }

        var ffmpegPath = cfg.FfmpegPath ?? FfmpegLocator.ResolveFfmpeg();
        using var client = new OllamaClient(cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
            keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);
        var service = new QuickScanService(
            client,
            cfg.VisionModel,
            ffmpegPath,
            _processOutputs);

        _quickScanCts = new CancellationTokenSource();
        _isQuickScanning = true;

        _heatmapCanvas.Children.Clear();
        _heatmapRects.Clear();

        _quickScanStatusText.Visibility = Visibility.Visible;
        _quickScanStatusText.Text = "Starte...";

        var progress = new Progress<QuickScanProgress>(p =>
        {
            _quickScanStatusText.Text = p.Status;
            if (p.LatestSegment is { } seg)
                AddHeatmapSegment(seg, p.FramesTotal * 5.0); // estimate duration
        });

        try
        {
            var result = await service.ScanAsync(_videoPath, progress, _quickScanCts.Token);

            // Rebuild heatmap with exact duration
            _heatmapCanvas.Children.Clear();
            _heatmapRects.Clear();
            foreach (var seg in result.Segments)
                AddHeatmapSegment(seg, result.VideoDurationSeconds);

            _quickScanStatusText.Text = result.Error ?? $"Fertig: {result.FramesAnalyzed} Frames analysiert";
        }
        catch (OperationCanceledException)
        {
            _quickScanStatusText.Text = "Abgebrochen";
        }
        catch (Exception ex)
        {
            _quickScanStatusText.Text = "Fehler: "
                                        + UserError.DescribeAndReport(ex, "KI-Schnellscan");
        }
        finally
        {
            _isQuickScanning = false;
            _quickScanButton.IsChecked = false;
            _quickScanCts?.Dispose();
            _quickScanCts = null;

            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            hideTimer.Tick += (_, _) =>
            {
                hideTimer.Stop();
                if (!_isQuickScanning)
                    _quickScanStatusText.Visibility = Visibility.Collapsed;
            };
            hideTimer.Start();
        }
    }

    private void AddHeatmapSegment(QuickScanSegment segment, double videoDurationSec)
    {
        if (videoDurationSec <= 0)
            return;

        var (offsetX, trackWidth) = _getSliderTrackBounds();
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
            _ensurePlaying();
            _playbackControlHost.SetPause(true);
            var length = _timelineHost.LengthMilliseconds ?? 0;
            if (length > 0)
            {
                var targetMs = (long)(timestampSec * 1000);
                if (targetMs > length) targetMs = length;
                _timelineHost.SeekMilliseconds(targetMs);
            }
            _updateUi();
        };

        Canvas.SetLeft(rect, layout.Left);
        Canvas.SetTop(rect, 0);

        _heatmapCanvas.Children.Add(rect);
        _heatmapRects.Add((segment, rect));
    }

    public void Reposition()
    {
        if (_heatmapRects.Count == 0)
            return;

        var (offsetX, trackWidth) = _getSliderTrackBounds();
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
