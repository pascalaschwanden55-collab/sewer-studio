using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Annotation;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Annotation;

/// <summary>
/// Adapter um <see cref="KnowledgeBaseManager.IndexSampleAsync"/>. Wandelt
/// das <c>bool</c>-Resultat in das <c>Task</c>-basierte Vertragsformat um:
/// <c>false</c> wird als Fehler erkannt (z.B. Disk-Full-Guard, Embedding-
/// Failure), damit <c>CommitAsync</c> das Sample auf KbIndexState.Pending
/// halten kann (K2 aus dem Plan-Header).
///
/// Der Constructor nimmt einen <c>Func</c>-Hook, damit der Adapter
/// sauber testbar bleibt — der KnowledgeBaseManager ist sealed und
/// schwer zu mocken. In Produktion wird der Hook auf
/// <c>manager.IndexSampleAsync</c> gesetzt.
/// </summary>
public sealed class KnowledgeBaseIndexerAdapter : IKnowledgeBaseIndexer
{
    private readonly Func<TrainingSample, CancellationToken, Task<bool>> _indexSample;

    public KnowledgeBaseIndexerAdapter(KnowledgeBaseManager manager)
        : this((sample, ct) => manager.IndexSampleAsync(sample, ct))
    {
    }

    /// <summary>Test-Konstruktor mit injizierbarem Hook.</summary>
    public KnowledgeBaseIndexerAdapter(Func<TrainingSample, CancellationToken, Task<bool>> indexSample)
    {
        _indexSample = indexSample ?? throw new ArgumentNullException(nameof(indexSample));
    }

    public async Task IndexSampleAsync(TrainingSample sample, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var ok = await _indexSample(sample, ct).ConfigureAwait(false);
        if (!ok)
        {
            throw new InvalidOperationException(
                $"KnowledgeBase indexing returned false for sample '{sample.SampleId}' " +
                "(disk-full guard or embedding failure — sample bleibt KbIndexState.Pending).");
        }
    }
}
