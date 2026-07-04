using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualCalibrationWorkflowTests
{
    [Fact]
    public void Apply_rejects_invalid_result_and_clears_start_only()
    {
        var calls = new List<string>();
        var result = new CodingManualCalibrationResult(
            IsValid: false,
            Calibration: null,
            StatusText: "",
            HintText: "Linie zu kurz - bitte nochmal");

        var outcome = CodingManualCalibrationWorkflow.Apply(
            new CodingManualCalibrationWorkflowRequest(
                result,
                ActiveCodingToolName: CodingCalibrationTogglePolicy.CalibrateButtonName,
                IsCodingSchemaActive: true),
            Actions(calls));

        Assert.Equal(CodingManualCalibrationWorkflowOutcome.Invalid, outcome);
        Assert.Equal(
            [
                "hint:Linie zu kurz - bitte nochmal",
                "clear-start"
            ],
            calls);
    }

    [Fact]
    public void Apply_applies_valid_result_and_clears_calibration_tool_when_active()
    {
        var calls = new List<string>();
        var calibration = Calibration();
        var result = new CodingManualCalibrationResult(
            IsValid: true,
            Calibration: calibration,
            StatusText: "Kalibriert: 500.0 mm/norm",
            HintText: "Kalibriert! DN 300mm = 600px");

        var outcome = CodingManualCalibrationWorkflow.Apply(
            new CodingManualCalibrationWorkflowRequest(
                result,
                ActiveCodingToolName: CodingCalibrationTogglePolicy.CalibrateButtonName,
                IsCodingSchemaActive: true),
            Actions(calls));

        Assert.Equal(CodingManualCalibrationWorkflowOutcome.Applied, outcome);
        Assert.Equal(
            [
                "overlay:300:0.600",
                "schema:300:0.600",
                "manual-result:Kalibriert: 500.0 mm/norm|Kalibriert! DN 300mm = 600px",
                "end-mode",
                "clear-active-tool",
                "hide-hint",
                "cursor",
                "schema-overlay"
            ],
            calls);
    }

    [Fact]
    public void Apply_keeps_other_active_tool_and_skips_schema_overlay_when_schema_inactive()
    {
        var calls = new List<string>();
        var result = new CodingManualCalibrationResult(
            IsValid: true,
            Calibration: Calibration(),
            StatusText: "Kalibriert: 500.0 mm/norm",
            HintText: "Kalibriert! DN 300mm = 600px");

        var outcome = CodingManualCalibrationWorkflow.Apply(
            new CodingManualCalibrationWorkflowRequest(
                result,
                ActiveCodingToolName: "BtnOtherTool",
                IsCodingSchemaActive: false),
            Actions(calls));

        Assert.Equal(CodingManualCalibrationWorkflowOutcome.Applied, outcome);
        Assert.Equal(
            [
                "overlay:300:0.600",
                "schema:300:0.600",
                "manual-result:Kalibriert: 500.0 mm/norm|Kalibriert! DN 300mm = 600px",
                "end-mode",
                "hide-hint",
                "cursor"
            ],
            calls);
    }

    private static CodingManualCalibrationWorkflowActions Actions(List<string> calls)
        => new(
            ShowInvalidHint: text => calls.Add($"hint:{text}"),
            ClearCalibrationStart: () => calls.Add("clear-start"),
            SetOverlayCalibration: calibration => calls.Add(
                $"overlay:{calibration.NominalDiameterMm}:{calibration.NormalizedDiameter:F3}"),
            ApplySchemaCalibration: calibration => calls.Add(
                $"schema:{calibration.NominalDiameterMm}:{calibration.NormalizedDiameter:F3}"),
            ApplyManualResult: result => calls.Add($"manual-result:{result.StatusText}|{result.HintText}"),
            EndCalibrationMode: () => calls.Add("end-mode"),
            ClearActiveToolName: () => calls.Add("clear-active-tool"),
            HideHint: () => calls.Add("hide-hint"),
            UpdateOverlayCursor: () => calls.Add("cursor"),
            EnableCodingSchemaOverlay: () => calls.Add("schema-overlay"));

    private static PipeCalibration Calibration()
        => new()
        {
            NominalDiameterMm = 300,
            NormalizedDiameter = 0.6,
            PipePixelDiameter = 600,
            PipeCenter = new NormalizedPoint(0.5, 0.4),
            WasManuallyCalibrated = true,
            Source = CalibrationSource.Manual
        };
}
