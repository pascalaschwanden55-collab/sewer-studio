using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingManualCalibrationWorkflowOutcome
{
    Invalid,
    Applied
}

public sealed record CodingManualCalibrationWorkflowRequest(
    CodingManualCalibrationResult Result,
    string? ActiveCodingToolName,
    bool IsCodingSchemaActive);

public sealed record CodingManualCalibrationWorkflowActions(
    Action<string> ShowInvalidHint,
    Action ClearCalibrationStart,
    Action<PipeCalibration> SetOverlayCalibration,
    Action<PipeCalibration> ApplySchemaCalibration,
    Action<CodingManualCalibrationResult> ApplyManualResult,
    Action EndCalibrationMode,
    Action ClearActiveToolName,
    Action HideHint,
    Action UpdateOverlayCursor,
    Action EnableCodingSchemaOverlay);

public static class CodingManualCalibrationWorkflow
{
    public static CodingManualCalibrationWorkflowOutcome Apply(
        CodingManualCalibrationWorkflowRequest request,
        CodingManualCalibrationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var result = request.Result;
        if (!result.IsValid || result.Calibration == null)
        {
            actions.ShowInvalidHint(result.HintText);
            actions.ClearCalibrationStart();
            return CodingManualCalibrationWorkflowOutcome.Invalid;
        }

        var calibration = result.Calibration;
        actions.SetOverlayCalibration(calibration);
        actions.ApplySchemaCalibration(calibration);
        actions.ApplyManualResult(result);
        actions.EndCalibrationMode();

        if (string.Equals(
                request.ActiveCodingToolName,
                CodingCalibrationTogglePolicy.CalibrateButtonName,
                StringComparison.Ordinal))
        {
            actions.ClearActiveToolName();
        }

        actions.HideHint();
        actions.UpdateOverlayCursor();

        if (request.IsCodingSchemaActive)
            actions.EnableCodingSchemaOverlay();

        return CodingManualCalibrationWorkflowOutcome.Applied;
    }
}
