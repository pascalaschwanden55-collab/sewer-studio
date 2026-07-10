using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

/// <summary>
/// Processes Accept/Reject feedback. The relearn checkpoint is persisted in SQLite;
/// therefore recreating this service for each UI decision no longer resets the threshold.
/// </summary>
public sealed class FeedbackIngestionService
{
    public int ReLearnInterval { get; set; } = 25;

    private readonly ValidationLogger _logger;
    private readonly WeightLearningService _weightLearner;
    private readonly ITrainingSampleIndexer? _sampleIndexer;

    public FeedbackIngestionService(
        ValidationLogger logger,
        WeightLearningService weightLearner,
        ITrainingSampleIndexer? sampleIndexer = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _weightLearner = weightLearner ?? throw new ArgumentNullException(nameof(weightLearner));
        _sampleIndexer = sampleIndexer;
    }

    /// <summary>Backward-compatible entry point for callers without a real frame/case sample.</summary>
    public Task ProcessFeedbackAsync(
        MappedProtocolEntry entry,
        string finalCode,
        bool accepted,
        CancellationToken ct = default)
        => ProcessFeedbackAsync(entry, finalCode, accepted, confirmedSample: null, ct);

    /// <summary>
    /// Processes feedback and, for accepted decisions, indexes the supplied real training sample.
    /// A synthetic fallback sample is kept only for compatibility and remains protected by the
    /// frame/holding identity gate.
    /// </summary>
    public async Task ProcessFeedbackAsync(
        MappedProtocolEntry entry,
        string finalCode,
        bool accepted,
        TrainingSample? confirmedSample,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var suggestedCode = entry.SuggestedCode ?? string.Empty;
        var vsaCode = !string.IsNullOrWhiteSpace(finalCode) ? finalCode : suggestedCode;
        var wasCorrect = accepted && string.Equals(suggestedCode, finalCode, StringComparison.OrdinalIgnoreCase);

        _logger.Log(vsaCode, suggestedCode, finalCode, wasCorrect, entry.Detection.Evidence);

        if (accepted && _sampleIndexer is not null && !string.IsNullOrWhiteSpace(vsaCode))
        {
            var sample = confirmedSample ?? BuildCompatibilitySample(entry, vsaCode);
            if (IsIndexable(sample))
            {
                try
                {
                    sample.Status = TrainingSampleStatus.Approved;
                    await _sampleIndexer.IndexSampleAsync(sample, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Validation logging must remain durable even when embedding/indexing is offline.
                    System.Diagnostics.Debug.WriteLine(
                        $"[FeedbackIngestion] KB-Indexierung fehlgeschlagen: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FeedbackIngestion] Sample {sample.SampleId} NICHT indexiert: kein Frame-/Haltungsbezug " +
                    "(Eval-Guard waere blind, Embedding inhaltsleer).");
            }
        }

        if (!_logger.TryClaimRelearnBatch(ReLearnInterval, out var claimedCount))
            return;

        try
        {
            var snapshot = await _weightLearner.ReLearnAndLoadSnapshotAsync(ct).ConfigureAwait(false);
            QualityGateService.ConfigureProcessWeights(snapshot.Weights, snapshot.Version);
            _logger.CompleteRelearnBatch(claimedCount, snapshot.Version);
        }
        catch (Exception ex)
        {
            _logger.FailRelearnBatch(ex.Message);
            // Weight learning is non-critical for saving the user's decision, but remains retryable.
            System.Diagnostics.Debug.WriteLine(
                $"[FeedbackIngestion] Weight-Learning fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>Total validations persisted in the shared database.</summary>
    public int TotalProcessed => _logger.GetTotalCount();

    private static TrainingSample BuildCompatibilitySample(MappedProtocolEntry entry, string vsaCode)
    {
        var detection = entry.Detection;
        return new TrainingSample
        {
            SampleId = $"feedback_{Guid.NewGuid():N}",
            CaseId = detection.FindingLabel ?? string.Empty,
            Code = vsaCode,
            Beschreibung = detection.FindingLabel ?? string.Empty,
            MeterStart = detection.MeterStart,
            MeterEnd = detection.MeterEnd,
            Status = TrainingSampleStatus.Approved
        };
    }

    private static bool IsIndexable(TrainingSample sample)
    {
        var hasFrame = !string.IsNullOrWhiteSpace(sample.FramePath);
        var hasHaltungId = !string.IsNullOrWhiteSpace(sample.CaseId) &&
            Regex.IsMatch(sample.CaseId, @"\d[\d.]*[-/]\d[\d.]*");
        return hasFrame || hasHaltungId;
    }
}
