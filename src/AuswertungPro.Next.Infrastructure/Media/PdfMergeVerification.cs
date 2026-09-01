using AuswertungPro.Next.Application.Reports;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Zusammenfuehren fuer Ausgaben, bei denen jede angeforderte Beilage zwingend
/// enthalten sein muss. Der allgemeine Merge-Dienst bleibt absichtlich
/// fehlertolerant; Eigentuemerdossiers duerfen dagegen nicht still Seiten verlieren.
/// </summary>
public static class PdfMergeVerification
{
    public static byte[] MergeWithRequiredOriginals(
        IPdfMergeService mergeService,
        byte[] generatedPdf,
        IReadOnlyList<string> originalPdfPaths)
    {
        ArgumentNullException.ThrowIfNull(mergeService);
        ArgumentNullException.ThrowIfNull(generatedPdf);
        ArgumentNullException.ThrowIfNull(originalPdfPaths);

        var expectedPages = ReadPageCount(generatedPdf, "Das erzeugte Dossier")
            + ReadRequiredOriginalPageCount(originalPdfPaths);
        var result = mergeService.MergeWithOriginals(generatedPdf, originalPdfPaths);
        EnsureExpectedPageCount(result, expectedPages);
        return result;
    }

    public static byte[] MergeRequiredOriginals(
        IPdfMergeService mergeService,
        IReadOnlyList<string> originalPdfPaths)
    {
        ArgumentNullException.ThrowIfNull(mergeService);
        ArgumentNullException.ThrowIfNull(originalPdfPaths);

        var expectedPages = ReadRequiredOriginalPageCount(originalPdfPaths);
        var result = mergeService.MergeOriginals(originalPdfPaths);
        EnsureExpectedPageCount(result, expectedPages);
        return result;
    }

    private static int ReadRequiredOriginalPageCount(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            throw new InvalidOperationException("Es wurden keine Original-Protokolle angegeben.");

        var pages = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Eine ausgewaehlte PDF-Beilage fehlt: {path ?? "(leer)"}");
            }

            try
            {
                using var document = PdfDocument.Open(path);
                pages += document.NumberOfPages;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Eine ausgewaehlte PDF-Beilage ist nicht lesbar: {path}", ex);
            }
        }

        return pages;
    }

    private static int ReadPageCount(byte[] pdf, string label)
    {
        if (pdf.Length == 0)
            throw new InvalidOperationException(label + " ist leer.");

        try
        {
            using var document = PdfDocument.Open(pdf);
            return document.NumberOfPages;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(label + " ist keine lesbare PDF-Datei.", ex);
        }
    }

    private static void EnsureExpectedPageCount(byte[] result, int expectedPages)
    {
        var actualPages = ReadPageCount(result, "Das zusammengefuehrte Dossier");
        if (actualPages != expectedPages)
        {
            throw new InvalidOperationException(
                $"Das Dossier ist unvollstaendig: erwartet {expectedPages} Seiten, erhalten {actualPages}.");
        }
    }
}
