using System.IO;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Imaging;

/// <summary>
/// WPF-Implementation von <see cref="IPipeCalibrationFromBytes"/>. Decodiert
/// die Bild-Bytes ueber <see cref="BitmapDecoder"/> und delegiert an die
/// bestehende <see cref="AutoCalibrationService.TryAutoCalibrate"/>-API
/// (die intern weiter mit <c>BitmapSource</c> arbeitet).
///
/// Wird in App.xaml.cs einmalig registriert:
/// <code>PipeCalibrationFromBytesProvider.SetImplementation(new WpfPipeCalibrationFromBytes());</code>
/// </summary>
public sealed class WpfPipeCalibrationFromBytes : IPipeCalibrationFromBytes
{
    public PipeCalibration? TryCalibrate(byte[] imageBytes, int nominalDiameterMm)
    {
        if (imageBytes is null || imageBytes.Length == 0) return null;
        if (nominalDiameterMm <= 0) return null;

        try
        {
            using var ms = new MemoryStream(imageBytes);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var bitmap = decoder.Frames[0];
            return AutoCalibrationService.TryAutoCalibrate(bitmap, nominalDiameterMm);
        }
        catch
        {
            // Korrupte Bytes oder unerwarteter Decoder-Fehler — Auto-
            // Calibration ist best-effort, also null statt Throw.
            return null;
        }
    }
}
