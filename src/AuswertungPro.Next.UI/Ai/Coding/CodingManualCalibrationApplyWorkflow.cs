namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingManualCalibrationApplyWorkflowOutcome
{
    PrerequisitesMissing,
    Invalid,
    Applied
}

public sealed record CodingManualCalibrationApplyWorkflowRequest(bool HasOverlayService);

public sealed record CodingManualCalibrationApplyWorkflowActions(
    Func<CodingManualCalibrationResult> BuildResult,
    Func<CodingManualCalibrationResult, CodingManualCalibrationWorkflowOutcome> ApplyResult);

public sealed record CodingManualCalibrationApplyWorkflowResult(
    CodingManualCalibrationApplyWorkflowOutcome Outcome);

public static class CodingManualCalibrationApplyWorkflow
{
    public static CodingManualCalibrationApplyWorkflowResult Execute(
        CodingManualCalibrationApplyWorkflowRequest request,
        CodingManualCalibrationApplyWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService)
            return Result(CodingManualCalibrationApplyWorkflowOutcome.PrerequisitesMissing);

        var calibrationResult = actions.BuildResult();
        var applyOutcome = actions.ApplyResult(calibrationResult);

        return Result(
            applyOutcome == CodingManualCalibrationWorkflowOutcome.Applied
                ? CodingManualCalibrationApplyWorkflowOutcome.Applied
                : CodingManualCalibrationApplyWorkflowOutcome.Invalid);
    }

    private static CodingManualCalibrationApplyWorkflowResult Result(
        CodingManualCalibrationApplyWorkflowOutcome outcome)
        => new(outcome);
}
