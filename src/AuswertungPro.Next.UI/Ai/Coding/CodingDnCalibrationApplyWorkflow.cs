using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingDnCalibrationApplyWorkflowOutcome
{
    MissingRequiredState,
    Applied
}

public sealed record CodingDnCalibrationApplyWorkflowRequest(
    bool HasHaltungRecord,
    bool HasOverlayService);

public sealed record CodingDnCalibrationApplyWorkflowActions(
    Func<CodingDnCalibrationState> BuildCalibration,
    Action<PipeCalibration> SetCalibration,
    Action<CodingDnCalibrationState> ApplyCalibrationControls);

public sealed record CodingDnCalibrationApplyWorkflowResult(
    CodingDnCalibrationApplyWorkflowOutcome Outcome)
{
    public bool Applied => Outcome == CodingDnCalibrationApplyWorkflowOutcome.Applied;
}

public static class CodingDnCalibrationApplyWorkflow
{
    public static CodingDnCalibrationApplyWorkflowResult Execute(
        CodingDnCalibrationApplyWorkflowRequest request,
        CodingDnCalibrationApplyWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasHaltungRecord || !request.HasOverlayService)
            return Result(CodingDnCalibrationApplyWorkflowOutcome.MissingRequiredState);

        var dnCalibration = actions.BuildCalibration();
        if (dnCalibration.Calibration != null)
            actions.SetCalibration(dnCalibration.Calibration);

        actions.ApplyCalibrationControls(dnCalibration);
        return Result(CodingDnCalibrationApplyWorkflowOutcome.Applied);
    }

    private static CodingDnCalibrationApplyWorkflowResult Result(
        CodingDnCalibrationApplyWorkflowOutcome outcome)
        => new(outcome);
}
