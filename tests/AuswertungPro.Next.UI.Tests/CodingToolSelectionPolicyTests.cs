using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingToolSelectionPolicyTests
{
    [Fact]
    public void Build_activates_new_tool_and_preserves_schema()
    {
        var state = CodingToolSelectionPolicy.Build(
            currentToolName: null,
            buttonName: "BtnPipeBend",
            buttonLabel: "Bogen",
            requestedTool: OverlayToolType.PipeBend,
            requestedSchemaType: SchemaType.PipeBend,
            requestedLevelMode: null);

        Assert.True(state.IsActive);
        Assert.Equal("BtnPipeBend", state.ActiveToolName);
        Assert.Equal(OverlayToolType.PipeBend, state.ActiveTool);
        Assert.Equal(SchemaType.PipeBend, state.ActiveSchemaType);
        Assert.Null(state.LevelModeToApply);
        Assert.Equal("Bogen", state.LabelText);
    }

    [Fact]
    public void Build_deactivates_when_same_button_is_selected_again()
    {
        var state = CodingToolSelectionPolicy.Build(
            currentToolName: "BtnPipeBend",
            buttonName: "BtnPipeBend",
            buttonLabel: "Bogen",
            requestedTool: OverlayToolType.PipeBend,
            requestedSchemaType: SchemaType.PipeBend,
            requestedLevelMode: null);

        Assert.False(state.IsActive);
        Assert.Null(state.ActiveToolName);
        Assert.Equal(OverlayToolType.None, state.ActiveTool);
        Assert.Null(state.ActiveSchemaType);
        Assert.Null(state.LevelModeToApply);
        Assert.Equal("", state.LabelText);
    }

    [Fact]
    public void Build_applies_level_mode_only_when_activated()
    {
        var state = CodingToolSelectionPolicy.Build(
            currentToolName: "BtnOther",
            buttonName: "BtnWater",
            buttonLabel: null,
            requestedTool: OverlayToolType.Level,
            requestedSchemaType: SchemaType.FillLevel,
            requestedLevelMode: LevelMode.Water);

        Assert.True(state.IsActive);
        Assert.Equal(LevelMode.Water, state.LevelModeToApply);
        Assert.Equal("Level", state.LabelText);
    }
}
