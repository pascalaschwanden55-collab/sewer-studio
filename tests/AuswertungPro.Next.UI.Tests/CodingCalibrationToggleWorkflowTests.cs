using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationToggleWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_when_prerequisites_are_missing(bool hasOverlayService, bool hasViewModel)
    {
        var result = CodingCalibrationToggleWorkflow.Execute(
            new CodingCalibrationToggleWorkflowRequest(
                HasOverlayService: hasOverlayService,
                HasViewModel: hasViewModel,
                IsCurrentlyCalibrating: false),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingCalibrationToggleWorkflowOutcome.PrerequisitesMissing, result.Outcome);
    }

    [Fact]
    public void Execute_enables_calibration_in_order()
    {
        var calls = new List<string>();

        var result = CodingCalibrationToggleWorkflow.Execute(
            new CodingCalibrationToggleWorkflowRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsCurrentlyCalibrating: false),
            Actions(calls.Add));

        Assert.Equal(CodingCalibrationToggleWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(
            [
                "close-dropdown",
                "calibrating:true",
                "clear-start",
                "tool:None",
                "active-name:BtnCodingCalibrate",
                "apply-label:Kalibrieren",
                "clear-current",
                "info:null",
                "toggle:true",
                "cursor",
                "redraw:false"
            ],
            calls);
    }

    [Fact]
    public void Execute_disables_calibration_in_order()
    {
        var calls = new List<string>();

        var result = CodingCalibrationToggleWorkflow.Execute(
            new CodingCalibrationToggleWorkflowRequest(
                HasOverlayService: true,
                HasViewModel: true,
                IsCurrentlyCalibrating: true),
            Actions(calls.Add));

        Assert.Equal(CodingCalibrationToggleWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(
            [
                "close-dropdown",
                "calibrating:false",
                "clear-start",
                "tool:None",
                "active-name:",
                "apply-label:",
                "clear-current",
                "info:null",
                "toggle:false",
                "cursor",
                "redraw:false"
            ],
            calls);
    }

    private static CodingCalibrationToggleWorkflowActions Actions(Action<string> calls)
        => new(
            CloseToolsDropdown: () => calls("close-dropdown"),
            SetCalibrationState: isCalibrating => calls($"calibrating:{isCalibrating.ToString().ToLowerInvariant()}"),
            ClearCalibrationStart: () => calls("clear-start"),
            SetActiveTool: tool => calls($"tool:{tool}"),
            SetActiveToolName: activeToolName => calls($"active-name:{activeToolName}"),
            ApplyActiveToolSelection: label => calls($"apply-label:{label}"),
            ClearCurrentOverlay: () => calls("clear-current"),
            ClearOverlayInfo: () => calls("info:null"),
            ApplyToggleControls: state => calls($"toggle:{state.IsCalibrating.ToString().ToLowerInvariant()}"),
            UpdateOverlayCursor: () => calls("cursor"),
            RedrawCodingCanvas: includeManualOverlay => calls($"redraw:{includeManualOverlay.ToString().ToLowerInvariant()}"));
}
