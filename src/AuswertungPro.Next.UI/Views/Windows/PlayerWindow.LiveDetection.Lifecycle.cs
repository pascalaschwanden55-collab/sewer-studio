using System;
using System.Threading.Tasks;
using System.Windows;
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
        await LiveDetectionStartupWorkflow.StartAsync(
            () => PlayerAiSettingsLoader.LoadRuntimeSettings(),
            settings => LiveDetectionRuntimeFactory.CreateAsync(settings),
            LiveDetectionDialogServiceFactory.Create(),
            new LiveDetectionStartupActions(
                UncheckToggle: () => LiveDetectionButton.IsChecked = false,
                StartRuntime: StartLiveDetectionRuntime));
    }

    private void StartLiveDetectionRuntime(LiveDetectionRuntime runtime)
    {
        _liveDetectionClient = runtime.Client;
        _liveDetectionService = runtime.Service;
        _liveDetectionModelName = runtime.VisionModel;
        _detectionCts = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_detectionCts);
        _isDetecting = true;

        LiveDetectionOverlayControls.Show(DetectionOverlayGrid);
        SetLiveDetectionBadge("KI aktiv", PlayerStatusColors.Success,
            $"Modell: {LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel)}");
        SetYoloStatus("Aktiv", PlayerStatusColors.Success, LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel));

        LiveDetectionStatusControls.ShowWaitingForFrame(LiveDetectionStatusText);

        _detectionTimer = PlayerWindowTimerFactory.CreateLiveDetectionTimer(DetectionTimer_Tick);
        _detectionTimer.Start();

        RunDetectionAsync().SafeFireAndForget("LiveDetection");
    }
}
