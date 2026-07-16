using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingReviewApprovalServiceFactory
{
    public static IReviewApprovalService Create(
        Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> index,
        Action<string> deindex,
        ITrainingSampleStore? trainingSamples = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(deindex);

        var indexer = new DelegatingKnowledgeBaseIndexer(index, deindex);
        return new ReviewApprovalService(
            new TrainingSamplesStoreAdapter(trainingSamples ?? TrainingSamplesStore.Current),
            indexer);
    }
}
