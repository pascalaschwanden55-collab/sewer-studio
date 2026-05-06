// AuswertungPro – Video-Selbsttraining Phase 3
using System;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

/// <summary>
/// Prueft vor der KB-Indexierung ob ein visuell aehnliches Sample bereits existiert.
/// Verhindert Redundanz in der KnowledgeBase.
///
/// Thresholds:
/// - Normale Samples: Cosine-Similarity >= 0.92 → bereits abgedeckt (verwerfen)
/// - Korrigierte Faelle: Cosine-Similarity >= 0.85 → bereits abgedeckt
///   (niedrigerer Threshold, weil Grenzfaelle besonders wertvoll sind)
/// </summary>
public sealed class KbDeduplicationService
{
    /// <summary>Similarity-Threshold fuer normale Samples.</summary>
    public const double NormalThreshold = 0.92;

    /// <summary>Similarity-Threshold fuer korrigierte Faelle (KI war falsch, Mensch hat korrigiert).</summary>
    public const double CorrectedThreshold = 0.85;

    private readonly EmbeddingService _embedding;
    private readonly RetrievalService _retrieval;
    private readonly ILogger? _log;

    public KbDeduplicationService(
        EmbeddingService embedding,
        RetrievalService retrieval,
        ILogger? log = null)
    {
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _log = log;
    }

    /// <summary>
    /// Prueft ob ein aehnliches Sample bereits in der KB existiert.
    /// </summary>
    /// <param name="sample">Das zu pruefende Sample.</param>
    /// <param name="isCorrected">True wenn es eine menschliche Korrektur ist (niedrigerer Threshold).</param>
    /// <returns>Dedup-Ergebnis mit Similarity-Score und Info ob schon abgedeckt.</returns>
    public async Task<DeduplicationResult> CheckAsync(
        TrainingSample sample,
        bool isCorrected,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sample);

        // Embedding-Text bauen (mit optionalem Kontext)
        var embeddingText = KnowledgeBaseManager.BuildEmbeddingText(
            sample.Code, sample.Beschreibung, sample.Rohrmaterial, sample.NennweiteMm);

        // Aehnlichste Samples in der KB suchen (Top-1 genuegt)
        var results = await _retrieval.RetrieveAsync(embeddingText, topK: 1, ct: ct)
            .ConfigureAwait(false);

        if (results is null || results.Count == 0)
        {
            _log?.LogDebug("Keine aehnlichen Samples in KB fuer {Code}", sample.Code);
            return new DeduplicationResult(
                IsAlreadyCovered: false,
                HighestSimilarity: 0,
                MostSimilarSampleId: null);
        }

        var best = results[0];
        var threshold = isCorrected ? CorrectedThreshold : NormalThreshold;
        var covered = best.Score >= threshold;

        _log?.LogDebug(
            "Dedup-Check fuer {Code}: Similarity={Score:F3}, Threshold={Threshold:F2}, Covered={Covered}",
            sample.Code, best.Score, threshold, covered);

        return new DeduplicationResult(
            IsAlreadyCovered: covered,
            HighestSimilarity: best.Score,
            MostSimilarSampleId: best.Sample.SampleId);
    }
}

/// <summary>Ergebnis der Dedup-Pruefung.</summary>
public sealed record DeduplicationResult(
    bool IsAlreadyCovered,
    double HighestSimilarity,
    string? MostSimilarSampleId);
