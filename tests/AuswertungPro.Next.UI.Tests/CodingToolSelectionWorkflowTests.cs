using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingToolSelectionWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_when_prerequisites_are_missing(bool hasOverlayService, bool hasViewModel)
    {
        var result = CodingToolSelectionWorkflow.Execute(
            new CodingToolSelectionWorkflowRequest(
                HasOverlayService: hasOverlayService,
                HasViewModel: hasViewModel,
                CurrentToolName: null,
                ButtonName: "BtnWater",
                ButtonLabel: "Wasser",
                RequestedTool: OverlayToolType.Level,
                RequestedSchemaType: SchemaType.FillLevel,
                RequestedLevelMode: LevelMode.Water),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingToolSelectionWorkflowOutcome.PrerequisitesMissing, result.Outcome);
    }

    [Fact]
    public void Execute_applies_new_tool_selection_in_order()
    {
        var calls = new List<string>();

        var result = CodingToolSelectionWorkflow.Execute(
            new CodingToolSelectionWorkflowRequest(
                HasOverlayService: true,
                HasViewModel: true,
                CurrentToolName: "BtnOther",
                ButtonName: "BtnWater",
                ButtonLabel: "Wasser",
                RequestedTool: OverlayToolType.Level,
                RequestedSchemaType: SchemaType.FillLevel,
                RequestedLevelMode: LevelMode.Water),
            Actions(calls.Add));

        Assert.Equal(CodingToolSelectionWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(
            [
                "reset-calibration",
                "close-dropdown",
                "active-name:BtnWater",
                "level:Water",
                "tool:Level",
                "schema:FillLevel",
                "cancel-schema",
                "apply-label:Wasser",
                "clear-current",
                "info:null",
                "cursor",
                "redraw:false"
            ],
            calls);
    }

    [Fact]
    public void Execute_deactivates_when_same_tool_is_selected_again()
    {
        var calls = new List<string>();

        var result = CodingToolSelectionWorkflow.Execute(
            new CodingToolSelectionWorkflowRequest(
                HasOverlayService: true,
                HasViewModel: true,
                CurrentToolName: "BtnWater",
                ButtonName: "BtnWater",
                ButtonLabel: "Wasser",
                RequestedTool: OverlayToolType.Level,
                RequestedSchemaType: SchemaType.FillLevel,
                RequestedLevelMode: LevelMode.Water),
            Actions(calls.Add));

        Assert.Equal(CodingToolSelectionWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(
            [
                "reset-calibration",
                "close-dropdown",
                "active-name:",
                "tool:None",
                "schema:",
                "cancel-schema",
                "apply-label:",
                "clear-current",
                "info:null",
                "cursor",
                "redraw:false"
            ],
            calls);
    }

    private static CodingToolSelectionWorkflowActions Actions(Action<string> calls)
        => new(
            ResetCalibration: () => calls("reset-calibration"),
            CloseToolsDropdown: () => calls("close-dropdown"),
            SetActiveToolName: name => calls($"active-name:{name}"),
            SetActiveLevelMode: mode => calls($"level:{mode}"),
            SetActiveTool: tool => calls($"tool:{tool}"),
            SetActiveSchemaType: schema => calls($"schema:{schema}"),
            CancelSchema: () => calls("cancel-schema"),
            ApplyActiveToolSelection: label => calls($"apply-label:{label}"),
            ClearCurrentOverlay: () => calls("clear-current"),
            ClearOverlayInfo: () => calls("info:null"),
            UpdateOverlayCursor: () => calls("cursor"),
            RedrawCodingCanvas: includeManualOverlay => calls($"redraw:{includeManualOverlay.ToString().ToLowerInvariant()}"));
}
