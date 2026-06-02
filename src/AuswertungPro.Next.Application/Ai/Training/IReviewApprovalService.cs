using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Ergebnis einer Approve- oder Reject-Operation auf einem Self-Training-Sample.
/// </summary>
/// <param name="Found">True wenn das Sample anhand der SampleId gefunden wurde.</param>
/// <param name="Indexed">True wenn das Sample (oder korrigiertes Sample beim Approve) erfolgreich in der KB indexiert wurde.</param>
/// <param name="Deindexed">True wenn das abgelehnte Sample aus der KB entfernt wurde.</param>
/// <param name="CorrectedSampleId">SampleId des neu angelegten korrigierten Samples (nur bei Reject mit correctedCode).</param>
public sealed record ReviewApplyResult(bool Found, bool Indexed, bool Deindexed, string? CorrectedSampleId);

/// <summary>
/// Orchestriert Approve/Reject-Entscheidungen fuer Self-Training-Review-Eintraege.
/// Lookup erfolgt per SampleId (nicht per CaseId/Code/Meter-Fuzzy-Match).
/// Die tatsaechliche KB-Indexierung wird ueber IKnowledgeBaseIndexer delegiert.
/// </summary>
public interface IReviewApprovalService
{
    /// <summary>
    /// Setzt ein Self-Training-Sample auf Approved und indexiert es in die KB.
    /// Optional wird eine BoundingBox auf dem Sample gesetzt (vor der Indexierung).
    /// </summary>
    Task<ReviewApplyResult> ApproveSelfTrainingAsync(string sampleId, BoundingBox? box, CancellationToken ct);

    /// <summary>
    /// Setzt ein Self-Training-Sample auf Rejected und entfernt es aus der KB.
    /// Bei correctedCode wird ein neues korrigiertes Sample angelegt und indexiert.
    /// </summary>
    Task<ReviewApplyResult> RejectSelfTrainingAsync(string sampleId, string? correctedCode, CancellationToken ct);
}
