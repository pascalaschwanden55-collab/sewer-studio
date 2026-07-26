using System.Threading.Tasks;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
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
                IsAlreadyCalibrated: _codingOverlayToolHost.IsCalibrated,
                Fields: _protocolContext.HaltungRecord?.Fields),
            new CodingAutoCalibrationWorkflowActions(
                CaptureFrameAsync: CaptureCurrentFrameAsync,
                TryAutoCalibrate: (frameBytes, nominalDn) =>
                    CodingAutoCalibrationFrameService.TryAutoCalibrate(frameBytes, nominalDn),
                ApplyCalibration: calibration => { _codingOverlayToolHost.SetCalibration(calibration); },
                SetCodingAiState: (status, color, detail) => _liveDetectionStatusController.SetCodingAiState(status, color, detail),
                TraceApplied: message => PlayerTrace.WriteLine(message),
                TraceError: message => PlayerTrace.WriteLine($"[AutoCalib] Fehlgeschlagen: {message}")));
    }
}
