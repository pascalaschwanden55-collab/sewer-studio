using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Training;

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
                    MeterEnd = det.MeterEnd
                };
                await _kbManager.IndexSampleAsync(sample, ct).ConfigureAwait(false);
            }
            catch
            {
                // KB re-indexing failure is non-critical
            }
        }

        _feedbackCount++;

        // 3. Every N validations: re-learn weights
        if (_feedbackCount % ReLearnInterval == 0)
        {
            try
            {
                await _weightLearner.ReLearnAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // Weight learning failure is non-critical
            }
        }
    }

    /// <summary>Total feedback events processed in this session.</summary>
    public int TotalProcessed => _feedbackCount;
}
