using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Abstraktion fuer KB-Indexierung und -Deindexierung von Trainingssamples.
/// Wird als Delegate-Bridge genutzt, damit der ReviewApprovalService
/// VM-Methoden (IncrementalKbUpdateAsync / TryDeindexSample) aufrufen kann,
/// ohne eine direkte Abhaengigkeit auf das ViewModel zu haben.
/// </summary>
public interface IKnowledgeBaseIndexer
{
    /// <summary>Indexiert die uebergebenen Samples in die Knowledge Base. Gibt die SampleIds der erfolgreich indizierten Samples zurueck.</summary>
    Task<IReadOnlyList<string>> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct);

    /// <summary>Entfernt das Sample mit der gegebenen SampleId aus der Knowledge Base.</summary>
    void Deindex(string sampleId);
}
