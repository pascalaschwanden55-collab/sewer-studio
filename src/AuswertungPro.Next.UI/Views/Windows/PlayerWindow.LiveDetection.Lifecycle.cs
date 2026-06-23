using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void LiveDetection_Click(object sender, RoutedEventArgs e)
        => HandleLiveDetectionClickAsync().SafeFireAndForget("LiveDetectionClick");

    private async Task HandleLiveDetectionClickAsync()
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
            cfg = PlayerAiSettingsLoader.LoadRuntimeSettings();
        }
        catch
        {
            LiveDetectionDialogServiceFactory.Create().ShowRuntimeSettingsLoadFailed();
            LiveDetectionButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            LiveDetectionDialogServiceFactory.Create().ShowDisabled();
            LiveDetectionButton.IsChecked = false;
            return;
        }

        try
        {
            var runtime = await LiveDetectionRuntimeFactory.CreateAsync(cfg);
            _liveDetectionClient = runtime.Client;
            _liveDetectionService = runtime.Service;
            _liveDetectionModelName = runtime.VisionModel;
            _detectionCts = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_detectionCts);
            _isDetecting = true;

            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
                $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel)}");
            SetYoloStatus("Aktiv", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = PlayerWindowTimerFactory.CreateLiveDetectionTimer(DetectionTimer_Tick);
            _detectionTimer.Start();

            RunDetectionAsync().SafeFireAndForget("LiveDetection");
        }
        catch (Exception ex)
        {
            LiveDetectionButton.IsChecked = false;
            LiveDetectionDialogServiceFactory.Create().ShowStartFailed(ex.Message);
        }
    }

}
