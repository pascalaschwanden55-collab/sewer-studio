namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Prueft die Seitenzahl, bevor ein externer PDF-Prozess gestartet wird.</summary>
internal static class PdfPageBudgetValidator
{
    public static void ThrowIfExceeded(string pdfPath, int? maxPages = null)
        => PdfTextExtractor.Current.ThrowIfPageBudgetExceeded(pdfPath, maxPages);
}
