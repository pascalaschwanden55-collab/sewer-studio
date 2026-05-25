using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

public interface ITrainingSampleIndexer
{
    Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct = default);
}
