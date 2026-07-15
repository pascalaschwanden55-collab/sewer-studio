using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>Liefert KIAS-PDF-Dateien und den Text ihrer ersten Seiten.</summary>
public interface IIbakPdfStammdatenSourceReader
{
    IReadOnlyList<string> EnumeratePdfFiles(string exportRoot);

    string? TryReadFirstPagesText(string pdfPath, int maxPages);
}

/// <summary>Kapselt rekursive PDF-Suche, Dateipruefung und geschuetzte Textauslese.</summary>
public sealed class IbakPdfStammdatenSourceReader : IIbakPdfStammdatenSourceReader
{
    private readonly IPdfTextExtractor _pdfTextExtractor;

    public IbakPdfStammdatenSourceReader(IPdfTextExtractor pdfTextExtractor)
    {
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
    }

    public IReadOnlyList<string> EnumeratePdfFiles(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Array.Empty<string>();

        try
        {
            var reportPath = Path.Combine(exportRoot, "Report");
            var searchRoot = Directory.Exists(reportPath) ? reportPath : exportRoot;
            return SafeFileEnumeration
                .EnumerateFilesSafe(searchRoot, "*.pdf", recursive: true)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? TryReadFirstPagesText(string pdfPath, int maxPages)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath) || maxPages <= 0)
            return null;

        try
        {
            var extraction = _pdfTextExtractor.ExtractPages(pdfPath);
            return string.Join("\n", extraction.Pages.Take(maxPages));
        }
        catch
        {
            return null;
        }
    }
}
