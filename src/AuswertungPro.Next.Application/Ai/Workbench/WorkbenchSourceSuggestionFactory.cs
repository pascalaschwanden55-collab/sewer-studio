using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Workbench;

/// <summary>
/// Baut aus einem gespeicherten PDF-Foto-Sample die fachliche Vorgabe fuer den Pruefplatz
/// (<see cref="WorkbenchSourceSuggestion"/>). Eine Stelle fuer Warteschlange des Pruefplatzes und
/// Gold-Qualitaetspruefung, vorher in beiden kopiert (Deepscan 02.10.2026, B6).
/// </summary>
public static class WorkbenchSourceSuggestionFactory
{
    /// <summary>
    /// Liefert die Vorgabe nur fuer ein PDF-Foto-Sample mit gueltigem Herkunftsvermerk, Code und
    /// Beschreibung; sonst null.
    /// </summary>
    public static WorkbenchSourceSuggestion? Von(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!string.Equals(sample.SourceType, SourceTypeNames.PdfPhoto, StringComparison.OrdinalIgnoreCase)
            || !PdfGoldProvenancePolicy.TryParse(sample.Notes, out var provenance)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceCode)
            || string.IsNullOrWhiteSpace(sample.SourceReferenceDescription))
        {
            return null;
        }

        return new WorkbenchSourceSuggestion(
            sample.SourceReferenceCode,
            sample.SourceReferenceDescription,
            provenance.SourceDocumentName,
            provenance.SourceDocumentSha256,
            provenance.PageNumber,
            provenance.PhotoId,
            provenance.MatchKind)
        {
            InspectionDate = sample.InspectionDate,
        };
    }
}
