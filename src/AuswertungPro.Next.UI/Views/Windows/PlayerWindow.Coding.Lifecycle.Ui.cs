using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void PrepareCodingModePlayback()
    {
        CodingModePreparePlaybackWorkflow.Execute(
            new CodingModePreparePlaybackWorkflowRequest(_liveDetectionController.IsDetecting),
            new CodingModePreparePlaybackWorkflowActions(
                SetPause: _playerPlaybackControlHost.SetPause,
                StopLiveDetection: _liveDetectionStopController.Stop,
                UncheckLiveDetectionToggle: () => LiveDetectionToggleControls.Uncheck(LiveDetectionButton),
                HideLiveDetectionEntry: () => CodingModeChromeControls.HideLiveDetectionEntry(
                    LiveDetectionButton,
                    LiveDetectionStatusText)));
    }

    private void ActivateDefaultCodingTool()
    {
        CodingModeDefaultToolWorkflow.Execute(
            new CodingModeDefaultToolWorkflowRequest(_codingOverlayToolHost.HasOverlayService),
            new CodingModeDefaultToolWorkflowActions(
                SetMarkToolType: _liveDetectionController.SetMarkToolType,
                SetToolLabels: _markToolControls.SetToolLabels,
                SetOverlayActiveTool: tool => { _codingOverlayToolHost.SetActiveTool(tool); }));
    }

    private void ShowCodingModeUi()
    {
        CodingModeShowUiWorkflow.Execute(
            new CodingModeShowUiWorkflowActions(
                ShowCodingSurface: () => CodingModeChromeControls.ShowCodingSurface(
                    CodingOverlayPopup,
                    CodingOverlayCanvas,
                    CodingSidePanel,
                    CodingSidePanelColumn,
                    CodingToolbar,
                    GetCodingSidePanelWidth()),
                UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
                UpdateCodingOverlayCursor: UpdateCodingOverlayCursor,
                ScheduleLoadedViewportUpdate: () => PlayerDispatcherScheduler.ScheduleLoaded(
                    Dispatcher,
                    UpdateCodingOverlayViewport)));
    }

    private void StartCodingModeBackgroundServices()
    {
        CodingModeBackgroundServicesWorkflow.Execute(
            new CodingModeBackgroundServicesWorkflowActions(
                StartCodingAiInitialization: () => _codingPipelineHealthController.InitializeAsync().SafeFireAndForget("InitCodingAi"),
                StartCodingOsdTimer: StartCodingOsdTimer,
                ShowInitialOsdMeterBadge: () => CodingOsdBadgeControls.ShowInitial(OsdMeterBadge, TxtOsdMeter),
                StartSuggestionScan: StartSuggestionScan));
    }

    /// <summary>Wird in PlayerWindow.Coding.Suggestions.cs ausgefuellt.</summary>
    private void StartSuggestionScan() { }
}
