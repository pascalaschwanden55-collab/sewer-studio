using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Adapter um den KnowledgeBaseManager. Indexiert ein Sample
/// (Embedding + KB-Eintrag).
/// </summary>
public interface IKnowledgeBaseIndexer
{
    Task IndexSampleAsync(TrainingSample sample, CancellationToken ct);
}
