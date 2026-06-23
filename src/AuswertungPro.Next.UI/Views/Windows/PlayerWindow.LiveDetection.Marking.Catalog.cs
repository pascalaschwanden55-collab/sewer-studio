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
        // Eingabemarker nutzt CodingOverlayCanvas, nicht DetectionCanvas.
        if (!_isManualMarkMode)
            return;

        var clickPoint = e.GetPosition(DetectionCanvas);
        var canvasSize = new Size(DetectionCanvas.ActualWidth, DetectionCanvas.ActualHeight);

        if (canvasSize.Width < 60 || canvasSize.Height < 60)
            return;

        _player.SetPause(true);

        var clockPosition = LiveDetectionGeometryMapper.ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        _player.SetPause(true);
        OpenCodeCatalogForMark(
            finding.PositionClock,
            timestampSec,
            finding.VsaCodeHint);
    }

    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        LiveDetectionMarkCatalogWorkflowServiceFactory.Create(
                hasCodeCatalog: () => _serviceProvider?.CodeCatalog is not null,
                createViewModel: CreateVsaCodeExplorerViewModel,
                onEntryCreated: entry => _onEntryCreated?.Invoke(entry),
                showOverlay: message => ShowOverlay(message, TimeSpan.FromSeconds(4)))
            .TryOpen(
                clockPosition,
                timestampSec,
                suggestedCode,
                _codingLastOsdMeter ?? GetMeterFromVideoPosition(),
                _videoPath,
                this);
    }
}
