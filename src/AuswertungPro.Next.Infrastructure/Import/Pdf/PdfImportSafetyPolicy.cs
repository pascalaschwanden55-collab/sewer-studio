namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed record PdfSafetyCheck(bool Allowed, string? Message);

public static class PdfImportSafetyPolicy
{
    public const long DefaultMaxPdfBytes = 256L * 1024 * 1024;
    public const int DefaultMaxPages = 1_000;

    public static PdfSafetyCheck CheckFileBudget(string pdfPath, long maxBytes = DefaultMaxPdfBytes)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return new PdfSafetyCheck(false, "PDF-Pfad fehlt.");
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Maximale PDF-Groesse muss positiv sein.");

        var file = new FileInfo(pdfPath);
        if (!file.Exists)
            return new PdfSafetyCheck(false, $"PDF nicht gefunden: {pdfPath}");

        if (file.Length > maxBytes)
        {
            var actualMb = file.Length / 1024d / 1024d;
            var maxMb = maxBytes / 1024d / 1024d;
            return new PdfSafetyCheck(false, $"PDF ist zu gross ({actualMb:F1} MB, Limit {maxMb:F1} MB): {pdfPath}");
        }

        return new PdfSafetyCheck(true, null);
    }

    public static PdfSafetyCheck CheckPageBudget(int pageCount, int maxPages = DefaultMaxPages)
    {
        if (maxPages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Maximale Seitenzahl muss positiv sein.");

        return pageCount > maxPages
            ? new PdfSafetyCheck(false, $"PDF hat zu viele Seiten ({pageCount}, Limit {maxPages}).")
            : new PdfSafetyCheck(true, null);
    }

    public static void ThrowIfFileTooLarge(string pdfPath, long maxBytes = DefaultMaxPdfBytes)
    {
        var check = CheckFileBudget(pdfPath, maxBytes);
        if (!check.Allowed)
            throw new InvalidDataException(check.Message);
    }

    public static void ThrowIfTooManyPages(int pageCount, int maxPages = DefaultMaxPages)
    {
        var check = CheckPageBudget(pageCount, maxPages);
        if (!check.Allowed)
            throw new InvalidDataException(check.Message);
    }
}
