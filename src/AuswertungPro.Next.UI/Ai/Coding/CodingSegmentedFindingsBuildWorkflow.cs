using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingSegmentedFindingsBuildWorkflowOutcome
{
    NoSamResponse,
    Built
}

public sealed record CodingSegmentedFindingsBuildRequest(
    SingleFrameResult Result,
    PipeCalibration? Calibration);

public sealed record CodingSegmentedFindingsBuildActions(
    Func<
        SamResponse,
        IReadOnlyList<DinoDetectionDto>,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask>,
        CodingPipeProximityCalibration,
        IReadOnlyList<SegmentedFinding>> BuildSegmentedFindings);

public sealed record CodingSegmentedFindingsBuildWorkflowResult(
    CodingSegmentedFindingsBuildWorkflowOutcome Outcome,
    IReadOnlyList<SegmentedFinding> Segmented);

public static class CodingSegmentedFindingsBuildWorkflow
{
    public static CodingSegmentedFindingsBuildWorkflowResult Execute(
        CodingSegmentedFindingsBuildRequest request,
        CodingSegmentedFindingsBuildActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        var samResponse = request.Result.SamResponse;
        if (samResponse == null)
            return Result(CodingSegmentedFindingsBuildWorkflowOutcome.NoSamResponse, []);

        var proximityCalibration = CodingPipeProximityCalibrationPolicy.Resolve(request.Calibration);
        var segmented = actions.BuildSegmentedFindings(
            samResponse,
            request.Result.DinoDetections,
            request.Result.QuantifiedMasks,
            proximityCalibration);

        return Result(CodingSegmentedFindingsBuildWorkflowOutcome.Built, segmented);
    }

    private static CodingSegmentedFindingsBuildWorkflowResult Result(
        CodingSegmentedFindingsBuildWorkflowOutcome outcome,
        IReadOnlyList<SegmentedFinding> segmented)
        => new(outcome, segmented);
}
