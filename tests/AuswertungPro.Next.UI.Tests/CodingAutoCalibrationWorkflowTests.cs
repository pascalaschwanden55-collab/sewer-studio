using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAutoCalibrationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_without_actions_when_already_calibrated()
    {
        var calls = new List<string>();

        var result = await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(
                IsAlreadyCalibrated: true,
                Fields: new Dictionary<string, string> { ["DN_mm"] = "400" }),
            Actions(calls));

        Assert.Equal(CodingAutoCalibrationWorkflowOutcome.SkippedAlreadyCalibrated, result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_applies_auto_calibration_with_dn_from_fields()
    {
        var calls = new List<string>();

        var result = await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(
                IsAlreadyCalibrated: false,
                Fields: new Dictionary<string, string> { ["DN_mm"] = "400" }),
            Actions(calls));

        Assert.Equal(CodingAutoCalibrationWorkflowOutcome.Applied, result);
        Assert.Equal(
            [
                "capture",
                "calibrate:400:3",
                "apply:400:0.500",
                "state:dn:True|diam:True|Rohrdurchmesser automatisch gemessen",
                "trace:dn:True|norm:True"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_uses_fallback_dn_when_field_is_missing()
    {
        var calls = new List<string>();

        var result = await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(
                IsAlreadyCalibrated: false,
                Fields: null),
            Actions(calls));

        Assert.Equal(CodingAutoCalibrationWorkflowOutcome.Applied, result);
        Assert.Contains("calibrate:300:3", calls);
        Assert.Contains("apply:300:0.500", calls);
    }

    [Fact]
    public async Task ExecuteAsync_returns_no_frame_when_capture_is_empty()
    {
        var calls = new List<string>();

        var result = await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(IsAlreadyCalibrated: false, Fields: null),
            Actions(
                calls,
                captureFrameAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>(Array.Empty<byte>());
                }));

        Assert.Equal(CodingAutoCalibrationWorkflowOutcome.NoFrame, result);
        Assert.Equal(["capture"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_logs_error_without_throwing()
    {
        var calls = new List<string>();

        var result = await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(IsAlreadyCalibrated: false, Fields: null),
            Actions(
                calls,
                tryAutoCalibrate: (_, _) => throw new InvalidOperationException("bad frame")));

        Assert.Equal(CodingAutoCalibrationWorkflowOutcome.ErrorLogged, result);
        Assert.Equal(["capture", "trace-error:bad frame"], calls);
    }

    private static CodingAutoCalibrationWorkflowActions Actions(
        List<string> calls,
        Func<Task<byte[]?>>? captureFrameAsync = null,
        Func<byte[], int, PipeCalibration?>? tryAutoCalibrate = null)
        => new(
            CaptureFrameAsync: captureFrameAsync ?? (() =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([1, 2, 3]);
            }),
            TryAutoCalibrate: tryAutoCalibrate ?? ((frameBytes, nominalDn) =>
            {
                calls.Add($"calibrate:{nominalDn}:{frameBytes.Length}");
                return new PipeCalibration
                {
                    NominalDiameterMm = nominalDn,
                    NormalizedDiameter = 0.5,
                    PipePixelDiameter = 123,
                    PipeCenter = new NormalizedPoint(0.4, 0.6)
                };
            }),
            ApplyCalibration: calibration => calls.Add(
                $"apply:{calibration.NominalDiameterMm}:{calibration.NormalizedDiameter:F3}"),
            SetCodingAiState: (status, color, detail) =>
            {
                Assert.Equal(AuswertungPro.Next.UI.Player.PlayerStatusColors.Success, color);
                calls.Add(
                    $"state:dn:{status.Contains("DN400", StringComparison.Ordinal)}|diam:{status.Contains("50", StringComparison.Ordinal)}|{detail}");
            },
            TraceApplied: message => calls.Add(
                $"trace:dn:{message.Contains("DN400", StringComparison.Ordinal)}|norm:{message.Contains("NormDiam=0.500", StringComparison.Ordinal)}"),
            TraceError: message => calls.Add($"trace-error:{message}"));
}
