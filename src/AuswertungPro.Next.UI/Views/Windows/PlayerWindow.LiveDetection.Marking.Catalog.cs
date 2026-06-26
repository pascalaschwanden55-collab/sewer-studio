using System;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void DetectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var result = LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick(
            new LiveDetectionMarkCatalogCanvasClickRequest(
                _liveDetectionController.IsManualMarkMode,
                e.GetPosition(DetectionCanvas),
                new Size(DetectionCanvas.ActualWidth, DetectionCanvas.ActualHeight),
                _playerTimelineHost.CurrentSecondsOrZero),
            new LiveDetectionMarkCatalogOpenWorkflowActions(
                SetPause: _playerPlaybackControlHost.SetPause,
                OpenCodeCatalog: OpenCodeCatalogForMark));

        e.Handled = result.Handled;
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        LiveDetectionMarkCatalogOpenWorkflow.ExecuteFindingClick(
            new LiveDetectionMarkCatalogFindingClickRequest(
                finding.PositionClock,
                timestampSec,
                finding.VsaCodeHint),
            new LiveDetectionMarkCatalogOpenWorkflowActions(
                SetPause: _playerPlaybackControlHost.SetPause,
                OpenCodeCatalog: OpenCodeCatalogForMark));
    }

    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        LiveDetectionMarkCatalogDisplayWorkflow.TryOpen(
            new LiveDetectionMarkCatalogDisplayRequest(
                clockPosition,
                timestampSec,
                suggestedCode,
                _codingOsdMeterController.LastMeter ?? GetMeterFromVideoPosition(),
                _playbackContext.VideoPath,
                this),
            new LiveDetectionMarkCatalogDisplayActions(
                HasCodeCatalog: () => _protocolContext.HasCodeCatalog,
                CreateViewModel: CreateVsaCodeExplorerViewModel,
                OnEntryCreated: _protocolContext.NotifyEntryCreated,
                ShowOverlay: message => ShowOverlay(message, TimeSpan.FromSeconds(4))));
    }
}
