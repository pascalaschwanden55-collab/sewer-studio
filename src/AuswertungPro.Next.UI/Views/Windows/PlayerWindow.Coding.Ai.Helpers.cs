using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private bool IsCodingAfterTerminalBoundary(double? currentMeter, TimeSpan currentVideoTime)
    {
        return CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            CodingTerminalBoundaryCandidateBuilder.Enumerate(
                _codingSessionRuntimeOwner.Service?.ActiveSession?.Events,
                _codingSessionHost.Events,
                _codingImportEvents),
            currentMeter,
            currentVideoTime);
    }

    private bool IsFindingTooFarAhead(LiveFrameFinding finding)
    {
        return CodingFindingProximityPolicy.IsTooFarAhead(
            finding,
            _codingOverlayToolHost.Calibration,
            _codingOverlayRenderState.VideoAspect);
    }

    private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings(SingleFrameResult mmResult)
    {
        var result = CodingSegmentedFindingsBuildWorkflow.Execute(
            new CodingSegmentedFindingsBuildRequest(
                Result: mmResult,
                Calibration: _codingOverlayToolHost.Calibration),
            new CodingSegmentedFindingsBuildActions(
                BuildSegmentedFindings: (samResponse, dinoDetections, quantifiedMasks, proximityCalibration) =>
                    SegmentedFindingBuilder.Build(
                        samResponse,
                        dinoDetections,
                        quantifiedMasks,
                        proximityCalibration.VanishX,
                        proximityCalibration.VanishY,
                        proximityCalibration.PipeRadiusNorm,
                        MetrierungProximityThresholds.Default)));

        return result.Segmented;
    }

    private Task<byte[]?> CaptureSnapshotAsync(CancellationToken ct)
        => CodingSnapshotCaptureFactory.CapturePngAsync(path => TakeSnapshotSafe(path), ct);
}
