using System.Text;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Baut lesbare Zusammenfassungs- und Detailstexte fuer Import-Ergebnisse.
/// Reine Formatierungslogik, kein IO.
/// </summary>
public static class ImportSummaryTextBuilder
{
    /// <summary>
    /// Erstellt einen mehrzeiligen Zusammenfassungstext fuer einen kombinierten Import
    /// (Hauptquelle + Sidecar-Sektionen XTF/PDF).
    /// </summary>
    /// <param name="sourceLabel">Label der Hauptimport-Quelle (z.B. "WinCan").</param>
    /// <param name="source">Stats der Hauptimport-Quelle.</param>
    /// <param name="xtfFiles">Anzahl verarbeiteter XTF-Dateien.</param>
    /// <param name="xtfFound">Im XTF-Sidecar gefundene Haltungen.</param>
    /// <param name="xtfUpdated">Aktualisierte Haltungen (XTF).</param>
    /// <param name="xtfUncertain">Unsichere Treffer (XTF).</param>
    /// <param name="xtfErrors">Fehler (XTF).</param>
    /// <param name="pdfFiles">Anzahl verarbeiteter PDF-Dateien.</param>
    /// <param name="pdfFound">Im PDF-Sidecar gefundene Haltungen.</param>
    /// <param name="pdfUpdated">Aktualisierte Haltungen (PDF).</param>
    /// <param name="pdfUncertain">Unsichere Treffer (PDF).</param>
    /// <param name="pdfErrors">Fehler (PDF).</param>
    public static string BuildSummary(
        string sourceLabel,
        ImportStats source,
        int xtfFiles, int xtfFound, int xtfUpdated, int xtfUncertain, int xtfErrors,
        int pdfFiles, int pdfFound, int pdfUpdated, int pdfUncertain, int pdfErrors)
    {
        var sb = new StringBuilder();

        var importSource = source.Messages.FirstOrDefault(m =>
            m.StartsWith("Importquelle:", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(importSource))
            sb.AppendLine(importSource);

        sb.AppendLine($"{sourceLabel}: Gefunden {source.Found}, Neu {source.Created}, Aktualisiert {source.Updated}, Unklar {source.Uncertain}, Fehler {source.Errors}");
        sb.AppendLine($"XTF/M150/MDB/XML: Dateien {xtfFiles}, Gefunden {xtfFound}, Updates {xtfUpdated}, Unklar {xtfUncertain}, Fehler {xtfErrors}");
        sb.AppendLine($"PDF: Dateien {pdfFiles}, Gefunden {pdfFound}, Updates {pdfUpdated}, Unklar {pdfUncertain}, Fehler {pdfErrors}");
        return sb.ToString();
    }

    /// <summary>
    /// Erstellt einen Detailtext aus Sidecar- und Quell-Meldungen (max. 200 Zeilen).
    /// </summary>
    public static string BuildDetails(
        IEnumerable<string> sidecarMessages,
        IEnumerable<string> sourceMessages)
    {
        return string.Join("\n", sidecarMessages.Concat(sourceMessages).Take(200));
    }
}
