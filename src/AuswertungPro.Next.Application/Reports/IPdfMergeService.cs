namespace AuswertungPro.Next.Application.Reports;

/// <summary>Kombiniert erzeugte Berichte mit vorhandenen Original-PDFs.</summary>
public interface IPdfMergeService
{
    byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths);

    byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths);
}
