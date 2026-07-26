using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDnCalibrationApplyWorkflowTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Execute_skips_when_required_state_is_missing(bool hasHaltungRecord, bool hasOverlayService)
    {
        var result = CodingDnCalibrationApplyWorkflow.Execute(
            new CodingDnCalibrationApplyWorkflowRequest(hasHaltungRecord, hasOverlayService),
            new CodingDnCalibrationApplyWorkflowActions(
                BuildCalibration: () => throw new InvalidOperationException("Calibration should not be built."),
                SetCalibration: _ => throw new InvalidOperationException("Overlay calibration should not be set."),
                ApplyCalibrationControls: _ => throw new InvalidOperationException("Controls should not be updated.")));

        Assert.Equal(CodingDnCalibrationApplyWorkflowOutcome.MissingRequiredState, result.Outcome);
        Assert.False(result.Applied);
    }

    [Fact]
    public void Execute_sets_overlay_calibration_before_updating_controls()
    {
        var calls = new List<string>();
        var calibration = new PipeCalibration { NominalDiameterMm = 300 };
        var state = new CodingDnCalibrationState(300, calibration, "DN: 300 mm", "Nicht kalibriert");

        var result = CodingDnCalibrationApplyWorkflow.Execute(
            new CodingDnCalibrationApplyWorkflowRequest(HasHaltungRecord: true, HasOverlayService: true),
            new CodingDnCalibrationApplyWorkflowActions(
                BuildCalibration: () =>
                {
                    calls.Add("build");
                    return state;
                },
                SetCalibration: applied =>
                {
                    Assert.Same(calibration, applied);
                    calls.Add("set");
                },
                ApplyCalibrationControls: applied =>
                {
                    Assert.Same(state, applied);
                    calls.Add("controls");
                }));

        Assert.Equal(CodingDnCalibrationApplyWorkflowOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
        Assert.Equal(["build", "set", "controls"], calls);
    }

    [Fact]
    public void Execute_updates_controls_without_overlay_calibration_when_dn_is_unknown()
    {
        var calls = new List<string>();
        var state = new CodingDnCalibrationState(0, null, "DN: unbekannt", "Nicht kalibriert");

        var result = CodingDnCalibrationApplyWorkflow.Execute(
            new CodingDnCalibrationApplyWorkflowRequest(HasHaltungRecord: true, HasOverlayService: true),
            new CodingDnCalibrationApplyWorkflowActions(
                BuildCalibration: () =>
                {
                    calls.Add("build");
                    return state;
                },
                SetCalibration: _ => throw new InvalidOperationException("Overlay calibration should not be set."),
                ApplyCalibrationControls: applied =>
                {
                    Assert.Same(state, applied);
                    calls.Add("controls");
                }));

        Assert.Equal(CodingDnCalibrationApplyWorkflowOutcome.Applied, result.Outcome);
        Assert.True(result.Applied);
        Assert.Equal(["build", "controls"], calls);
    }
}
