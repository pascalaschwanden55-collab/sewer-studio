using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Two-Phase-API fuer Operateur-Annotation.
/// Phase 1: <see cref="PreviewMaskAsync"/> — SAM-Call ohne Persistenz.
/// Phase 2: <see cref="CommitAsync"/> — Best-Effort Store &gt; YOLO &gt; KB.
/// </summary>
public interface IOperateurAnnotationService
{
    /// <summary>
    /// Schickt die UI-Box an SAM und liefert die Maske (RLE + Polygon)
    /// zurueck. Persistiert nichts — die Preview kann verworfen werden.
    /// </summary>
    Task<MaskPreview> PreviewMaskAsync(AnnotationRequest request, CancellationToken ct);

    /// <summary>
    /// Best-Effort-Persistierung: Frame finalisieren, dann Store-Append,
    /// danach YOLO und KB. Store-Erfolg = Commit-Erfolg; YOLO/KB sind
    /// separat reportet, KB-Fehler markiert das Sample als Pending.
    /// </summary>
    Task<CommitResult> CommitAsync(AnnotationRequest request, MaskPreview preview, CancellationToken ct);
}
