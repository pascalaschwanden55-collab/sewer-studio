using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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

            var visionModel = cfg.VisionModel;
            try
            {
                var models = await client.ListModelNamesAsync(CancellationToken.None);
                visionModel = VisionModelSelectionPolicy.Select(visionModel, models);
            }
            catch { /* use configured model */ }

            _liveDetectionClient = client;
            _liveDetectionService = new LiveDetectionService(client, visionModel);
            _liveDetectionModelName = visionModel;
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(visionModel));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _detectionTimer.Tick += DetectionTimer_Tick;
            _detectionTimer.Start();

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

        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", PlayerStatusColors.Muted);
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;

        var totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet \u2014 {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

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
}
