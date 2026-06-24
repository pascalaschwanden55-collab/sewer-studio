using System.Globalization;
using System.Windows.Media;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingAutoCalibrationWorkflowOutcome
{
    SkippedAlreadyCalibrated,
    NoFrame,
    NoCalibration,
    Applied,
    ErrorLogged
}

public sealed record CodingAutoCalibrationWorkflowRequest(
    bool IsAlreadyCalibrated,
    IReadOnlyDictionary<string, string>? Fields);

public sealed record CodingAutoCalibrationWorkflowActions(
    Func<Task<byte[]?>> CaptureFrameAsync,
    Func<byte[], int, PipeCalibration?> TryAutoCalibrate,
    Action<PipeCalibration> ApplyCalibration,
    Action<string, Color, string?> SetCodingAiState,
    Action<string> TraceApplied,
    Action<string> TraceError);

public static class CodingAutoCalibrationWorkflow
{
    public static async Task<CodingAutoCalibrationWorkflowOutcome> ExecuteAsync(
        CodingAutoCalibrationWorkflowRequest request,
        CodingAutoCalibrationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsAlreadyCalibrated)
            return CodingAutoCalibrationWorkflowOutcome.SkippedAlreadyCalibrated;

        var nominalDn = ResolveNominalDn(request.Fields);

        try
        {
            var frameBytes = await actions.CaptureFrameAsync();
            if (frameBytes == null || frameBytes.Length == 0)
                return CodingAutoCalibrationWorkflowOutcome.NoFrame;

            var autoCalibration = actions.TryAutoCalibrate(frameBytes, nominalDn);
            if (autoCalibration == null)
                return CodingAutoCalibrationWorkflowOutcome.NoCalibration;

            actions.ApplyCalibration(autoCalibration);
            actions.SetCodingAiState(
                $"Auto-Kalibrierung: DN{nominalDn} erkannt ({autoCalibration.NormalizedDiameter:P0} der Bildbreite)",
                PlayerStatusColors.Success,
                "Rohrdurchmesser automatisch gemessen");
            actions.TraceApplied(
                $"[AutoCalib] DN{nominalDn}: NormDiam={autoCalibration.NormalizedDiameter:F3}, " +
                $"Center=({autoCalibration.PipeCenter.X:F3},{autoCalibration.PipeCenter.Y:F3}), " +
                $"PixelDiam={autoCalibration.PipePixelDiameter:F0}");

            return CodingAutoCalibrationWorkflowOutcome.Applied;
        }
        catch (Exception ex)
        {
            actions.TraceError(ex.Message);
            return CodingAutoCalibrationWorkflowOutcome.ErrorLogged;
        }
    }

    private static int ResolveNominalDn(IReadOnlyDictionary<string, string>? fields)
    {
        const int fallbackDn = 300;

        return fields?.TryGetValue("DN_mm", out var dnText) == true
               && int.TryParse(dnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn)
               && dn > 0
            ? dn
            : fallbackDn;
    }
}
