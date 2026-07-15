using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>Liest Seiten aus Verteil-PDFs und erhaelt leere Bildseiten fuer den OCR-Rueckfall.</summary>
public sealed class DistributionPdfPageReadingService : IDistributionPdfPageReader
{
    private readonly IPdfTextExtractor _textExtractor;
    private readonly IPdfFileSafetyChecker _fileSafety;

    public DistributionPdfPageReadingService()
        : this(PdfTextExtractor.Current, PdfImportSafetyPolicy.Current)
    {
    }

    public DistributionPdfPageReadingService(
        IPdfTextExtractor textExtractor,
        IPdfFileSafetyChecker fileSafety)
    {
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _fileSafety = fileSafety ?? throw new ArgumentNullException(nameof(fileSafety));
    }

    public IReadOnlyList<DistributionPdfPage> ReadPages(string pdfPath)
    {
        try
        {
            var extraction = _textExtractor.ExtractPages(pdfPath);
            if (extraction.Pages.Count > 0)
                return CreatePages(extraction.Pages, pdfPath);
        }
        catch
        {
            // Der direkte PdfPig-Rueckfall erhaelt auch leere Bildseiten.
        }

        return ReadPagesWithPdfPig(pdfPath);
    }

    private IReadOnlyList<DistributionPdfPage> ReadPagesWithPdfPig(string pdfPath)
    {
        var pages = new List<DistributionPdfPage>();
        _fileSafety.ThrowIfFileTooLarge(pdfPath);
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
        var pageNumber = 0;
        foreach (var page in document.GetPages())
        {
            pageNumber++;
            var text = (page.Text ?? "").Replace("\r\n", "\n").Trim();
            pages.Add(new DistributionPdfPage(pageNumber, text, pdfPath));
        }

        return pages;
    }

    private static IReadOnlyList<DistributionPdfPage> CreatePages(
        IReadOnlyList<string> extractedPages,
        string pdfPath)
    {
        var pages = new List<DistributionPdfPage>(extractedPages.Count);
        for (var index = 0; index < extractedPages.Count; index++)
        {
            var text = (extractedPages[index] ?? "").Replace("\r\n", "\n").Trim();
            pages.Add(new DistributionPdfPage(index + 1, text, pdfPath));
        }

        return pages;
    }
}

/// <summary>Kompatible Fassade fuer bestehende statische Verteiler-Aufrufe.</summary>
public static class DistributionPdfPageReader
{
    private static IDistributionPdfPageReader _current = new DistributionPdfPageReadingService();

    public static IDistributionPdfPageReader Current => Volatile.Read(ref _current);

    public static void Use(IDistributionPdfPageReader reader) =>
        Volatile.Write(ref _current, reader ?? throw new ArgumentNullException(nameof(reader)));
}
