using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Schliesst eindeutig fremde PDFs vor dem seitenweisen Schacht-OCR aus.</summary>
internal static class ShaftPdfRelevance
{
    internal static bool ShouldProcess(string path)
    {
        try
        {
            Pdf.PdfImportSafetyPolicy.Current.ThrowIfFileTooLarge(path);
            using var document = PdfDocument.Open(path);
            Pdf.PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
            if (document.NumberOfPages == 0) return true;
            // Nicht nur den Anfang prüfen: ein Schachtteil kann weit hinten stehen.
            foreach (var page in document.GetPages())
                if (!IsClearlyForeignPage(page.Text)) return true;
            return false;
        }
        catch
        {
            // Unlesbar ist kein Beleg für einen fremden Inhalt. Der normale
            // Verteiler behält Fehlerbericht und Formular-/OCR-Rückfall.
            return true;
        }
    }

    internal static bool IsClearlyForeignPage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || PdfDokumentTypErkennung.HasShaftMarkers(text))
            return false;

        var typ = PdfDokumentTypErkennung.ErkenneText(text);
        if (typ is PdfDokumentTyp.TvProtokoll or PdfDokumentTyp.PlanSituation
            or PdfDokumentTyp.Dichtheitspruefung or PdfDokumentTyp.Deckblatt)
            return true;

        // WinCan-Projektdeckblatt: alle drei Titel verlangen, nicht nur "Projekt".
        // Andere unklare Seiten (auch Bild-/Formularseiten) bleiben im Schachtweg.
        return text.Contains("Projekt", StringComparison.OrdinalIgnoreCase)
               && text.Contains("Kunde", StringComparison.OrdinalIgnoreCase)
               && text.Contains("Unternehmer", StringComparison.OrdinalIgnoreCase)
               && !text.Contains("Schacht", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsHoldingPage(string? text)
        => !PdfDokumentTypErkennung.HasShaftMarkers(text)
           && PdfDokumentTypErkennung.ErkenneText(text) == PdfDokumentTyp.TvProtokoll;
}
