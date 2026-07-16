using System.Globalization;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed record PdfSafetyCheck(bool Allowed, string? Message);

public static class PdfImportSafetyPolicy
{
    private static readonly IPdfFileSafetyChecker Default = new PdfFileSafetyService();

    // Workstation-tauglicher Default (2 GB): grosse GEP-/SchachtPro-Gesamtauszuege mit
    // Vollbild-Fotos (~1 GB) muessen in voller Qualitaet importierbar sein. Bleibt eine
    // vorsorgliche Obergrenze gegen pathologische Dateien (Stabilitaets-Audit K10/S3) und
    // ist per SEWERSTUDIO_MAX_PDF_MB zusaetzlich anpassbar.
    public const long DefaultMaxPdfBytes = 2048L * 1024 * 1024;
    public const int DefaultMaxPages = 1_000;

    /// <summary>
    /// Umgebungsvariable (Megabyte), um das PDF-Groessen-Budget auf leistungsfaehiger
    /// Workstation-Hardware anzuheben. Der Default <see cref="DefaultMaxPdfBytes"/> bleibt
    /// unveraendert, damit der vorsorgliche Stabilitaets-Schutz (Gesamtaudit K10/S3) erhalten
    /// bleibt. Beispiel: grosser GEP-/SchachtPro-Gesamtauszug mit Vollbild-Fotos (~934 MB).
    /// </summary>
    public const string MaxBytesEnvVar = "SEWERSTUDIO_MAX_PDF_MB";

    /// <summary>
    /// Umgebungsvariable, um das Seiten-Budget anzuheben. Default <see cref="DefaultMaxPages"/>.
    /// </summary>
    public const string MaxPagesEnvVar = "SEWERSTUDIO_MAX_PDF_PAGES";

    public static IPdfFileSafetyChecker Current => Default;

    [Obsolete("Globale Dienstwechsel sind nicht mehr erlaubt. IPdfFileSafetyChecker direkt uebergeben.")]
    public static void Use(IPdfFileSafetyChecker checker)
        => throw new NotSupportedException(
            "PdfImportSafetyPolicy ist unveraenderlich. IPdfFileSafetyChecker direkt uebergeben.");

    /// <summary>
    /// Aufgeloestes Byte-Budget: Override aus <see cref="MaxBytesEnvVar"/> (in MB), sonst Default.
    /// </summary>
    public static long ResolveMaxBytes()
        => Current.ResolveMaxBytes();

    internal static long ResolveMaxBytes(string? rawMegabytes)
    {
        if (long.TryParse(rawMegabytes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb)
            && mb > 0
            && mb <= long.MaxValue / (1024L * 1024L))
        {
            return mb * 1024L * 1024L;
        }

        return DefaultMaxPdfBytes;
    }

    /// <summary>
    /// Aufgeloestes Seiten-Budget: Override aus <see cref="MaxPagesEnvVar"/>, sonst Default.
    /// </summary>
    public static int ResolveMaxPages()
        => ResolveMaxPages(Environment.GetEnvironmentVariable(MaxPagesEnvVar));

    internal static int ResolveMaxPages(string? rawPages)
    {
        if (int.TryParse(rawPages, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pages) && pages > 0)
            return pages;

        return DefaultMaxPages;
    }

    public static PdfSafetyCheck CheckFileBudget(string pdfPath, long? maxBytes = null)
    {
        var result = Current.CheckFileBudget(pdfPath, maxBytes);
        return new PdfSafetyCheck(result.Allowed, result.Message);
    }

    public static PdfSafetyCheck CheckPageBudget(int pageCount, int? maxPages = null)
    {
        var limit = maxPages ?? ResolveMaxPages();
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Maximale Seitenzahl muss positiv sein.");

        return pageCount > limit
            ? new PdfSafetyCheck(false, $"PDF hat zu viele Seiten ({pageCount}, Limit {limit}).")
            : new PdfSafetyCheck(true, null);
    }

    public static void ThrowIfFileTooLarge(string pdfPath, long? maxBytes = null)
        => Current.ThrowIfFileTooLarge(pdfPath, maxBytes);

    public static void ThrowIfTooManyPages(int pageCount, int? maxPages = null)
    {
        var check = CheckPageBudget(pageCount, maxPages);
        if (!check.Allowed)
            throw new InvalidDataException(check.Message);
    }
}
