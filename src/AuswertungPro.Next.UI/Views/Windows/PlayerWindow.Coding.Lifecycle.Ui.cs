using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
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
                SetPause: pause => _player.SetPause(pause),
                StopLiveDetection: StopLiveDetection,
                UncheckLiveDetectionToggle: () => LiveDetectionToggleControls.Uncheck(LiveDetectionButton),
                HideLiveDetectionEntry: () => CodingModeChromeControls.HideLiveDetectionEntry(
                    LiveDetectionButton,
                    LiveDetectionStatusText)));
    }

    private void ActivateDefaultCodingTool()
    {
        CodingModeDefaultToolWorkflow.Execute(
            new CodingModeDefaultToolWorkflowRequest(_codingOverlayService is not null),
            new CodingModeDefaultToolWorkflowActions(
                SetMarkToolType: tool => _markToolType = tool,
                SetToolLabels: _markToolControls.SetToolLabels,
                SetOverlayActiveTool: tool => _codingOverlayService!.ActiveTool = tool));
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
                ScheduleLoadedViewportUpdate: () => Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(UpdateCodingOverlayViewport))));
    }

    private void StartCodingModeBackgroundServices()
    {
        CodingModeBackgroundServicesWorkflow.Execute(
            new CodingModeBackgroundServicesWorkflowActions(
                StartCodingAiInitialization: () => InitCodingAi().SafeFireAndForget("InitCodingAi"),
                StartCodingOsdTimer: StartCodingOsdTimer,
                ShowInitialOsdMeterBadge: () => CodingOsdBadgeControls.ShowInitial(OsdMeterBadge, TxtOsdMeter)));
    }
}
