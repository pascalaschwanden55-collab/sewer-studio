using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Thin Adapter: delegiert IKnowledgeBaseIndexer an bestehende VM-Methoden
/// (IncrementalKbUpdateAsync / TryDeindexSample) via Func/Action.
/// Erzeugt keine neue Logik — reines Bridging.
/// </summary>
public sealed class DelegatingKnowledgeBaseIndexer : IKnowledgeBaseIndexer, IDisposable
{
    private readonly Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> _index;
    private readonly Action<string> _deindex;
    private readonly Action? _dispose;
    private bool _disposed;

    public DelegatingKnowledgeBaseIndexer(
        Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> index,
        Action<string> deindex,
        Action? dispose = null)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _deindex = deindex ?? throw new ArgumentNullException(nameof(deindex));
        _dispose = dispose;
    }

    /// <inheritdoc />
    public Task<KbIndexOutcome> IndexAsync(IReadOnlyList<TrainingSample> samples, CancellationToken ct) =>
        _index(samples, ct);

    /// <inheritdoc />
    public void Deindex(string sampleId) => _deindex(sampleId);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dispose?.Invoke();
    }
}
