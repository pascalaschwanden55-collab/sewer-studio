namespace AuswertungPro.Next.Application.Ai.Imaging;

/// <summary>
/// Phase 6.2 (Audit 2026-04-23, ARCH-H5): Abstraktion fuer Bild-Metadaten.
///
/// Laedt ein Bild aus Bytes und gibt grundlegende Properties (PixelWidth/Height,
/// DPI) zurueck. WPF-frei, damit der <c>MultiModelAnalysisService</c> in einer
/// kommenden Phase 6.3 nach Application/Infrastructure migriert werden kann,
/// ohne <c>System.Windows.Media.Imaging.BitmapDecoder</c> direkt aufzurufen.
///
/// Anders als <see cref="AuswertungPro.Next.Application.Imaging.IImagePixelDecoder"/>
/// liefert diese Abstraktion <em>nur</em> Metadaten (kein Pixel-Buffer) und ist
/// damit guenstig fuer Use-Cases wie Auto-Kalibrierung oder Frame-Vorpruefungen,
/// bei denen die Aufloesung reicht.
/// </summary>
public interface IImageBitmapAnalyzer
{
    /// <summary>
    /// Liest die Metadaten (Pixel-Dimensionen + DPI) aus den uebergebenen
    /// Bildbytes (PNG/JPG/BMP).
    /// </summary>
    /// <param name="imageBytes">Roh-Bytes der Bilddatei.</param>
    /// <returns>
    /// <see cref="ImageMetadata"/> des ersten Frames im Bildstream.
    /// </returns>
    ImageMetadata GetMetadata(byte[] imageBytes);
}

/// <summary>
/// Metadaten eines dekodierten Bildes (erster Frame).
/// </summary>
/// <param name="PixelWidth">Bildbreite in Pixeln.</param>
/// <param name="PixelHeight">Bildhoehe in Pixeln.</param>
/// <param name="DpiX">Horizontale Aufloesung in DPI.</param>
/// <param name="DpiY">Vertikale Aufloesung in DPI.</param>
public sealed record ImageMetadata(int PixelWidth, int PixelHeight, double DpiX, double DpiY);
