using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private IReadOnlyList<(string Code, string Description, double Meter)>? GatherImportContext()
        => CodingImportContextBuilder.Build(_codingImportEvents);

    private void ShowCodingAiResults(LiveDetection result)
    {
        CodingAiResultWorkflow.Execute(
            new CodingAiResultWorkflowRequest(
                result,
                _codingAiController.ModelName,
                _codingSessionHost.HasViewModel,
                CodingOverlayPopup.IsOpen,
                _player.Time / 1000.0),
            new CodingAiResultWorkflowActions(
                (status, color, detail) => SetCodingAiState(status, color, detail),
                () => DetectionOverlayCleaner.ClearFindings(CodingFindingsList),
                () => DetectionOverlayCleaner.ClearFindingsAndCanvas(DetectionCanvas, CodingFindingsList),
                () => DetectionOverlayCleaner.ClearVisuals(DetectionCanvas, DetectionOverlayGrid),
                UpdateFrameReadiness,
                IsFrameReady,
                pending => _codingFrameReadinessController.StorePendingWarmupResult(pending),
                () => _codingFrameReadinessController.SkippedFrames,
                _codingFrameReadinessController.SelectReadyResult,
                CodingOsdMeterStateWorkflow.FromDetectionResult,
                ApplyCodingOsdMeterState,
                meter => _codingSessionService?.MoveToMeter(meter),
                ResolveCodingMeterForFrame,
                FilterValidFindings,
                findings => CodingFindingsListControls.ShowFindings(CodingFindingsList, findings),
                (findings, currentMeter) => CodingNewFindingOverlaySelector.Select(
                    findings,
                    currentMeter,
                    IsFindingAlreadyKnown),
                AddAiFindingsAsEvents,
                () => LiveDetectionOverlayControls.Show(DetectionOverlayGrid),
                RenderDetectionOverlay,
                ScheduleDetectionAutoHide));
    }

}
