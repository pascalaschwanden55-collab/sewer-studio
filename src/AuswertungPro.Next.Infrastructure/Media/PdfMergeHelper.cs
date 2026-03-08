using System;
using System.Collections.Generic;
using System.IO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Kombiniert ein generiertes PDF mit Original-PDF-Protokollen zu einem Dossier.
/// </summary>
public static class PdfMergeHelper
{
    /// <summary>
    /// Haengt die Seiten der Original-PDFs an das generierte PDF an.
    /// Falls ein Merge-Fehler auftritt, wird das Original-PDF zurueckgegeben.
    /// </summary>
    public static byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
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

    /// <summary>
    /// Kombiniert nur die angegebenen Original-PDFs in ein einziges PDF.
    /// </summary>
    public static byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
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

    private static void AppendOriginalPages(PdfDocumentBuilder builder, IReadOnlyList<string> originalPdfPaths)
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
                // Skip unreadable PDFs
            }
        }
    }
}
