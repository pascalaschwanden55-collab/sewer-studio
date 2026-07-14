using AuswertungPro.Next.Application.Reports;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>Kombiniert erzeugte Berichte mit lesbaren Original-PDFs.</summary>
public sealed class PdfMergeService : IPdfMergeService
{
    public byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
    {
        if (generatedPdf.Length == 0)
            return MergeOriginals(originalPdfPaths);

        if (originalPdfPaths.Count == 0)
            return generatedPdf;

        try
        {
            using var ms = new MemoryStream();
            using (var builder = new PdfDocumentBuilder(ms))
            {
                using (var genDoc = PdfDocument.Open(generatedPdf))
                {
                    foreach (var page in genDoc.GetPages())
                        builder.AddPage(genDoc, page.Number);
                }

                AppendOriginalPages(builder, originalPdfPaths);
            }

            return ms.ToArray();
        }
        catch
        {
            return generatedPdf;
        }
    }

    public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
    {
        if (originalPdfPaths.Count == 0)
            return Array.Empty<byte>();

        try
        {
            using var ms = new MemoryStream();
            using (var builder = new PdfDocumentBuilder(ms))
            {
                AppendOriginalPages(builder, originalPdfPaths);
            }

            return ms.ToArray();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static void AppendOriginalPages(
        PdfDocumentBuilder builder,
        IReadOnlyList<string> originalPdfPaths)
    {
        foreach (var pdfPath in originalPdfPaths)
        {
            if (!File.Exists(pdfPath))
                continue;

            try
            {
                using var origDoc = PdfDocument.Open(pdfPath);
                foreach (var page in origDoc.GetPages())
                    builder.AddPage(origDoc, page.Number);
            }
            catch
            {
                // Eine unlesbare Originaldatei darf die restlichen PDFs nicht blockieren.
            }
        }
    }
}
