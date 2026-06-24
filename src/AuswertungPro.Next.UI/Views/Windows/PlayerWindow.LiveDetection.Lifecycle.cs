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
            LiveDetectionToggleControls.Uncheck(LiveDetectionButton);
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
                UncheckToggle: () => LiveDetectionToggleControls.Uncheck(LiveDetectionButton),
                StartRuntime: StartLiveDetectionRuntime));
    }

    private void StartLiveDetectionRuntime(LiveDetectionRuntime runtime)
        => LiveDetectionRuntimeStartWorkflow.Start(
            runtime,
            new LiveDetectionRuntimeStartActions(
                StoreRuntime: StoreLiveDetectionRuntime,
                ResetCancellation: () => _detectionCts = CancellationTokenSourceLifecycle.CancelPreviousAndCreate(_detectionCts),
                MarkDetecting: () => _isDetecting = true,
                ShowOverlay: () => LiveDetectionOverlayControls.Show(DetectionOverlayGrid),
                ApplyActiveStatus: ApplyLiveDetectionRuntimeStartStatus,
                ShowWaitingForFrame: () => LiveDetectionStatusControls.ShowWaitingForFrame(LiveDetectionStatusText),
                StartTimer: StartLiveDetectionTimer,
                RunFirstDetection: () => RunDetectionAsync().SafeFireAndForget("LiveDetection")));

    private void StoreLiveDetectionRuntime(LiveDetectionRuntime runtime)
    {
        _liveDetectionClient = runtime.Client;
        _liveDetectionService = runtime.Service;
        _liveDetectionModelName = runtime.VisionModel;
    }

    private void ApplyLiveDetectionRuntimeStartStatus(LiveDetectionRuntimeStartStatus status)
    {
        SetLiveDetectionBadge(status.BadgeText, status.StatusColor, status.BadgeDetails);
        SetYoloStatus(status.YoloText, status.StatusColor, status.ModelLabel);
    }

    private void StartLiveDetectionTimer()
    {
        _detectionTimer = PlayerWindowTimerFactory.CreateLiveDetectionTimer(DetectionTimer_Tick);
        _detectionTimer.Start();
    }
}
