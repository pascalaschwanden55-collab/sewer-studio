using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai.Imaging;

/// <summary>
/// Phase 6.3 (Audit 2026-04-23, ARCH-H5): Abstraktion fuer
/// Auto-Calibration-aus-Bytes ohne WPF-Kopplung.
///
/// Erlaubt Application/Infrastructure-Services (insbesondere
/// MultiModelAnalysisService) eine Pipe-Calibration aus PNG/JPG-Bytes
/// abzuleiten, ohne dass sie BitmapDecoder direkt aufrufen oder gegen
/// AutoCalibrationService (heute in UI/Ai/) kompilieren muessen.
///
/// Implementation lebt in der UI-Schicht (WpfPipeCalibrationFromBytes)
/// und nutzt intern BitmapDecoder + AutoCalibrationService.
/// </summary>
public interface IPipeCalibrationFromBytes
{
    /// <summary>
    /// Versucht aus Bild-Bytes (PNG/JPG/BMP) eine Pipe-Calibration zu
    /// extrahieren. Liefert null wenn die Bytes nicht decodierbar sind
    /// oder der Algorithmus kein erkennbares Pipe-Profil findet.
    /// </summary>
    PipeCalibration? TryCalibrate(byte[] imageBytes, int nominalDiameterMm);
}

/// <summary>
/// Static-Provider analog zu <see cref="ImagePixelDecoderProvider"/>:
/// UI-Schicht registriert beim App-Start eine Implementation, der Rest
/// des Codes greift hier zu. Tests / nicht-UI-Hosts koennen ohne
/// Implementation laufen — der Aufrufer pruefen <see cref="Instance"/>
/// auf null und schwenkt auf "keine Auto-Calibration".
/// </summary>
public static class PipeCalibrationFromBytesProvider
{
    private static IPipeCalibrationFromBytes? _impl;

    /// <summary>
    /// Wird einmal beim App-Start (UI-Layer) gesetzt. Tests koennen das
    /// per Instanz auf einen Stub setzen oder leer lassen, je nach
    /// Bedarf.
    /// </summary>
    public static void SetImplementation(IPipeCalibrationFromBytes? impl) => _impl = impl;

    /// <summary>
    /// Aktuelle Implementation oder null wenn keine registriert ist.
    /// </summary>
    public static IPipeCalibrationFromBytes? Instance => _impl;
}
