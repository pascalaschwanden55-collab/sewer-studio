using System.Threading.Tasks;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Versucht eine Auto-Kalibrierung des Rohrdurchmessers aus dem aktuellen Video-Frame.
    /// Erkennt Rohrinnenwand-Kanten per Helligkeitsgradienten.
    /// </summary>
    private async Task TryAutoCalibrationFromCurrentFrame()
    {
        await CodingAutoCalibrationWorkflow.ExecuteAsync(
            new CodingAutoCalibrationWorkflowRequest(
                IsAlreadyCalibrated: _codingOverlayService?.IsCalibrated == true,
                Fields: _haltungRecord?.Fields),
            new CodingAutoCalibrationWorkflowActions(
                CaptureFrameAsync: CaptureCurrentFrameAsync,
                TryAutoCalibrate: (frameBytes, nominalDn) =>
                    CodingAutoCalibrationFrameService.TryAutoCalibrate(frameBytes, nominalDn),
                ApplyCalibration: calibration => _codingOverlayService?.SetCalibration(calibration),
                SetCodingAiState: (status, color, detail) => SetCodingAiState(status, color, detail),
                TraceApplied: message => PlayerTrace.WriteLine(message),
                TraceError: message => PlayerTrace.WriteLine($"[AutoCalib] Fehlgeschlagen: {message}")));
    }
}
