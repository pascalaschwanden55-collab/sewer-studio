namespace AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

/// <summary>
/// Farbmodell eines JPEG-Bildstroms, wie es im PDF deklariert ist.
/// </summary>
public enum TrainingPdfJpegColorModel
{
    Gray,
    Rgb,
    Cmyk,
}

/// <summary>
/// Vollstaendige PDF-Farbinformation fuer einen bereits validierten JPEG-Bildstrom.
/// <see cref="Decode"/> enthaelt je Farbkanal ein Min-/Max-Paar.
/// <see cref="InvertSourceSamples"/> rekonstruiert bei CMYK die DCT-Kanalpolaritaet
/// aus den von Windows bereits normalisierten CMYK-Werten; sie ist keine
/// PDF-Decode-Regel.
/// </summary>
public sealed record TrainingPdfJpegColorNormalizationRequest(
    byte[] JpegBytes,
    int PixelWidth,
    int PixelHeight,
    int BitsPerComponent,
    TrainingPdfJpegColorModel ColorModel,
    IReadOnlyList<decimal> Decode,
    bool InvertSourceSamples);

/// <summary>
/// Wandelt einen JPEG-Bildstrom samt PDF-Farbregeln in ein sichtbares RGB-PNG um.
/// Die Windows-WPF-Anwendung stellt dafuer ihren vorhandenen Bilddecoder bereit.
/// </summary>
public interface ITrainingPdfJpegColorNormalizer
{
    bool TryNormalizeToRgbPng(
        TrainingPdfJpegColorNormalizationRequest request,
        out byte[] pngBytes);
}
