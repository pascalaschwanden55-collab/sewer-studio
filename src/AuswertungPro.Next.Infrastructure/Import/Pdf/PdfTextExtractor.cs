using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed record PdfTextExtraction(IReadOnlyList<string> Pages, string FullText);

/// <summary>Kompatible statische API; die Datei- und Prozessarbeit liegt im Instanzdienst.</summary>
public static class PdfTextExtractor
{
    private static IPdfTextExtractor _current = new PdfTextExtractionService();

    public static IPdfTextExtractor Current => Volatile.Read(ref _current);

    public static void Use(IPdfTextExtractor extractor)
        => Volatile.Write(ref _current, extractor ?? throw new ArgumentNullException(nameof(extractor)));

    public static string FindPdfToTextPath(string? explicitPath = null)
        => Current.FindPdfToTextPath(explicitPath);

    public static PdfTextExtraction ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
    {
        var result = Current.ExtractPages(pdfPath, explicitPdfToTextPath);
        return new PdfTextExtraction(result.Pages, result.FullText);
    }
}
