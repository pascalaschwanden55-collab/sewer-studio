using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record OcrPageExtractionResult(bool Success, string? Text, string? Message);

/// <summary>Kompatible interne API; Datei- und Prozessarbeit liegt im Instanzdienst.</summary>
public static class PdfOcrExtractor
{
    private static IPdfOcrExtractor _current =
        new PdfOcrExtractionService(PdfTextExtractor.Current);

    public static IPdfOcrExtractor Current => Volatile.Read(ref _current);

    public static void Use(IPdfOcrExtractor extractor)
        => Volatile.Write(ref _current, extractor ?? throw new ArgumentNullException(nameof(extractor)));

    internal static OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
    {
        var result = Current.TryExtractPageText(pdfPath, pageNumber);
        return new OcrPageExtractionResult(result.Success, result.Text, result.Message);
    }
}
