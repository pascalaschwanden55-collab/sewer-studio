using System;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Liest das Ausfuehrungsdatum aus einem Schachtprotokoll-PDF - mit derselben
/// Textquelle und derselben Regel wie die manuelle Verteilung.
///
/// Ohne diesen gemeinsamen Weg hiess dieselbe Datei je nach Weg anders:
/// "20231010_80783.pdf" nach dem Verteilen, "00000000_80783.pdf" nach dem Import.
/// </summary>
public interface IProtocolPdfDateReader
{
    /// <param name="pdfPath">Lesbarer Pfad des Protokoll-PDFs.</param>
    /// <returns>Das gefundene Datum oder null, wenn keines sicher lesbar ist.</returns>
    DateTime? ReadSchachtDate(string pdfPath);
}
