namespace AuswertungPro.Next.Application.Import;

/// <summary>Ausgelesene Texte einer PDF-Datei, getrennt nach Seiten.</summary>
public sealed record PdfTextExtractionResult(IReadOnlyList<string> Pages, string FullText);

/// <summary>
/// Liest PDF-Text mit begrenzter Dateigroesse, Seitenzahl und Ausgabemenge.
/// Die konkrete Datei- und Prozessarbeit liegt in Infrastructure.
/// </summary>
public interface IPdfTextExtractor
{
    string FindPdfToTextPath(string? explicitPath = null);

    PdfTextExtractionResult ExtractPages(string pdfPath, string? explicitPdfToTextPath = null);

    void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null);
}
