using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Processes Accept/Reject feedback:
/// 1. Logs to ValidationLog
/// 2. On Accept: re-indexes corrected code in KB
/// 3. On Reject: logs as hard-negative for future training
/// 4. Every 25 validations: triggers WeightLearningService.ReLearnAsync()
/// </summary>
public sealed class FeedbackIngestionService
{
    public int ReLearnInterval { get; set; } = 25;

    private readonly ValidationLogger _logger;
    private readonly WeightLearningService _weightLearner;
    private readonly KnowledgeBaseManager? _kbManager;
    private int _feedbackCount;

    public FeedbackIngestionService(
        ValidationLogger logger,
        WeightLearningService weightLearner,
        KnowledgeBaseManager? kbManager = null)
    {
        _logger = logger;
        _weightLearner = weightLearner;
        _kbManager = kbManager;
    }

    /// <summary>Process user feedback for a detection.</summary>
    public async Task ProcessFeedbackAsync(
        MappedProtocolEntry entry,
        string finalCode,
        bool accepted,
        CancellationToken ct = default)
    {
        var suggestedCode = entry.SuggestedCode ?? "";
        var vsaCode = !string.IsNullOrWhiteSpace(finalCode) ? finalCode : suggestedCode;
        var wasCorrect = accepted && string.Equals(suggestedCode, finalCode, StringComparison.OrdinalIgnoreCase);

        // 1. Log to ValidationLog
        _logger.Log(vsaCode, suggestedCode, finalCode, wasCorrect, entry.Detection.Evidence);

        // 2. On Accept: re-index corrected code in KB for future retrieval
        if (accepted && _kbManager is not null && !string.IsNullOrWhiteSpace(vsaCode))
        {
            try
            {
                var det = entry.Detection;
                var sample = new TrainingSample
                {
                    SampleId = $"feedback_{Guid.NewGuid():N}",
                    CaseId = det.FindingLabel ?? "",
                    Code = vsaCode,
                    Beschreibung = det.FindingLabel ?? "",
                    MeterStart = det.MeterStart,
                    MeterEnd = det.MeterEnd,
                    IsKorrigiert = !wasCorrect,
                    QualityGateLevel = entry.QualityGateResult?.TrafficLight.ToString(),
                    SourceType = "FeedbackReview"
                };
                await _kbManager.IndexSampleAsync(sample, ct).ConfigureAwait(false);
            }
            catch
            {
                // KB re-indexing failure is non-critical
            }
        }

        Interlocked.Increment(ref _feedbackCount);
        int totalCount;
        try
        {
            totalCount = _logger.GetTotalCount();
        }
        catch
        {
            totalCount = 0;
        }

        // 3. Every N persisted validations: re-learn weights in background.
        // Uses global ValidationLog count so a fresh service instance still triggers learning.
        if (ReLearnInterval > 0 && totalCount > 0 && totalCount % ReLearnInterval == 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _weightLearner.ReLearnAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Weight learning failure is non-critical
                }
            });
        }
    }

    /// <summary>Total feedback events processed in this session.</summary>
    public int TotalProcessed => _feedbackCount;
}
