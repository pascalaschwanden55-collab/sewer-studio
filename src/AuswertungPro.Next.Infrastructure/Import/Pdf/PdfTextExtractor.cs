using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed record PdfTextExtraction(IReadOnlyList<string> Pages, string FullText);

/// <summary>Kompatible statische API; die Datei- und Prozessarbeit liegt im Instanzdienst.</summary>
public static class PdfTextExtractor
{
    private static readonly IPdfTextExtractor Default = new PdfTextExtractionService();

    public static IPdfTextExtractor Current => Default;

    [Obsolete("Die PDF-Text-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IPdfTextExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        throw new NotSupportedException(
            "Die PDF-Text-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static string FindPdfToTextPath(string? explicitPath = null)
        => Current.FindPdfToTextPath(explicitPath);

    public static PdfTextExtraction ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
    {
        var result = Current.ExtractPages(pdfPath, explicitPdfToTextPath);
        return new PdfTextExtraction(result.Pages, result.FullText);
    }
}
