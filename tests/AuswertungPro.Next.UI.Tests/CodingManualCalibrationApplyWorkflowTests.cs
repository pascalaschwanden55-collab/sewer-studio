using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualCalibrationApplyWorkflowTests
{
    [Fact]
    public void Execute_skips_when_overlay_service_is_missing()
    {
        var result = CodingManualCalibrationApplyWorkflow.Execute(
            new CodingManualCalibrationApplyWorkflowRequest(HasOverlayService: false),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(CodingManualCalibrationApplyWorkflowOutcome.PrerequisitesMissing, result.Outcome);
    }

    [Fact]
    public void Execute_builds_result_before_applying_manual_calibration()
    {
        var calls = new List<string>();
        var calibrationResult = ValidResult();

        var result = CodingManualCalibrationApplyWorkflow.Execute(
            new CodingManualCalibrationApplyWorkflowRequest(HasOverlayService: true),
            Actions(
                calls.Add,
                buildResult: () =>
                {
                    calls.Add("build");
                    return calibrationResult;
                },
                applyResult: resultToApply =>
                {
                    Assert.Same(calibrationResult, resultToApply);
                    calls.Add("apply");
                    return CodingManualCalibrationWorkflowOutcome.Applied;
                }));

        Assert.Equal(CodingManualCalibrationApplyWorkflowOutcome.Applied, result.Outcome);
        Assert.Equal(["build", "apply"], calls);
    }

    [Fact]
    public void Execute_maps_invalid_apply_outcome()
    {
        var calls = new List<string>();

        var result = CodingManualCalibrationApplyWorkflow.Execute(
            new CodingManualCalibrationApplyWorkflowRequest(HasOverlayService: true),
            Actions(
                calls.Add,
                applyResult: _ =>
                {
                    calls.Add("apply-invalid");
                    return CodingManualCalibrationWorkflowOutcome.Invalid;
                }));

        Assert.Equal(CodingManualCalibrationApplyWorkflowOutcome.Invalid, result.Outcome);
        Assert.Equal(["build", "apply-invalid"], calls);
    }

    private static CodingManualCalibrationApplyWorkflowActions Actions(
        Action<string> calls,
        Func<CodingManualCalibrationResult>? buildResult = null,
        Func<CodingManualCalibrationResult, CodingManualCalibrationWorkflowOutcome>? applyResult = null)
        => new(
            BuildResult: buildResult ?? (() =>
            {
                calls("build");
                return ValidResult();
            }),
            ApplyResult: applyResult ?? (_ =>
            {
                calls("apply");
                return CodingManualCalibrationWorkflowOutcome.Applied;
            }));

    private static CodingManualCalibrationResult ValidResult()
        => new(
            IsValid: true,
            Calibration: new PipeCalibration
            {
                NominalDiameterMm = 300,
                NormalizedDiameter = 0.6,
                PipePixelDiameter = 600,
                PipeCenter = new NormalizedPoint(0.5, 0.4),
                WasManuallyCalibrated = true,
                Source = CalibrationSource.Manual
            },
            StatusText: "Kalibriert: 500.0 mm/norm",
            HintText: "Kalibriert! DN 300mm = 600px");
}
