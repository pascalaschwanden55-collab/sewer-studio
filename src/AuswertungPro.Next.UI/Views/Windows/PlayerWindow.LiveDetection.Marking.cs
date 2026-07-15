using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ManualMark_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.ToggleManualMarkPopup(_codingModeState.IsCodingMode);

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.ToggleToolsDropdown();

    private void MarkTool_Punkt_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.Activate(OverlayToolType.Point, "Punkt");

    private void MarkTool_Ellipse_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.Activate(OverlayToolType.Ellipse, "Ellipse");

    private void MarkTool_Freihand_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.Activate(OverlayToolType.Freehand, "Freihand");

    private void MarkTool_Rechteck_Click(object sender, RoutedEventArgs e)
        => _liveDetectionMarkToolController.Activate(OverlayToolType.Rectangle, "Rechteck");

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
                _codingModeState.IsCodingMode,
                _liveDetectionController.MarkToolType),
            new LiveDetectionManualMarkCompletionWorkflowActions(
                ClearSamMasks: () => CodingSamMaskOverlayController.Clear(CodingOverlayCanvas),
                ClearBendMarker: () => CodingBendMarkerOverlayController.Clear(CodingOverlayCanvas),
                ClearCurrentOverlay: _codingSessionHost.ClearCurrentOverlay,
                RedrawCodingCanvasWithoutManualOverlay: () => RedrawCodingCanvas(includeManualOverlay: false),
                DeactivateMarkTool: _liveDetectionMarkToolController.Deactivate,
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
