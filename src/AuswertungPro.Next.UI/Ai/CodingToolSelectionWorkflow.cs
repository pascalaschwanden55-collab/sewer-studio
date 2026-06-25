using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingToolSelectionWorkflowOutcome
{
    PrerequisitesMissing,
    Applied
}

public sealed record CodingToolSelectionWorkflowRequest(
    bool HasOverlayService,
    bool HasViewModel,
    string? CurrentToolName,
    string ButtonName,
    string? ButtonLabel,
    OverlayToolType RequestedTool,
    SchemaType? RequestedSchemaType,
    LevelMode? RequestedLevelMode);

public sealed record CodingToolSelectionWorkflowActions(
    Action ResetCalibration,
    Action CloseToolsDropdown,
    Action<string?> SetActiveToolName,
    Action<LevelMode> SetActiveLevelMode,
    Action<OverlayToolType> SetActiveTool,
    Action<SchemaType?> SetActiveSchemaType,
    Action CancelSchema,
    Action<string> ApplyActiveToolSelection,
    Action ClearCurrentOverlay,
    Action ClearOverlayInfo,
    Action UpdateOverlayCursor,
    Action<bool> RedrawCodingCanvas);

public sealed record CodingToolSelectionWorkflowResult(
    CodingToolSelectionWorkflowOutcome Outcome);

public static class CodingToolSelectionWorkflow
{
    public static CodingToolSelectionWorkflowResult Execute(
        CodingToolSelectionWorkflowRequest request,
        CodingToolSelectionWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasOverlayService || !request.HasViewModel)
            return Result(CodingToolSelectionWorkflowOutcome.PrerequisitesMissing);

        actions.ResetCalibration();
        actions.CloseToolsDropdown();

        var selection = CodingToolSelectionPolicy.Build(
            request.CurrentToolName,
            request.ButtonName,
            request.ButtonLabel,
            request.RequestedTool,
            request.RequestedSchemaType,
            request.RequestedLevelMode);

        actions.SetActiveToolName(selection.ActiveToolName);
        if (selection.LevelModeToApply.HasValue)
            actions.SetActiveLevelMode(selection.LevelModeToApply.Value);

        actions.SetActiveTool(selection.ActiveTool);
        actions.SetActiveSchemaType(selection.ActiveSchemaType);
        actions.CancelSchema();
        actions.ApplyActiveToolSelection(selection.LabelText);
        actions.ClearCurrentOverlay();
        actions.ClearOverlayInfo();
        actions.UpdateOverlayCursor();
        actions.RedrawCodingCanvas(false);

        return Result(CodingToolSelectionWorkflowOutcome.Applied);
    }

    private static CodingToolSelectionWorkflowResult Result(CodingToolSelectionWorkflowOutcome outcome)
        => new(outcome);
}
