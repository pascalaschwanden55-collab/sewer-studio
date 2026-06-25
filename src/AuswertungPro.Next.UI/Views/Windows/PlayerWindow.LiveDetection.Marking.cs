using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Nach abgeschlossener Markierung: Code-Katalog oeffnen und Training speichern.
    /// </summary>
    private void HandleMarkDrawingComplete()
    {
        HandleMarkDrawingCompleteAsync().SafeFireAndForget("MarkDrawingComplete");
    }

    private async Task HandleMarkDrawingCompleteAsync()
    {
        try
        {
            var overlay = _codingSessionHost.CurrentOverlay;
            if (overlay == null)
                return;

            var timestampSec = _playerTimelineHost.CurrentSecondsOrZero;
            var frameBytes = await CaptureCurrentFrameAsync();

            string? clockPos = LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(overlay);

            var samResult = await TrySegmentMarkBoxAsync(overlay, frameBytes);
            if (!string.IsNullOrEmpty(samResult?.Quant.ClockPosition))
                clockPos = samResult!.Quant.ClockPosition;

            if (samResult != null)
            {
                ShowMarkSamMask(samResult, overlay);
                // SAM-Maske kurz sichtbar lassen, bevor der Code-Dialog oeffnet.
                await Task.Delay(3000);
            }

            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos, frameBytes);

            LiveDetectionManualMarkCompletionWorkflow.Execute(
                new LiveDetectionManualMarkCompletionWorkflowRequest(
                    saved,
                    _isCodingMode,
                    _markToolType),
                new LiveDetectionManualMarkCompletionWorkflowActions(
                    ClearSamMasks: () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                    ClearBendMarker: () => CodingBendMarkerOverlayController.Clear(CodingOverlayCanvas),
                    ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                    RedrawCodingCanvasWithoutManualOverlay: () => RedrawCodingCanvas(includeManualOverlay: false),
                    DeactivateMarkTool: DeactivateMarkTool,
                    SetActiveTool: tool => _codingOverlayToolHost.SetActiveTool(tool),
                    ApplyCrossCursor: () => CodingOverlayInputControls.ApplyCanvasCursor(
                        CodingOverlayCanvas,
                        useCrossCursor: true)));
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
    {
        CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, message);

        if (!resetAfterDelay)
            return;

        var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(3), () =>
        {
            if (_codingOsdMeterController.LastMeter.HasValue)
                CodingOsdBadgeControls.ShowMeter(OsdMeterBadge, TxtOsdMeter, _codingOsdMeterController.LastMeter.Value);
            else
                CodingOsdBadgeControls.Hide(OsdMeterBadge);
        });
        resetTimer.Start();
    }
}
