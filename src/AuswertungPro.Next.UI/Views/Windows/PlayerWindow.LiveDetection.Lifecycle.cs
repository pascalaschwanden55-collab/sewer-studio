using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
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
            var runtime = await LiveDetectionRuntimeFactory.CreateAsync(cfg);
            _liveDetectionClient = runtime.Client;
            _liveDetectionService = runtime.Service;
            _liveDetectionModelName = runtime.VisionModel;
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel)}");
            SetYoloStatus("Aktiv", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel));

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

}
