using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
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

        LiveDetectionButton.Visibility = Visibility.Collapsed;
        LiveDetectionStatusText.Visibility = Visibility.Collapsed;
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
        CodingOverlayPopup.IsOpen = true;
        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(GetCodingSidePanelWidth());
        CodingToolbar.Visibility = Visibility.Visible;
    }

    private void StartCodingModeBackgroundServices()
    {
        InitCodingAi().SafeFireAndForget("InitCodingAi");
        StartCodingOsdTimer();
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";
    }
}
