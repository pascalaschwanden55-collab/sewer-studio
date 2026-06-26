using System.Threading.Tasks;
using System.Windows;
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
        await LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync<Infrastructure.Ai.Pipeline.BoxSegmentationResult>(
            new LiveDetectionManualMarkCompletionCommandActions<Infrastructure.Ai.Pipeline.BoxSegmentationResult>(
                GetCurrentOverlay: () => _codingSessionHost.CurrentOverlay,
                GetTimestampSeconds: () => _playerTimelineHost.CurrentSecondsOrZero,
                CaptureCurrentFrameAsync: CaptureCurrentFrameAsync,
                EstimateClockPosition: LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter,
                SegmentMarkAsync: TrySegmentMarkBoxAsync,
                GetSegmentClockPosition: result => result.Quant.ClockPosition,
                ShowSegment: (result, overlay) => ShowMarkSamMask(result, overlay),
                DelayAfterSegmentAsync: LiveDetectionManualMarkCompletionCommandWorkflow.DelayAfterSegmentPreviewAsync,
                SaveTrainingAsync: SaveMarkAsTrainingAsync,
                CompleteManualMark: CompleteManualMark,
                TraceError: message => PlayerTrace.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {message}")));
    }

    private void CompleteManualMark(bool saved)
    {
        LiveDetectionManualMarkCompletionWorkflow.Execute(
            new LiveDetectionManualMarkCompletionWorkflowRequest(
                saved,
                _isCodingMode,
                _liveDetectionController.MarkToolType),
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

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
        => LiveDetectionOsdMeterStatusWorkflow.Show(
            new LiveDetectionOsdMeterStatusWorkflowRequest(
                Message: message,
                ResetAfterDelay: resetAfterDelay),
            new LiveDetectionOsdMeterStatusDisplayActions(
                ShowMessage: text => CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, text),
                GetLastMeter: () => _codingOsdMeterController.LastMeter,
                ShowMeter: meter => CodingOsdBadgeControls.ShowMeter(OsdMeterBadge, TxtOsdMeter, meter),
                HideBadge: () => CodingOsdBadgeControls.Hide(OsdMeterBadge)));
}
