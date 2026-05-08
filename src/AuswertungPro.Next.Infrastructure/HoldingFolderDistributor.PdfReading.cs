using System.Collections.Generic;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — PDF-Lese- und Common-Helper (partial class).
///
/// Refactor 2026-05-08 (Etappe 6, Charge R13): PDF-Read-Pfade,
/// PageInfo/PdfPageChunk-Records, NormalizeText, IsContentsPage,
/// BuildPageRange — alles was beim Per-Page-PDF-Verarbeiten gebraucht
/// wird. Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private sealed record PageInfo(int PageNumber, string Text, string SourcePath);
    private sealed record PdfPageChunk(IReadOnlyList<int> Pages, ParsedPdf Parsed);

    /// <summary>
    /// Liest alle Seiten eines PDFs als PageInfo-Liste.
    /// Bevorzugt PdfTextExtractor (Layout-erhaltend), Fallback auf PdfPig.
    /// </summary>
    private static IReadOnlyList<PageInfo> ReadPdfPages(string pdfPath)
    {
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            if (extraction.Pages.Count == 0)
                return ReadPdfPagesWithPdfPig(pdfPath);

            var pages = new List<PageInfo>(extraction.Pages.Count);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                var text = (extraction.Pages[i] ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(i + 1, text, pdfPath));
            }
            return pages;
        }
        catch
        {
            return ReadPdfPagesWithPdfPig(pdfPath);
        }
    }

    private static IReadOnlyList<PageInfo> ReadPdfPagesWithPdfPig(string pdfPath)
    {
        // PdfTextExtractor nutzt Layout-erhaltende Extraktion (Letter-by-Letter),
        // die Zeilen/Spalten korrekt rekonstruiert. Direkt page.Text ist unbrauchbar
        // weil es keine Zeilenumbrueche oder Abstande erhaelt.
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            var pages = new List<PageInfo>(extraction.Pages.Count);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                var text = (extraction.Pages[i] ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(i + 1, text, pdfPath));
            }
            return pages;
        }
        catch
        {
            // Absoluter Fallback: page.Text (besser als nichts)
            var pages = new List<PageInfo>();
            using var doc = PdfDocument.Open(pdfPath);
            var pageNumber = 0;
            foreach (var page in doc.GetPages())
            {
                pageNumber++;
                var text = (page.Text ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(pageNumber, text, pdfPath));
            }
            return pages;
        }
    }

    private static string ReadPdfText(string pdfPath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(pdfPath);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    /// <summary>
    /// Ersetzt non-breaking spaces (U+00A0), en/em/minus-dashes und Tabs
    /// durch Standard-ASCII-Zeichen.
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return text
            .Replace(' ', ' ')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('−', '-')
            .Replace("\t", " ");
    }

    private static string BuildPageRange(IReadOnlyList<int> pages)
    {
        if (pages.Count == 0) return "";
        var sorted = pages.Distinct().OrderBy(p => p).ToList();
        return sorted.Count == 1 ? $"{sorted[0]}" : $"{sorted[0]}-{sorted[^1]}";
    }

    private static bool IsContentsPage(string text)
        => text.Contains("Inhaltsverzeichnis", System.StringComparison.OrdinalIgnoreCase);
}
