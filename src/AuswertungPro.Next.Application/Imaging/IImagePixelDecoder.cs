using System;

namespace AuswertungPro.Next.Application.Imaging;

/// <summary>
/// Phase 5.3 Sub-A (Audit 2026-05-06 Top-10 Punkt 5): WPF-Imaging-Adapter.
///
/// Decoder fuer PNG/JPG-Bytes nach Bgra32-Pixelarray. Implementierung lebt
/// in der UI-Schicht (System.Windows.Media.Imaging.BitmapImage), das
/// Interface hier in der Application-Schicht entkoppelt KI-Services
/// (PdfProtocolExtractor, FrameQualityFilter, AutoCalibrationService etc.)
/// von WPF, damit sie ohne Windows-Target-Framework laufen koennen.
/// </summary>
public interface IImagePixelDecoder
{
    /// <summary>
    /// Dekodiert Bildbytes (PNG/JPG/BMP) nach Bgra32 (4 Byte pro Pixel: B, G, R, A).
    /// </summary>
    /// <param name="imageBytes">Roh-Bytes der Bilddatei.</param>
    /// <param name="maxWidth">
    /// Optional: maximale Decodierungs-Breite. WPF nutzt das fuer effizientes
    /// Down-Sampling beim Decode (DecodePixelWidth). Hilft bei grossen
    /// Originalen wenn der Konsument nur eine Vorschau braucht (z.B.
    /// PdfProtocolExtractor's IsLikelyLogoOrSymbol mit Width=100).
    /// </param>
    /// <returns>
    /// <see cref="DecodedImage"/> mit Pixels + Dimensionen, oder null wenn das
    /// Format nicht dekodiert werden konnte.
    /// </returns>
    DecodedImage? DecodeBgra32(byte[] imageBytes, int? maxWidth = null);
}

/// <summary>
/// Ergebnis eines Bild-Decodes. Pixels sind im Bgra32-Format
/// (4 Byte pro Pixel: B, G, R, A in dieser Reihenfolge).
/// </summary>
public sealed record DecodedImage(
    byte[] Bgra32Pixels,
    int Width,
    int Height,
    int Stride);
