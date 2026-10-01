using System;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Verwendet genau die Bausteine der Verteilung: den Seitenleser
/// (<see cref="DistributionPdfAssignmentController.ReadPages"/>, inklusive
/// OCR-Rueckfall fuer Bildseiten) und den Schachtparser
/// (<see cref="HoldingFolderDistributor.ParseSchachtPdf"/>).
///
/// Wichtig ist die TEXTQUELLE: Der Import las PDFs bisher ueber PdfPig, das
/// saemtliche Leerzeichen entfernt - die zeilenbasierte Datumsregel der
/// Verteilung findet darin nichts. Deshalb hier derselbe Leser.
/// </summary>
public sealed class ProtocolPdfDateReader : IProtocolPdfDateReader
{
    public DateTime? ReadSchachtDate(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        try
        {
            var seiten = DistributionPdfAssignmentController.ReadPages(pdfPath);
            if (seiten.Count == 0)
                return null;

            var text = string.Join("\n\n", seiten.Select(s => s.Text));
            return HoldingFolderDistributor.ParseSchachtPdf(text).Date;
        }
        catch
        {
            // Ein unlesbares PDF darf die Verteilung nicht anhalten; es bekommt
            // den Nullstempel statt eines geratenen Datums.
            return null;
        }
    }
}
