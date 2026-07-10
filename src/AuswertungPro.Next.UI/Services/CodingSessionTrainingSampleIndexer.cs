using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Adapts the coding-session service's existing, eval-safe KB indexing path to the
/// generic feedback ingestion contract.
/// </summary>
public sealed class CodingSessionTrainingSampleIndexer : ITrainingSampleIndexer
{
    private readonly ICodingSessionService _sessionService;

    public CodingSessionTrainingSampleIndexer(ICodingSessionService sessionService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    public async Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        await _sessionService.IndexConfirmedSampleAsync(sample, ct).ConfigureAwait(false);
        return sample.KbIndexState == KbIndexState.Indexed;
    }
}
