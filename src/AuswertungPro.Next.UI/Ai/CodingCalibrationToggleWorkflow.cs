using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingCalibrationToggleWorkflowOutcome
{
    PrerequisitesMissing,
    Applied
}

public sealed record CodingCalibrationToggleWorkflowRequest(
    bool HasOverlayService,
    bool HasViewModel,
    bool IsCurrentlyCalibrating);

public sealed record CodingCalibrationToggleWorkflowActions(
    Action CloseToolsDropdown,
    Action<bool> SetCalibrationState,
    Action ClearCalibrationStart,
    Action<OverlayToolType> SetActiveTool,
    Action<string?> SetActiveToolName,
    Action<string> ApplyActiveToolSelection,
    Action ClearCurrentOverlay,
    Action ClearOverlayInfo,
    Action<CodingCalibrationToggleState> ApplyToggleControls,
    Action UpdateOverlayCursor,
    Action<bool> RedrawCodingCanvas);

public sealed record CodingCalibrationToggleWorkflowResult(
    CodingCalibrationToggleWorkflowOutcome Outcome);

public static class CodingCalibrationToggleWorkflow
{
    public static CodingCalibrationToggleWorkflowResult Execute(
        CodingCalibrationToggleWorkflowRequest request,
        CodingCalibrationToggleWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingCalibrationToggleWorkflowOutcome.PrerequisitesMissing);

        actions.CloseToolsDropdown();

        var state = CodingCalibrationTogglePolicy.Build(request.IsCurrentlyCalibrating);
        actions.SetCalibrationState(state.IsCalibrating);
        actions.ClearCalibrationStart();
        actions.SetActiveTool(state.ActiveTool);
        actions.SetActiveToolName(state.ActiveToolName);
        actions.ApplyActiveToolSelection(state.ToolLabel);
        actions.ClearCurrentOverlay();
        actions.ClearOverlayInfo();
        actions.ApplyToggleControls(state);
        actions.UpdateOverlayCursor();
        actions.RedrawCodingCanvas(false);

        return Result(CodingCalibrationToggleWorkflowOutcome.Applied);
    }

    private static CodingCalibrationToggleWorkflowResult Result(
        CodingCalibrationToggleWorkflowOutcome outcome)
        => new(outcome);
}
