using System;
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
        // Nur wenn noch nicht kalibriert
        if (_codingOverlayService?.IsCalibrated == true) return;

        // DN aus Haltungsdaten
        int nominalDn = 300; // Fallback
        if (_haltungRecord?.Fields.TryGetValue("DN_mm", out var dnStr) == true
            && int.TryParse(dnStr, out var dn) && dn > 0)
            nominalDn = dn;

        try
        {
            // Aktuellen Frame capturen (async)
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes == null || frameBytes.Length == 0) return;

            var autoCalib = CodingAutoCalibrationFrameService.TryAutoCalibrate(frameBytes, nominalDn);
            if (autoCalib == null) return;

            _codingOverlayService?.SetCalibration(autoCalib);

            SetCodingAiState(
                $"Auto-Kalibrierung: DN{nominalDn} erkannt ({autoCalib.NormalizedDiameter:P0} der Bildbreite)",
                PlayerStatusColors.Success,
                "Rohrdurchmesser automatisch gemessen");

            PlayerTrace.WriteLine(
                $"[AutoCalib] DN{nominalDn}: NormDiam={autoCalib.NormalizedDiameter:F3}, " +
                $"Center=({autoCalib.PipeCenter.X:F3},{autoCalib.PipeCenter.Y:F3}), " +
                $"PixelDiam={autoCalib.PipePixelDiameter:F0}");
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[AutoCalib] Fehlgeschlagen: {ex.Message}");
        }
    }
}
