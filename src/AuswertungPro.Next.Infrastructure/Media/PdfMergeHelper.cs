using System.Threading;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Kompatibilitaetsfassade fuer den zentral aufgebauten PDF-Zusammenfuegedienst.
/// </summary>
public static class PdfMergeHelper
{
    private static IPdfMergeService _current = new PdfMergeService();

    public static IPdfMergeService Current => Volatile.Read(ref _current);

    public static void Use(IPdfMergeService service)
        => Volatile.Write(
            ref _current,
            service ?? throw new ArgumentNullException(nameof(service)));

    /// <summary>
    /// Haengt die Seiten der Original-PDFs an das generierte PDF an.
    /// Falls ein Merge-Fehler auftritt, wird das Original-PDF zurueckgegeben.
    /// </summary>
    public static byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
        => Current.MergeWithOriginals(generatedPdf, originalPdfPaths);

    /// <summary>
    /// Kombiniert nur die angegebenen Original-PDFs in ein einziges PDF.
    /// </summary>
    public static byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
        => Current.MergeOriginals(originalPdfPaths);
}
