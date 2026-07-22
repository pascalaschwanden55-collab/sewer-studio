namespace AuswertungPro.Next.Application.Ai.Workbench;

using AuswertungPro.Next.Application.Ai.Training;   // BoundingBox

/// <summary>
/// Orchestriert den Pruefplatz-Handgriff: Box → SAM segmentiert → KI schlaegt Code vor →
/// geprueftes Sample in KB + Teacher-Pool (mit hartem Eval-Schutz).
/// EIN Service fuer Center und Player. Implementierung liegt in der UI-Schicht
/// (wie beim SAM-Review-Muster), damit die Application-Schicht keine Infrastruktur bindet.
/// </summary>
public interface IAnnotationWorkbenchService
{
    /// <summary>Segmentiert die normierte Box per SAM. Liefert Maske(n) + Quantifizierung.</summary>
    Task<WorkbenchSegmentation> SegmentAsync(WorkbenchItem item, BoundingBox box, string codeHint, CancellationToken ct = default);

    /// <summary>Erzeugt den KI-Codevorschlag zur Box (cls-Klassifikator + aehnliche KB-Faelle).</summary>
    Task<WorkbenchSuggestion> SuggestAsync(WorkbenchItem item, BoundingBox box, CancellationToken ct = default);

    /// <summary>
    /// Fragt Qwen nach der feinen Anschluss-Bauart (nur sinnvoll, wenn ein Anschluss im Bild ist).
    /// Kandidaten tragen Quelle "bca"; ohne verfuegbaren Classifier oder bei Unsicherheit leer.
    /// </summary>
    Task<WorkbenchSuggestion> SuggestBcaBauartAsync(WorkbenchItem item, CancellationToken ct = default);

    /// <summary>Speichert die menschliche Entscheidung: Eval-Schutz, TrainingSample, KB-Index, Teacher-Kandidat.</summary>
    Task<WorkbenchSaveResult> SaveAsync(WorkbenchItem item, BoundingBox box, WorkbenchSegmentation? segmentation, WorkbenchDecision decision, CancellationToken ct = default);
}
