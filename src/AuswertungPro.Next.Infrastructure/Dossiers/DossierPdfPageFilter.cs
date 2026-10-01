using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Lässt einzelne Blätter aus dem fertigen Gesamt-PDF weg.
///
/// Weggelassen wird nur in der AUSGABE. Die Word-Datei und die
/// Original-Protokolle bleiben unverändert — deshalb arbeitet diese Regel auf
/// dem bereits zusammengeführten PDF und nicht an den Quellen.
/// </summary>
public static class DossierPdfPageFilter
{
    /// <summary>
    /// Dasselbe PDF ohne die genannten Seitennummern (1-basiert). Ohne
    /// Ausschluss wird die Vorlage unveraendert zurueckgegeben — dann ist
    /// nichts neu zu schreiben.
    /// </summary>
    public static byte[] Ohne(byte[] pdf, IReadOnlySet<int> ausgeschlossen)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(ausgeschlossen);

        if (ausgeschlossen.Count == 0)
            return pdf;

        using var quelle = PdfDocument.Open(pdf);

        var behalten = Enumerable
            .Range(1, quelle.NumberOfPages)
            .Where(nummer => !ausgeschlossen.Contains(nummer))
            .ToList();

        if (behalten.Count == quelle.NumberOfPages)
            return pdf;

        if (behalten.Count == 0)
        {
            throw new InvalidOperationException(
                "Es muss mindestens ein Blatt im Gesamt-PDF bleiben.");
        }

        using var speicher = new MemoryStream();
        using (var bauer = new PdfDocumentBuilder(speicher))
        {
            foreach (var nummer in behalten)
                bauer.AddPage(quelle, nummer);
        }

        return speicher.ToArray();
    }
}
