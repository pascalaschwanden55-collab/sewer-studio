using System;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Schliesst eindeutig fremde PDFs vor dem seitenweisen Schacht-OCR aus.</summary>
internal static class ShaftPdfRelevance
{
    /// <summary>
    /// Wie viele Seiten eines reinen Scans per OCR beurteilt werden. Ein Dokument hat
    /// EINEN Typ, und der steht auf dem Deckblatt — dieselbe Annahme wie in
    /// <see cref="PdfDokumentTypErkennung.ErkenneDatei"/>.
    /// </summary>
    private const int MaxOcrSeiten = 2;

    internal static bool ShouldProcess(string path)
        => ShouldProcess(path, PdfDokumenttext.LiesSeiteMitOcr);

    internal static bool ShouldProcess(string path, Func<string, int, string?> ocr)
    {
        try
        {
            Pdf.PdfImportSafetyPolicy.Current.ThrowIfFileTooLarge(path);
            using var document = PdfDocument.Open(path);
            Pdf.PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
            if (document.NumberOfPages == 0) return true;

            var mitText = 0;
            var ohneText = 0;
            // Nicht nur den Anfang prüfen: ein Schachtteil kann weit hinten stehen.
            foreach (var page in document.GetPages())
            {
                if (string.IsNullOrWhiteSpace(page.Text))
                {
                    ohneText++;
                    continue;
                }

                mitText++;
                if (!IsClearlyForeignPage(page.Text)) return true;
            }

            // Jede Seite hatte Text und war fremd — unveraendert.
            if (ohneText == 0) return false;

            // Gemischtes Dokument: eine einzelne Bildseite zwischen Textseiten haelt es
            // wie bisher im Schachtweg. Sie kann ein eingescannter Schachtteil sein, und
            // der Rest des Dokuments belegt nicht, was auf ihr steht.
            if (mitText > 0) return true;

            // Reiner Scan ohne jede Textebene. Bis 2026-09-09 galt er damit als "unklar"
            // und lief immer in den Schachtweg — dort las ihn das OCR des Verteilers,
            // fand die erste bekannte Nummer und legte ihn beim Schacht ab. In Buerglen
            // landeten so alle zehn Dichtheitspruefungen und alle neun Aushaerte-
            // protokolle im Schachtordner, eines davon unter der Chargen-Nr. des Liners.
            // Der Scan wird deshalb wie jede andere PDF nach seinem Kopf beurteilt.
            return !IstFremderScan(path, document.NumberOfPages, ocr);
        }
        catch
        {
            // Unlesbar ist kein Beleg für einen fremden Inhalt. Der normale
            // Verteiler behält Fehlerbericht und Formular-/OCR-Rückfall.
            return true;
        }
    }

    /// <summary>
    /// Beurteilt den Kopf eines reinen Scans als EIN Dokument.
    ///
    /// Bewusst nicht Seite fuer Seite: Seite 2 eines Druckpruef- oder Aushaerte-
    /// protokolls ist ein Diagramm ohne Aussage. Eine Regel "jede gepruefte Seite muss
    /// fremd sein" liess deshalb am 2026-09-09 alle 19 Buerglen-Protokolle wieder durch,
    /// obwohl Seite 1 sie klar benannte. Ein Dokument hat einen Typ, und der steht vorn —
    /// dieselbe Annahme wie in <see cref="PdfDokumentTypErkennung.ErkenneDatei"/>.
    /// </summary>
    private static bool IstFremderScan(string path, int seitenzahl, Func<string, int, string?> ocr)
    {
        var kopf = new System.Text.StringBuilder();
        for (var seite = 1; seite <= Math.Min(MaxOcrSeiten, seitenzahl); seite++)
        {
            var text = ocr(path, seite);
            if (string.IsNullOrWhiteSpace(text))
                break;

            kopf.AppendLine(text);
        }

        // Kein lesbarer Text ist kein Beleg fuer einen fremden Inhalt.
        return kopf.Length > 0 && IsClearlyForeignPage(kopf.ToString());
    }

    internal static bool IsClearlyForeignPage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || PdfDokumentTypErkennung.HasShaftMarkers(text))
            return false;

        var typ = PdfDokumentTypErkennung.ErkenneText(text);
        if (typ is PdfDokumentTyp.TvProtokoll or PdfDokumentTyp.PlanSituation
            or PdfDokumentTyp.Dichtheitspruefung or PdfDokumentTyp.Deckblatt
            or PdfDokumentTyp.Aushaerteprotokoll)
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
