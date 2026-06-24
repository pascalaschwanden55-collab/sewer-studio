using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void PrepareCodingModePlayback()
    {
        PlayerCodingPlayback.PauseForCodingInteraction(pause => _player.SetPause(pause));

        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
        }

        CodingModeChromeControls.HideLiveDetectionEntry(LiveDetectionButton, LiveDetectionStatusText);
    }

    private void ActivateDefaultCodingTool()
    {
        _markToolType = OverlayToolType.Rectangle;
        TxtMarkToolName.Text = "Rechteck";
        TxtActiveToolLabel.Text = "Rechteck";
        if (_codingOverlayService != null)
            _codingOverlayService.ActiveTool = OverlayToolType.Rectangle;
    }

    private void ShowCodingModeUi()
    {
        CodingModeChromeControls.ShowCodingSurface(
            CodingOverlayPopup,
            CodingOverlayCanvas,
            CodingSidePanel,
            CodingSidePanelColumn,
            CodingToolbar,
            GetCodingSidePanelWidth());
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
    }

    private void StartCodingModeBackgroundServices()
    {
        InitCodingAi().SafeFireAndForget("InitCodingAi");
        StartCodingOsdTimer();
        CodingOsdBadgeControls.ShowInitial(OsdMeterBadge, TxtOsdMeter);
    }
}
