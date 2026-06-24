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
                _codingSessionService?.ActiveSession?.Events,
                _codingSessionHost.Events,
                _codingImportEvents),
            currentMeter,
            currentVideoTime);
    }

    private bool IsFindingTooFarAhead(LiveFrameFinding finding)
    {
        return CodingFindingProximityPolicy.IsTooFarAhead(
            finding,
            _codingOverlayService?.Calibration,
            _codingVideoAspect);
    }

    private IReadOnlyList<SegmentedFinding> BuildCodingSegmentedFindings(SingleFrameResult mmResult)
    {
        if (mmResult.SamResponse == null)
            return Array.Empty<SegmentedFinding>();

        var proximityCalibration = CodingPipeProximityCalibrationPolicy.Resolve(
            _codingOverlayService?.Calibration);

        return SegmentedFindingBuilder.Build(
            mmResult.SamResponse,
            mmResult.DinoDetections,
            mmResult.QuantifiedMasks,
            proximityCalibration.VanishX,
            proximityCalibration.VanishY,
            proximityCalibration.PipeRadiusNorm,
            MetrierungProximityThresholds.Default);
    }

    private Task<byte[]?> CaptureSnapshotAsync(CancellationToken ct)
        => CodingSnapshotCaptureFactory.CapturePngAsync(path => TakeSnapshotSafe(path), ct);
}
